using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jcf.NetworkManager.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Jcf.NetworkManager.Tests;

/// <summary>
/// Production-mode end-to-end tests: real ASP.NET Core Identity, cookie sign-in, antiforgery,
/// role policies, lockout and account lifecycle. Only the SQL Server store is substituted with an
/// isolated in-memory store; everything else runs exactly as configured for deployment.
/// </summary>
public sealed class ProductionSecurityEndToEndTests : IClassFixture<ProductionSecurityEndToEndTests.ProductionFactory>
{
    public const string AdminUser = "platform.admin";
    public const string AdminPassword = "Initial-Admin-Pass#2026";
    public const string StrongPassword = "Operator-Pass#2026x";
    private readonly ProductionFactory factory;
    public ProductionSecurityEndToEndTests(ProductionFactory factory) => this.factory = factory;

    public sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        private readonly string passwordFile = Path.Combine(Path.GetTempPath(), "jcf-bootstrap-" + Guid.NewGuid().ToString("N"));
        private readonly string store = "jcf-production-e2e-" + Guid.NewGuid().ToString("N");
        public ProductionFactory() => File.WriteAllText(passwordFile, AdminPassword + "\n");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jcf:DemoMode", "false");
            builder.UseSetting("Jcf:EnableDeviceWrites", "false");
            builder.UseSetting("ConnectionStrings:NetworkManager", "Server=substituted-by-test;Database=unused");
            builder.UseSetting("Jcf:BootstrapAdmin:Username", AdminUser);
            builder.UseSetting("Jcf:BootstrapAdmin:PasswordFile", passwordFile);
            builder.UseSetting("Jcf:LoginRateLimit:PermitLimit", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<NetworkDbContext>>();
                services.RemoveAll<DbContextOptions<NetworkDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextFactory<NetworkDbContext>>();
                services.RemoveAll<NetworkDbContext>();
                services.AddDbContextFactory<NetworkDbContext>(o => o.UseInMemoryDatabase(store));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { File.Delete(passwordFile); } catch (IOException) { }
        }
    }

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> LoginFormTokenAsync(HttpClient client, string path = "/account/login")
    {
        var page = await client.GetStringAsync(path);
        return Regex.Match(page, "name='__RequestVerificationToken' value='([^']+)'").Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string user, string password, string returnUrl = "/")
    {
        var token = await LoginFormTokenAsync(client);
        return await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["username"] = user, ["password"] = password, ["returnUrl"] = returnUrl }));
    }

    private async Task<HttpClient> SignedInAsync(string user, string password)
    {
        var client = NewClient();
        var response = await PostLoginAsync(client, user, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/security/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    private async Task<Guid> CreateAccountAsync(HttpClient admin, string user, string role)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { username = user, password = StrongPassword, displayName = user, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static string Unique(string prefix) => prefix + "." + Guid.NewGuid().ToString("N")[..6];

    // Production security tests must explicitly grant site scopes. Roles alone convey no fleet access.
    private static async Task<Guid> CreateSiteAsAdminAsync(HttpClient admin, string label)
    {
        var r = await admin.PostAsJsonAsync("/api/v1/locations", new { name = Unique(label), type = "Station" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task GrantSiteAsync(HttpClient admin, Guid userId, Guid locationId, string role)
    {
        var r = await admin.PostAsJsonAsync("/api/v1/admin/site-permissions", new { userId, locationId, role });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_api_gets_401_and_ui_redirects_to_login_with_return_url()
    {
        using var client = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/devices")).StatusCode);
        var ui = await client.GetAsync("/devices");
        Assert.Equal(HttpStatusCode.Redirect, ui.StatusCode);
        Assert.Contains("/account/login", ui.Headers.Location!.OriginalString);
        Assert.Contains("ReturnUrl=%2Fdevices", ui.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
    }

    [Fact]
    public async Task Login_post_without_antiforgery_token_is_rejected()
    {
        using var client = NewClient();
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["username"] = AdminUser, ["password"] = AdminPassword }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_admin_signs_in_and_wrong_password_fails()
    {
        using var bad = NewClient();
        var failed = await PostLoginAsync(bad, AdminUser, "Wrong-Password#1234");
        Assert.Contains("error=1", failed.Headers.Location!.OriginalString);
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var readiness = await admin.GetStringAsync("/api/v1/admin/readiness");
        Assert.Contains("\"labWritesEnabled\":false", readiness);
    }

    [Fact]
    public async Task Api_mutations_require_the_csrf_header_even_with_a_valid_session()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        // Same authenticated session, but the request omits the X-CSRF-TOKEN header (a forged cross-site POST).
        admin.DefaultRequestHeaders.TryGetValues("X-CSRF-TOKEN", out var values);
        var token = values!.Single();
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var forged = await admin.PostAsJsonAsync("/api/v1/locations", new { name = "CSRF Probe", type = "Station" });
        admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Contains("Anti-forgery", await forged.Content.ReadAsStringAsync());
        var ok = await admin.PostAsJsonAsync("/api/v1/locations", new { name = Unique("CSRF Station"), type = "Station" });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Weak_passwords_and_unknown_roles_are_rejected_with_400()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var weak = await admin.PostAsJsonAsync("/api/v1/admin/users", new { username = Unique("weak"), password = "short1!", displayName = "Weak", role = "Viewer" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Contains("application/problem+json", weak.Content.Headers.ContentType!.ToString());
        var role = await admin.PostAsJsonAsync("/api/v1/admin/users", new { username = Unique("role"), password = StrongPassword, displayName = "R", role = "SuperUser" });
        Assert.Equal(HttpStatusCode.BadRequest, role.StatusCode);
    }

    [Fact]
    public async Task Separation_of_duties_holds_across_real_accounts()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var op = Unique("operator"); var approver = Unique("approver"); var viewer = Unique("viewer");
        var opId = await CreateAccountAsync(admin, op, "NetworkOperator");
        var approverId = await CreateAccountAsync(admin, approver, "ChangeApprover");
        var viewerId = await CreateAccountAsync(admin, viewer, "Viewer");
        var locationId = await CreateSiteAsAdminAsync(admin, "Half Way Tree");
        await GrantSiteAsync(admin, opId, locationId, "NetworkOperator");
        await GrantSiteAsync(admin, approverId, locationId, "ChangeApprover");
        await GrantSiteAsync(admin, viewerId, locationId, "Viewer");

        using var operatorClient = await SignedInAsync(op, StrongPassword);
        // Non-admin users cannot invent locations or broaden their own scope.
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsJsonAsync("/api/v1/locations",
            new { name = Unique("Unapproved Station"), type = "Station" })).StatusCode);
        var ip = "192.0.2." + Random.Shared.Next(40, 240);
        var device = await operatorClient.PostAsJsonAsync("/api/v1/devices", new { name = "HWT AP-505", kind = "Ap505", managementIp = ip, secretRef = "HWT_AP", locationId });
        Assert.Equal(HttpStatusCode.OK, device.StatusCode);
        var deviceId = (await device.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var proposal = await operatorClient.PostAsJsonAsync("/api/v1/change-proposals", new
        { deviceId, action = "CreateSsid", payloadJson = "{\"ssid\":\"JCF-HWT\",\"vlan\":120,\"pskSecretRef\":\"HWT_WIFI\"}", justification = "E2E" });
        Assert.Equal(HttpStatusCode.OK, proposal.StatusCode);
        var proposalId = (await proposal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // The requester holds no approver role: the API refuses before the service is reached.
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsJsonAsync($"/api/v1/change-proposals/{proposalId}/decision", new { decision = "approve" })).StatusCode);
        // An approver cannot submit proposals.
        using var approverClient = await SignedInAsync(approver, StrongPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await approverClient.PostAsJsonAsync("/api/v1/change-proposals", new { deviceId, action = "CreateSsid", payloadJson = "{}", justification = "x" })).StatusCode);
        var decided = await approverClient.PostAsJsonAsync($"/api/v1/change-proposals/{proposalId}/decision", new { decision = "approve" });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        Assert.Contains("\"Approved\"", await decided.Content.ReadAsStringAsync());
        // A decided proposal cannot be decided again.
        Assert.Equal(HttpStatusCode.Conflict, (await approverClient.PostAsJsonAsync($"/api/v1/change-proposals/{proposalId}/decision", new { decision = "reject" })).StatusCode);

        using var viewerClient = await SignedInAsync(viewer, StrongPassword);
        Assert.Equal(HttpStatusCode.OK, (await viewerClient.GetAsync("/api/v1/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.PostAsJsonAsync("/api/v1/devices", new { name = "x", kind = "Ap505", managementIp = "10.1.1.1", secretRef = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/v1/admin/users")).StatusCode);

        var audit = await admin.GetStringAsync("/api/v1/audit?take=50");
        Assert.Contains(op, audit);
        Assert.Contains(approver, audit);
        Assert.Contains("Review change proposal", audit);
    }

    [Fact]
    public async Task Five_failed_sign_ins_lock_the_account_until_an_admin_unlocks_it()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var user = Unique("lockme");
        var id = await CreateAccountAsync(admin, user, "Viewer");
        for (var i = 0; i < 5; i++)
        {
            using var attempt = NewClient();
            await PostLoginAsync(attempt, user, "Not-The-Password#99");
        }
        using (var locked = NewClient())
            Assert.Contains("error=1", (await PostLoginAsync(locked, user, StrongPassword)).Headers.Location!.OriginalString);
        var accounts = await admin.GetStringAsync("/api/v1/admin/users");
        Assert.Contains("\"isLockedOut\":true", accounts);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/admin/users/{id}/unlock", null)).StatusCode);
        using var unlocked = NewClient();
        Assert.Equal("/", (await PostLoginAsync(unlocked, user, StrongPassword)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Disabled_accounts_cannot_sign_in_and_admin_cannot_lock_out_administration()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var user = Unique("leaver");
        var id = await CreateAccountAsync(admin, user, "NetworkOperator");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/admin/users/{id}/disable", null)).StatusCode);
        using (var c = NewClient())
            Assert.Contains("error=1", (await PostLoginAsync(c, user, StrongPassword)).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/admin/users/{id}/enable", null)).StatusCode);
        using (var c = NewClient())
            Assert.Equal("/", (await PostLoginAsync(c, user, StrongPassword)).Headers.Location!.OriginalString);

        var accounts = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/users");
        var adminId = accounts.EnumerateArray().First(a => a.GetProperty("userName").GetString() == AdminUser).GetProperty("id").GetGuid();
        var self = await admin.PostAsync($"/api/v1/admin/users/{adminId}/disable", null);
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Contains("own account", await self.Content.ReadAsStringAsync());
        var demote = await admin.PostAsJsonAsync($"/api/v1/admin/users/{adminId}/role", new { role = "Viewer" });
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/unlock", null)).StatusCode);
    }

    [Fact]
    public async Task Role_change_takes_effect_and_is_audited()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var user = Unique("promote");
        var id = await CreateAccountAsync(admin, user, "Viewer");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{id}/role", new { role = "NetworkOperator" })).StatusCode);
        using var promoted = await SignedInAsync(user, StrongPassword);
        // A new NetworkOperator role does NOT automatically authorize location creation or fleet access.
        Assert.Equal(HttpStatusCode.Forbidden, (await promoted.PostAsJsonAsync("/api/v1/locations",
            new { name = Unique("Promoted"), type = "Station" })).StatusCode);
        var siteId = await CreateSiteAsAdminAsync(admin,"Assigned after promotion");
        await GrantSiteAsync(admin,id,siteId,"NetworkOperator");
        var ownDevice = await promoted.PostAsJsonAsync("/api/v1/devices",new {
            name=Unique("Promoted AP"),kind="Ap505",managementIp="192.0.2.170",secretRef="PROMOTED_AP",locationId=siteId
        });
        Assert.Equal(HttpStatusCode.OK,ownDevice.StatusCode);
        Assert.Contains("Viewer -> NetworkOperator", await admin.GetStringAsync("/api/v1/audit?take=100"));
    }

    [Fact]
    public async Task Site_grants_restrict_real_Identity_api_list_details_probes_and_proposals()
    {
        using var admin=await SignedInAsync(AdminUser,AdminPassword);
        var inSite=await CreateSiteAsAdminAsync(admin,"Authorized division");
        var otherSite=await CreateSiteAsAdminAsync(admin,"Unrelated division");
        var name=Unique("siteop");
        var userId=await CreateAccountAsync(admin,name,"NetworkOperator");
        await GrantSiteAsync(admin,userId,inSite,"NetworkOperator");
        using var op=await SignedInAsync(name,StrongPassword);

        var own=await op.PostAsJsonAsync("/api/v1/devices",new {
            name=Unique("Own AP"),kind="Ap505",managementIp="192.0.2.241",secretRef="OWN_AP",locationId=inSite
        });
        Assert.Equal(HttpStatusCode.OK,own.StatusCode);
        var ownId=(await own.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var unrelated=await admin.PostAsJsonAsync("/api/v1/devices",new {
            name=Unique("Other AP"),kind="Ap535",managementIp="192.0.2.242",secretRef="OTHER_AP",locationId=otherSite
        });
        Assert.Equal(HttpStatusCode.OK,unrelated.StatusCode);
        var otherId=(await unrelated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var visible=await op.GetStringAsync("/api/v1/devices");
        Assert.Contains(ownId.ToString().ToLowerInvariant(),visible.ToLowerInvariant());
        Assert.DoesNotContain(otherId.ToString().ToLowerInvariant(),visible.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Forbidden,(await op.GetAsync($"/api/v1/devices/{otherId}/ports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await op.PostAsync($"/api/v1/devices/{otherId}/probe",null)).StatusCode);
        var forbiddenProposal=await op.PostAsJsonAsync("/api/v1/change-proposals",new {
            deviceId=otherId,action="ChangeBandSteering",payloadJson="{\"value\":\"balance-bands\"}",justification="should be denied"
        });
        Assert.Equal(HttpStatusCode.Forbidden,forbiddenProposal.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await op.PostAsJsonAsync("/api/v1/devices",new {
            name="Unassigned",kind="Ap505",managementIp="192.0.2.243",secretRef="UNASSIGNED"
        })).StatusCode);
    }

    [Fact]
    public async Task Post_login_redirect_only_allows_local_paths()
    {
        using var evil = NewClient();
        Assert.Equal("/", (await PostLoginAsync(evil, AdminUser, AdminPassword, "//evil.example/phish")).Headers.Location!.OriginalString);
        using var evil2 = NewClient();
        Assert.Equal("/", (await PostLoginAsync(evil2, AdminUser, AdminPassword, "https://evil.example/")).Headers.Location!.OriginalString);
        using var local = NewClient();
        Assert.Equal("/devices", (await PostLoginAsync(local, AdminUser, AdminPassword, "/devices")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sign_out_requires_post_with_token_and_ends_the_session()
    {
        using var admin = await SignedInAsync(AdminUser, AdminPassword);
        var token = await LoginFormTokenAsync(admin, "/account/logout");
        var response = await admin.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/devices")).StatusCode);
    }
}

/// <summary>Demo-mode REST end-to-end flows using the synthetic personas.</summary>
public sealed class DemoApiEndToEndTests : IClassFixture<DemoApiEndToEndTests.IsolatedDemoFactory>
{
    private readonly IsolatedDemoFactory factory;
    public DemoApiEndToEndTests(IsolatedDemoFactory factory) => this.factory = factory;

    /// <summary>Own in-memory store so xUnit can run this class in parallel with other demo fixtures.</summary>
    public sealed class IsolatedDemoFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Jcf:DemoMode", "true");
            builder.UseSetting("Jcf:EnableDeviceWrites", "false");
            builder.UseSetting("Jcf:DemoDatabaseName", "jcf-demo-e2e-" + Guid.NewGuid().ToString("N"));
            builder.UseEnvironment("Development");
        }
    }

    private HttpClient As(string persona)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "jcf_demo_persona=" + persona);
        return client;
    }
    private static int nextTestIp = 100;
    private static string NewIp() => "198.51.100." + Interlocked.Increment(ref nextTestIp);

    [Fact]
    public async Task Domain_errors_are_problem_responses_not_500()
    {
        using var op = As("operator");
        var badIp = await op.PostAsJsonAsync("/api/v1/devices", new { name = "Bad", kind = "Ap505", managementIp = "8.8.8.8", secretRef = "X" });
        Assert.Equal(HttpStatusCode.Conflict, badIp.StatusCode);
        Assert.Contains("approved management subnet", await badIp.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await op.PostAsync($"/api/v1/devices/{Guid.NewGuid()}/probe", null)).StatusCode);

        var devices = await op.GetFromJsonAsync<JsonElement>("/api/v1/devices");
        var ap = devices.EnumerateArray().First(d => d.GetProperty("model").GetString() == "Ap505").GetProperty("id").GetGuid();
        var badJson = await op.PostAsJsonAsync("/api/v1/change-proposals", new { deviceId = ap, action = "ChangeMinTxPower", payloadJson = "{not json", justification = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, badJson.StatusCode);
        var wide = await op.PostAsJsonAsync("/api/v1/change-proposals", new { deviceId = ap, action = "SetChannelPolicy", payloadJson = "{\"channelWidthMhz\":80,\"channels5Ghz\":[36,40,44,48]}", justification = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, wide.StatusCode);
        Assert.Contains("20 MHz and 40 MHz", await wide.Content.ReadAsStringAsync());
        var dfs = await op.PostAsJsonAsync("/api/v1/change-proposals", new { deviceId = ap, action = "SetChannelPolicy", payloadJson = "{\"channelWidthMhz\":20,\"channels5Ghz\":[36,100]}", justification = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, dfs.StatusCode);
    }

    [Fact]
    public async Task Monitoring_to_recommendation_review_lifecycle()
    {
        using var op = As("operator");
        var device = await (await op.PostAsJsonAsync("/api/v1/devices", new { name = "Lifecycle AP-535", kind = "Ap535", managementIp = NewIp(), secretRef = "LIFE_AP" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = device.GetProperty("id").GetGuid();
        var probe = await op.PostAsync($"/api/v1/devices/{id}/probe", null);
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.Contains("NO device changes", await probe.Content.ReadAsStringAsync());
        Assert.Contains("Healthy", await op.GetStringAsync("/api/v1/devices"));
        Assert.Contains("LAB-AP-535", await op.GetStringAsync($"/api/v1/devices/{id}/members"));

        Assert.Equal(HttpStatusCode.OK, (await op.PostAsync("/api/v1/analysis-runs", null)).StatusCode);
        var recs = await op.GetFromJsonAsync<JsonElement>("/api/v1/recommendations");
        var rec = recs.EnumerateArray().First(r => r.GetProperty("fingerprint").GetString()!.StartsWith(id.ToString("N")) &&
                                                   r.GetProperty("fingerprint").GetString()!.EndsWith("RF_WIDTH_80_ENABLED"));
        var recId = rec.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync($"/api/v1/recommendations/{recId}/decision", new { decision = "approve" })).StatusCode);
        using var reviewer = As("reviewer-one");
        var accepted = await reviewer.PostAsJsonAsync($"/api/v1/recommendations/{recId}/decision", new { decision = "approve" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains("\"executed\":false", await accepted.Content.ReadAsStringAsync());

        var compliance = await op.PostAsync("/api/v1/compliance/evaluate", null);
        Assert.Equal(HttpStatusCode.OK, compliance.StatusCode);
        Assert.Contains("EIGHTY_MHZ_ADVERTISED", await op.GetStringAsync("/api/v1/compliance/findings"));

        var snapshot = await op.PostAsync($"/api/v1/devices/{id}/snapshot", null);
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        var snapText = await snapshot.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passphrase", snapText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", snapText, StringComparison.OrdinalIgnoreCase);

        var shadow = await op.PostAsync($"/api/v1/ml/evaluate-shadow/{id}", null);
        Assert.Contains("Insufficient firmware-certified samples", await shadow.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Location_placement_and_incident_lifecycle()
    {
        using var op = As("operator");
        var parent = await (await op.PostAsJsonAsync("/api/v1/locations", new { name = "St Catherine North " + Guid.NewGuid().ToString("N")[..4], type = "Division" })).Content.ReadFromJsonAsync<JsonElement>();
        var child = await op.PostAsJsonAsync("/api/v1/locations", new { name = "Spanish Town Station", type = "Station", parentId = parent.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);
        var childId = (await child.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await op.PostAsJsonAsync("/api/v1/locations", new { name = "Bad", type = "Planet" })).StatusCode);

        var device = await (await op.PostAsJsonAsync("/api/v1/devices", new { name = "STN CX 6200", kind = "Cx6200", managementIp = NewIp(), secretRef = "STN_CX" })).Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = device.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync($"/api/v1/devices/{deviceId}/placement", new { locationId = childId })).StatusCode);
        Assert.Contains(deviceId.ToString(), await op.GetStringAsync("/api/v1/placements"));
        Assert.Equal(HttpStatusCode.NotFound, (await op.PostAsJsonAsync($"/api/v1/devices/{deviceId}/placement", new { locationId = Guid.NewGuid() })).StatusCode);

        var space = await op.PostAsJsonAsync("/api/v1/physical-spaces", new { locationId = childId, type = "Building", name = "Main block" });
        Assert.Equal(HttpStatusCode.OK, space.StatusCode);

        var incident = await op.PostAsJsonAsync("/api/v1/incidents", new { deviceId, locationId = childId, title = "Uplink flapping", description = "Port 1/1/49 errors", severity = "High" });
        Assert.Equal(HttpStatusCode.OK, incident.StatusCode);
        var incidentId = (await incident.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await op.PostAsJsonAsync("/api/v1/incidents", new { locationId = Guid.NewGuid(), title = "x", description = "y", severity = "Low" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/state", new { state = "Investigating", owner = "NOC shift A" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/state", new { state = "Resolved" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/state", new { state = "Open" })).StatusCode);
    }

    [Fact]
    public async Task Csv_export_neutralises_spreadsheet_formulas()
    {
        using var op = As("operator");
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync("/api/v1/devices", new { name = "=HYPERLINK(\"http://x\")", kind = "Ap505", managementIp = NewIp(), secretRef = "CSV_AP" })).StatusCode);
        var csv = await op.GetStringAsync("/api/v1/reports/device-health.csv");
        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\")\"", csv);
    }

    [Fact]
    public async Task Viewer_persona_is_read_only_and_audit_is_readable()
    {
        using var viewer = As("viewer");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/dashboard/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/analysis-runs", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/admin/telemetry/rollup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/audit?take=5")).StatusCode);
    }
}
