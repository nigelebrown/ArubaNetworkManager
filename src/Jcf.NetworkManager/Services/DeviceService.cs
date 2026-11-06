using System.Text.Json;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Jcf.NetworkManager.Connectors;
using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jcf.NetworkManager.Services;

public sealed class DeviceService(IDbContextFactory<NetworkDbContext> dbFactory,IEnumerable<IDeviceConnector> connectors,IConfiguration configuration,SiteAccessService siteAccess)
{
    public async Task<Device> RegisterAsync(string name,DeviceKind kind,string ip,string secretRef,Guid? locationId,string actor,CancellationToken ct=default)
    {
        await siteAccess.RequireActorLocationAsync(actor,locationId,"Operate",ct);
        var cidrs=configuration.GetSection("Jcf:AllowedManagementCidrs").Get<string[]>() ?? Array.Empty<string>();
        if (!AddressPolicy.IsAllowed(ip,cidrs)) throw new InvalidOperationException("Device IP is not inside an approved management subnet.");
        if (string.IsNullOrWhiteSpace(name) || name.Length>120) throw new InvalidOperationException("Name is required (1-120 characters).");
        // Secret identifiers are NOT credentials; credentials must be provisioned in the service environment.
        if (!System.Text.RegularExpressions.Regex.IsMatch(secretRef??"", @"^[A-Za-z0-9_]{1,40}$"))
            throw new InvalidOperationException("Secret reference must be letters, numbers or underscores.");
        await using var db=await dbFactory.CreateDbContextAsync(ct);
        if (await db.Devices.AnyAsync(d=>d.ManagementIp==ip,ct)) throw new InvalidOperationException("IP address is already registered.");
        if(locationId.HasValue && !await db.Locations.AnyAsync(l=>l.Id==locationId,ct)) throw new InvalidOperationException("Location does not exist.");
        var d=new Device{Name=name.Trim(),Kind=kind,ManagementIp=ip,SecretRef=secretRef!,LocationId=locationId};
        db.Devices.Add(d);
        if (locationId is Guid loc)
            db.DevicePlacements.Add(new DevicePlacement {DeviceId=d.Id,LocationId=loc,ChangedBy=actor});
        db.AuditEvents.Add(new AuditEvent{Actor=actor,Action="Register device",Target=d.Id.ToString(),Outcome=$"{d.Kind} at {ip}"});
        await db.SaveChangesAsync(ct);
        return d;
    }

    // One read probe per device per application process at a time. This prevents a background
    // poll and an operator acceptance run from corrupting cluster inventory or exhausting
    // Aruba's limited API sessions. Distributed poll scheduling remains a production HA gate.
    private static readonly ConcurrentDictionary<Guid,SemaphoreSlim> ProbeLocks = new();
    public async Task<ProbeResult> ProbeAsync(Guid deviceId,string actor,CancellationToken ct=default)
    {
        await siteAccess.RequireActorDeviceAsync(actor,deviceId,"Operate",ct);
        return await ProbeWithLockAsync(deviceId,actor,ct);
    }

    // A distinct method for the background worker; callers cannot impersonate this through an account name.
    public Task<ProbeResult> ProbeAsWorkerAsync(Guid deviceId,CancellationToken ct=default)
        => ProbeWithLockAsync(deviceId,"monitoring-worker",ct);

    private async Task<ProbeResult> ProbeWithLockAsync(Guid deviceId,string actor,CancellationToken ct)
    {
        var gate = ProbeLocks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1,1));
        await gate.WaitAsync(ct);
        try {
            var result=await ProbeCoreAsync(deviceId,actor,ct);
            // The full VC membership may span physical locations, and clients may include MAC addresses.
            // Until every member/client has independently certified site scope, detailed identities
            // are available only to PlatformAdmin. Scoped operators still receive counts and RF metrics.
            if(actor!="monitoring-worker" && !(await siteAccess.ForActorAsync(actor,"Read",ct)).AllLocations)
                return result with {Members=new(),Clients=null,
                    ApiChecks=[..result.ApiChecks,"Cluster member identities and client MACs withheld for site-scoped operator."]};
            return result;
        }
        finally { gate.Release(); }
    }

    private async Task<ProbeResult> ProbeCoreAsync(Guid deviceId,string actor,CancellationToken ct)
    {
        await using var db=await dbFactory.CreateDbContextAsync(ct);
        var device=await db.Devices.SingleOrDefaultAsync(d=>d.Id==deviceId,ct)
            ?? throw new KeyNotFoundException("Unknown device.");
        if (!device.Enabled) throw new InvalidOperationException("Device disabled.");
        var cidrs=configuration.GetSection("Jcf:AllowedManagementCidrs").Get<string[]>() ?? Array.Empty<string>();
        if (!AddressPolicy.IsAllowed(device.ManagementIp,cidrs)) throw new InvalidOperationException("Device IP no longer within approved management subnets.");
        var connector=connectors.FirstOrDefault(c=>c.Supports(device.Kind))
            ?? throw new NotSupportedException("Device adapter unavailable.");
        ProbeResult result;
        try {result=await connector.ProbeAsync(device,ct);}
        catch(Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        { result=new ProbeResult(false,null,null,null,null,null,new(),new(),new(),ex.GetType().Name+": "+ex.Message,new(){"FAIL connection"}); }
        device.LastProbeUtc=DateTime.UtcNow;
        if(result.Connected) device.LastSeenUtc=DateTime.UtcNow;
        device.LastStatus=result.Connected && result.Issue is null ? "Healthy" : result.Connected?"Degraded":"Offline";
        device.LastIssue=result.Issue;
        if (result.Firmware is not null) device.Firmware=result.Firmware;
        db.TelemetrySnapshots.Add(new TelemetrySnapshot {
            DeviceId=device.Id,ApiConnected=result.Connected,ApCount=result.ApCount,ClientCount=result.ClientCount,
            PortCount=result.PortCount,VlanCount=result.VlanCount,
            RadioChannel=result.Observation?.Channel,ChannelWidthMhz=result.Observation?.WidthMhz,
            ChannelUtilizationPct=result.Observation?.UtilizationPct,NoiseFloorDbm=result.Observation?.NoiseFloorDbm,
            ClientRetryPct=result.Observation?.ClientRetryPct,ObservationSource=result.Observation?.Source??"Unavailable",
            ArmJson=JsonSerializer.Serialize(result.Arm),
            MetricAvailabilityJson=JsonSerializer.Serialize(result.Fields),Problem=result.Issue
        });
        if(result.Observation is RfObservation r)
            db.RadioObservations.Add(new RadioObservation{DeviceId=device.Id,Channel=r.Channel,WidthMhz=r.WidthMhz,
                UtilizationPct=r.UtilizationPct,NoiseFloorDbm=r.NoiseFloorDbm,ErrorPct=r.ClientRetryPct,
                Source=r.Source,Quality="Heuristic-uncertified"});
        if(result.PortReadings is {Count:>0}) {
            foreach(var port in result.PortReadings.Take(200))
                db.PortObservations.Add(new PortObservation{DeviceId=device.Id,Port=port.Name,LinkState=port.State??"Unknown",
                    RxBytes=port.RxBytes,TxBytes=port.TxBytes,RxErrors=port.RxErrors,TxErrors=port.TxErrors,
                    PoeWatts=port.PoeWatts,Source="AOS-CX REST interfaces"});
        }
        // Client identifiers are never stored in plaintext. A private HMAC key must be configured,
        // otherwise the platform records count/metric availability only.
        if(result.Clients is {Count:>0})
        {
            var key=Environment.GetEnvironmentVariable("JCF_CLIENT_HASH_KEY");
            if(!string.IsNullOrEmpty(key) && key.Length>=32)
            {
                foreach(var client in result.Clients.Take(500))
                {
                    var hash=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(client.Mac)));
                    var current=await db.ClientSessions.FirstOrDefaultAsync(x=>x.DeviceId==device.Id && x.ClientHash==hash && x.Ssid==client.Ssid,ct);
                    if(current is null)db.ClientSessions.Add(new ClientSession{DeviceId=device.Id,ClientHash=hash,
                        Ssid=client.Ssid,Band=client.Band,RssiDbm=client.RssiDbm,SnrDb=client.SnrDb,
                        AssociatedVlan=client.Vlan,Source="Instant show clients; identity only (uncertified CLI parse)"});
                    else current.LastSeenUtc=DateTime.UtcNow;
                }
            }
        }
        if(result.Members.Count>0)
        {
            var old=await db.ClusterMembers.Where(m=>m.ConductorDeviceId==deviceId).ToListAsync(ct);
            db.ClusterMembers.RemoveRange(old);
            foreach(var member in result.Members) db.ClusterMembers.Add(new ClusterMember {
                ConductorDeviceId=deviceId,Name=member.Name,Model=member.Model,Zone=member.Zone,
                IpMasked=member.IpMasked,DiscoveryConfidence=member.Confidence
            });
        }
        var outageKey="APIDOWN:"+device.Id.ToString("N");
        var outage=await db.NetworkAlerts.FirstOrDefaultAsync(x=>x.Fingerprint==outageKey && x.State=="Open",ct);
        if(device.LastStatus!="Healthy") {
            if(outage is null) db.NetworkAlerts.Add(new NetworkAlert{DeviceId=device.Id,Fingerprint=outageKey,
                Severity="Warning",Category="Device API unavailable",Detail="Management API did not complete all checks."});
            else outage.LastSeenUtc=DateTime.UtcNow;
        }else if(outage is not null) {outage.State="Resolved";outage.LastSeenUtc=DateTime.UtcNow;}
        db.AuditEvents.Add(new AuditEvent {Actor=actor,Action="Read-only device probe",Target=deviceId.ToString(),Outcome=device.LastStatus});
        await db.SaveChangesAsync(ct);
        return result;
    }
}
