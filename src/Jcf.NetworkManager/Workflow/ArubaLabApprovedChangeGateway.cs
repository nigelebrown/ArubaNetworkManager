using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jcf.NetworkManager.Connectors;
using Jcf.NetworkManager.Domain;
using Jcf.NetworkManager.Services;

namespace Jcf.NetworkManager.Workflow;

/// <summary>LAB-ONLY native REST execution adapter. Only reached through ChangeWorkflowService's
/// exact-target, independent-approval, firmware and capability gates. No generic command execution.</summary>
public sealed class ArubaLabApprovedChangeGateway(DeviceHttpFactory factory,SecretResolver secrets) : IApprovedChangeGateway
{
    public Task<ChangeExecutionOutcome> ExecuteAsync(Device device,ConfigProposal p,CancellationToken ct) =>
        device.IsAccessPoint ? InstantAsync(device,p,ct) : CxAsync(device,p,ct);

    private async Task<ChangeExecutionOutcome> InstantAsync(Device d,ConfigProposal p,CancellationToken ct)
    {
        if(p.Action is not (ConfigAction.CreateSsid or ConfigAction.ChangeBandSteering or ConfigAction.ChangeMinTxPower))
            return new(false,true,false,"Instant action not allowlisted.");
        using var client=factory.Create(d,4343);
        var (user,password)=secrets.Resolve(d.SecretRef);
        string? sid=null;
        bool attempted=false;
        string? old=null;
        string? testSsid=null;
        // Compensation must still run if the caller's request is cancelled mid-write, so it switches to its own bounded token.
        var op=ct;
        using var compensation=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try {
            using var login=await client.PostAsJsonAsync("rest/login",new {user,passwd=password},ct);
            using var reply=JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct));
            if(!login.IsSuccessStatusCode || !reply.RootElement.TryGetProperty("Status",out var status) || !string.Equals(status.GetString(),"success",StringComparison.OrdinalIgnoreCase) ||
                !reply.RootElement.TryGetProperty("sid",out var token)) return new(false,true,false,"Instant REST login rejected.");
            sid=token.GetString();if(string.IsNullOrWhiteSpace(sid)) return new(false,true,false,"Instant session missing.");
            var members=await Command("show aps");
            var count=Regex.Match(members,@"\b(\d+)\s+Access Points?\b",RegexOptions.IgnoreCase);
            if(!count.Success || count.Groups[1].Value!="1")
                return new(false,true,false,"Multi-member/unknown Instant cluster; live lab write blocked.");
            var baseline=await Command("show running-config");
            using var data=JsonDocument.Parse(p.PayloadJson);
            var root=data.RootElement;
            if(p.Action==ConfigAction.CreateSsid) {
                testSsid=root.GetProperty("ssid").GetString()!;
                if(Profile(baseline,testSsid)) return new(false,true,false,"SSID already exists; refusing overwrite.");
                var vlan=root.GetProperty("vlan").GetInt32();
                var secretRef=root.GetProperty("pskSecretRef").GetString()!;
                var psk=Environment.GetEnvironmentVariable("JCF_WLAN_"+secretRef+"_PASS");
                if(psk is null || psk.Length<8 || psk.Length>63)
                    return new(false,true,false,"PSK secret reference unresolved or invalid. No device change.");
                // Explicitly never put the passphrase in database or emitted logs.
                var body=new Dictionary<string,object> { ["ssid-profile"]=new Dictionary<string,object> {
                    ["action"]="create",["ssid-profile"]=testSsid,
                    ["essid"]=new {action="create",value=testSsid},["type"]="employee",["opmode"]="wpa2-psk-aes",
                    ["wpa-passphrase"]=psk,["vlan"]=new {action="create",value=vlan.ToString()},["rf-band"]="all",["enable"]="yes"
                }};
                attempted=true;
                using(var answer=await client.PostAsJsonAsync("rest/ssid?sid="+Uri.EscapeDataString(sid),body,ct)) {
                    if(!await Accepted(answer,ct)) throw new InvalidOperationException("SSID API rejected the write; checking rollback.");
                }
                var after=await Command("show running-config");
                if(!Profile(after,testSsid) || !SsidVlan(after,testSsid,vlan)) throw new InvalidOperationException("SSID or VLAN readback did not match.");
                return new(true,true,false,"SSID created and VLAN binding verified through Instant readback (LAB ONLY).");
            }
            var param=p.Action==ConfigAction.ChangeBandSteering?"band-steering-mode":"min-tx-power";
            var baselineArm=InstantConnector.ExtractArm(baseline);
            if(!baselineArm.TryGetValue(param,out old)) return new(false,true,false,"No explicit ARM baseline for "+param+"; no write attempted.");
            string target=p.Action==ConfigAction.ChangeMinTxPower?root.GetProperty("value").GetInt32().ToString():root.GetProperty("value").GetString()!;
            if(target==old) return new(true,true,false,"No change required; target ARM value already matches.");
            attempted=true;
            if(!await ArmWrite(param,target)) throw new InvalidOperationException("ARM API rejected write; attempting restoration.");
            var now=InstantConnector.ExtractArm(await Command("show running-config"));
            if(!now.TryGetValue(param,out var readback) || readback!=target) throw new InvalidOperationException("ARM readback mismatch; attempting restoration.");
            return new(true,true,false,"ARM "+param+" applied and exact readback verified on lab AP.");
        }
        catch(Exception exc) when(exc is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException) {
            if(!attempted) return new(false,true,false,"Lab preflight failed: "+exc.GetType().Name);
            op=compensation.Token;
            try {
                if(sid is null) return new(false,false,true,"Write outcome unknown; Instant session lost. Inspect manually.");
                if(testSsid is not null) {
                    var body=new Dictionary<string,object>{["ssid-profile"]=new Dictionary<string,string>{{"action","delete"},{"ssid-profile",testSsid}}};
                    using var deleted=await client.PostAsJsonAsync("rest/ssid?sid="+Uri.EscapeDataString(sid),body,op);
                    if(Profile(await Command("show running-config"),testSsid)) return new(false,false,true,"SSID restoration not verified; manual cleanup required.");
                } else if(old is not null) {
                    var param=p.Action==ConfigAction.ChangeBandSteering?"band-steering-mode":"min-tx-power";
                    if(!await ArmWrite(param,old)) return new(false,false,true,"ARM restoration API rejected; manual recovery required.");
                    var check=InstantConnector.ExtractArm(await Command("show running-config"));
                    if(!check.TryGetValue(param,out var value) || value!=old) return new(false,false,true,"ARM restoration readback failed; manual recovery required.");
                }
                return new(false,true,false,"Write not confirmed; original setting restored and verified.");
            }catch {return new(false,false,true,"Write outcome and rollback unverified. Manual device inspection required.");}
        }
        finally {
            if(sid is not null) try {using var _=await client.PostAsJsonAsync("rest/logout",new {sid},CancellationToken.None);}catch{}
        }
        async Task<string> Command(string command) {
            var path="rest/show-cmd?iap_ip_addr="+Uri.EscapeDataString(d.ManagementIp)+"&cmd="+Uri.EscapeDataString(command)+"&sid="+Uri.EscapeDataString(sid!);
            using var r=await client.GetAsync(path,op);r.EnsureSuccessStatusCode();
            using var doc=JsonDocument.Parse(await r.Content.ReadAsStringAsync(op));
            if(!doc.RootElement.TryGetProperty("Status-code",out var code) || code.ToString()!="0" ||
               !doc.RootElement.TryGetProperty("Command output",out var output)) throw new InvalidOperationException("Readback unsupported.");
            return output.GetString()??"";
        }
        async Task<bool> ArmWrite(string key,string value) {
            var obj=new Dictionary<string,object>{["arm"]=new Dictionary<string,object>{["action"]="create",[key]=new {action="create",value}}};
            using var resp=await client.PostAsJsonAsync("rest/arm?sid="+Uri.EscapeDataString(sid!),obj,op);
            return await Accepted(resp,op);
        }
    }
    static bool Profile(string config,string name) => Regex.IsMatch(config,
        "(?m)^\\s*wlan ssid-profile\\s+\"?"+Regex.Escape(name)+"\"?\\s*$");
    static bool SsidVlan(string config,string name,int vlan) {
        var match=Regex.Match(config,"(?m)^\\s*wlan ssid-profile\\s+\"?"+Regex.Escape(name)+"\"?\\s*$");
        if(!match.Success) return false;
        var lines=config[(match.Index+match.Length)..].Split('\n');
        foreach(var line in lines) {
            if(line.Length>0 && !char.IsWhiteSpace(line[0])) break;
            if(Regex.IsMatch(line,@"^\s*vlan\s+"+vlan+@"\s*$"))return true;
        }
        return false;
    }
    static async Task<bool> Accepted(HttpResponseMessage response,CancellationToken ct) {
        if(!response.IsSuccessStatusCode)return false;
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if(doc.RootElement.TryGetProperty("Status-code",out var code) && code.ToString()!="0")return false;
        if(doc.RootElement.TryGetProperty("Status",out var status) && status.ValueKind==JsonValueKind.String &&
           (string.Equals(status.GetString(),"failed",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.GetString(),"failure",StringComparison.OrdinalIgnoreCase))) return false;
        return true;
    }

    private async Task<ChangeExecutionOutcome> CxAsync(Device d,ConfigProposal proposal,CancellationToken ct) {
        if(proposal.Action!=ConfigAction.CreateSwitchVlan) return new(false,true,false,"Switch action not allowlisted.");
        using var http=factory.Create(d,443);
        var (username,password)=secrets.Resolve(d.SecretRef);
        // Firmware-specific lab adapter: AOS-CX 10.09+ REST session login uses
        // query parameters, a cookie and x-csrf-token. Never log the login URI.
        var loginPath="rest/latest/login?username="+Uri.EscapeDataString(username)+
            "&password="+Uri.EscapeDataString(password);
        using var login=new HttpRequestMessage(HttpMethod.Post,loginPath)
            {Content=new ByteArrayContent(Array.Empty<byte>())};
        login.Headers.TryAddWithoutValidation("x-use-csrf-token","true");
        string? path=null,name=null;bool attempted=false,authenticated=false;
        using var compensation=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try {
            using var response=await http.SendAsync(login,ct);
            if(!response.IsSuccessStatusCode) return new(false,true,false,"AOS-CX REST session login rejected.");
            authenticated=true;
            if(response.Headers.TryGetValues("x-csrf-token",out var values))
                http.DefaultRequestHeaders.TryAddWithoutValidation("x-csrf-token",values.FirstOrDefault());
            using var obj=JsonDocument.Parse(proposal.PayloadJson);
            int vlan=obj.RootElement.GetProperty("vlan").GetInt32();
            name=obj.RootElement.GetProperty("name").GetString()!;
            path="rest/latest/system/vlans/"+vlan;
            using(var pre=await http.GetAsync(path,ct)) {
                if(pre.StatusCode!=HttpStatusCode.NotFound) return new(false,true,false,"Target VLAN exists or cannot be proven absent. No write attempted.");
            }
            attempted=true;
            using(var created=await http.PostAsJsonAsync("rest/latest/system/vlans",
                new {id=vlan,name,type="static",admin="up"},ct)) {
                if(!created.IsSuccessStatusCode) throw new InvalidOperationException("VLAN create rejected. Rollback safety check required.");
            }
            using var read=await http.GetAsync(path,ct);
            if(!read.IsSuccessStatusCode) throw new InvalidOperationException("VLAN write readback failed.");
            using var json=JsonDocument.Parse(await read.Content.ReadAsStringAsync(ct));
            if(!json.RootElement.TryGetProperty("name",out var observed) || observed.GetString()!=name)
                throw new InvalidOperationException("Created VLAN name mismatch. Rollback required.");
            return new(true,true,false,"Lab VLAN created and exact name/ID verified by CX REST readback.");
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException) {
            if(!attempted) return new(false,true,false,"Switch preflight failed; no write attempted: "+ex.GetType().Name);
            if(path is null || name is null) return new(false,false,true,"VLAN state unknown; manual inspection required.");
            var op=compensation.Token;
            try {
                using var check=await http.GetAsync(path,op);
                if(check.StatusCode==HttpStatusCode.NotFound)return new(false,true,false,"VLAN create unconfirmed; target remains absent.");
                if(!check.IsSuccessStatusCode)return new(false,false,true,"Cannot inspect temporary VLAN; manual verification required.");
                using var actual=JsonDocument.Parse(await check.Content.ReadAsStringAsync(op));
                if(!actual.RootElement.TryGetProperty("name",out var n)||n.GetString()!=name)
                    return new(false,false,true,"VLAN ownership not verified; manual inspection required, deletion blocked.");
                using var deleted=await http.DeleteAsync(path,op);
                using var confirm=await http.GetAsync(path,op);
                return confirm.StatusCode==HttpStatusCode.NotFound
                    ?new(false,true,false,"Temporary lab VLAN removed and verified after failed create/readback.")
                    :new(false,false,true,"Lab VLAN cleanup not verified. Manual recovery required.");
            } catch {return new(false,false,true,"VLAN write outcome unknown. Manual inspection required.");}
        } finally {
            if(authenticated) try {using var _=await http.PostAsync("rest/latest/logout",null,CancellationToken.None);}catch{}
        }
    }
}
