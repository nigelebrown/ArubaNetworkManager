using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Jcf.NetworkManager.Components;
using Jcf.NetworkManager.Connectors;
using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Jcf.NetworkManager.Services;
using Jcf.NetworkManager.Workflow;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder=WebApplication.CreateBuilder(args);
// Containerized Identity cookies must survive restarts; keys are stored only in a protected volume.
var dataProtectionPath=builder.Configuration["Jcf:DataProtectionKeysPath"];
if(!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    Directory.CreateDirectory(dataProtectionPath);
    builder.Services.AddDataProtection()
        .SetApplicationName("Jcf.NetworkManager")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}
var demo=builder.Configuration.GetValue<bool>("Jcf:DemoMode");
if(demo && !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
    throw new InvalidOperationException("DemoMode cannot run in Production. Set ASPNETCORE_ENVIRONMENT=Development for isolated demos.");
// Live equipment changes are gated by explicit lab allowlist, current certification and independent approval.
// Production writes remain disabled until a supervised, firmware-specific acceptance gate passes.
if(!demo && builder.Configuration.GetValue<bool>("Jcf:EnableDeviceWrites") &&
   !builder.Configuration.GetValue<bool>("Jcf:LiveLabWrites:Enabled"))
    throw new InvalidOperationException("Global device writes require a separately certified execution gateway; use isolated lab allowlist only.");
// Fail fast if the management-network allowlist is missing or malformed. An empty list silently refuses every
// device (e.g. when the process is started outside its content root and appsettings.json is never loaded).
var managementCidrs=builder.Configuration.GetSection("Jcf:AllowedManagementCidrs").Get<string[]>() ?? [];
if(managementCidrs.Length==0)
    throw new InvalidOperationException($"Jcf:AllowedManagementCidrs is empty. Check that appsettings.json was loaded (content root: {builder.Environment.ContentRootPath}) or supply Jcf__AllowedManagementCidrs__0.");
var badCidrs=managementCidrs.Where(c=>!AddressPolicy.IsValidCidr(c)).ToArray();
if(badCidrs.Length>0)
    throw new InvalidOperationException("Invalid entries in Jcf:AllowedManagementCidrs: "+string.Join(", ",badCidrs));
// API contract uses enum names ("Ap505", "CreateSsid", "PendingApproval"), not opaque integers.
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
// Domain validation/authorization failures become 400/403/404/409 problem responses, not HTTP 500.
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// Cookie-authenticated JSON endpoints must require same-origin anti-forgery proof.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.Configure<ForwardedHeadersOptions>(o=>{
    // Only the on-host loopback reverse proxy may supply the scheme/client address.
    o.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Add(IPAddress.Loopback);
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
    o.ForwardLimit=1;
});
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    // Per-source throttle against credential stuffing. Per-account guessing is stopped separately by
    // Identity lockout (5 failures). Many operators can share one NAT/proxy address, so the per-IP
    // budget must comfortably exceed a shift change at a busy station.
    var loginPermits=Math.Clamp(builder.Configuration.GetValue("Jcf:LoginRateLimit:PermitLimit",30),3,1000);
    var loginWindow=TimeSpan.FromMinutes(Math.Clamp(builder.Configuration.GetValue("Jcf:LoginRateLimit:WindowMinutes",5),1,60));
    o.AddPolicy("login",ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions {
        PermitLimit=loginPermits,Window=loginWindow,QueueLimit=0,AutoReplenishment=true
    }));
});
if(demo)
{
    // Do not expose development's synthetic identity beyond the same machine.
    // Docker publishes only to the host loopback; listen on all container interfaces internally.
    if(!builder.Configuration.GetValue<bool>("Jcf:DockerMode"))
        builder.WebHost.UseUrls("http://127.0.0.1:5180");
    builder.Services.AddAuthentication("Demo")
        .AddScheme<AuthenticationSchemeOptions,DemoAuthenticationHandler>("Demo",_=>{});
    builder.Services.AddDbContextFactory<NetworkDbContext>(o=>o.UseInMemoryDatabase(builder.Configuration["Jcf:DemoDatabaseName"] ?? "JCF-NetworkManager-Demo"));
}
else
{
    var connection=builder.Configuration.GetConnectionString("NetworkManager");
    if(string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Production startup requires a SQL Server connection string.");
    builder.Services.AddDbContextFactory<NetworkDbContext>(o=>o.UseSqlServer(connection,
        opts=>opts.EnableRetryOnFailure(3,TimeSpan.FromSeconds(4),null)));
    builder.Services.AddIdentity<OperatorUser,IdentityRole<Guid>>(o=> {
        o.Password.RequiredLength=14;
        o.Password.RequireDigit=true;
        o.Password.RequireUppercase=true;
        o.Password.RequireLowercase=true;
        o.Password.RequireNonAlphanumeric=true;
        o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
        o.Lockout.MaxFailedAccessAttempts=5;
        o.User.RequireUniqueEmail=false;
    }).AddEntityFrameworkStores<NetworkDbContext>().AddDefaultTokenProviders();
    builder.Services.ConfigureApplicationCookie(o=> {
        o.Cookie.Name="JcfNetworkManager.Session";
        o.Cookie.HttpOnly=true;
        o.Cookie.SecurePolicy=CookieSecurePolicy.Always;
        o.Cookie.SameSite=SameSiteMode.Lax;
        o.ExpireTimeSpan=TimeSpan.FromHours(8);
        o.SlidingExpiration=false;
        o.LoginPath="/account/login";
        o.AccessDeniedPath="/account/login";
        // REST clients get 401/403 status codes instead of an HTML login redirect.
        o.Events.OnRedirectToLogin=ctx => ApiAwareRedirect(ctx,StatusCodes.Status401Unauthorized);
        o.Events.OnRedirectToAccessDenied=ctx => ApiAwareRedirect(ctx,StatusCodes.Status403Forbidden);
    });
    builder.Services.Configure<SecurityStampValidatorOptions>(o=>o.ValidationInterval=TimeSpan.FromMinutes(1));

}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Viewer",p=>p.RequireAuthenticatedUser())
    .AddPolicy("Operator",p=>p.RequireRole(JcfRoles.Operators))
    .AddPolicy("Approver",p=>p.RequireRole(JcfRoles.Approvers))
    .AddPolicy("Admin",p=>p.RequireRole(JcfRoles.Admins));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SecretResolver>();
builder.Services.AddSingleton<DeviceHttpFactory>();
if(demo) builder.Services.AddSingleton<IDeviceConnector,DemoConnector>();
else
{
    builder.Services.AddSingleton<IDeviceConnector,InstantConnector>();
    builder.Services.AddSingleton<IDeviceConnector,CxConnector>();
}
builder.Services.AddScoped<SiteAccessService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<HardwareAcceptanceService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<ChangeWorkflowService>();
builder.Services.AddScoped<CapabilityService>();
builder.Services.AddScoped<EnterpriseOperationsService>();
builder.Services.AddScoped<NetworkDiagnosticsService>();
builder.Services.AddScoped<IncidentService>();
if(!demo) builder.Services.AddScoped<OperatorAccountService>();
// Only the approval workflow is allowed to resolve this service.
if(demo) builder.Services.AddScoped<IApprovedChangeGateway,DemoChangeGateway>();
else builder.Services.AddScoped<IApprovedChangeGateway,ArubaLabApprovedChangeGateway>();
builder.Services.AddHostedService<PollingWorker>();
builder.Services.AddHostedService<HourlyAnalysisWorker>();
builder.Services.AddHostedService<ExecutionRecoveryWorker>();
builder.Services.AddHostedService<MaintenanceWorker>();

var app=builder.Build();
if(!demo) app.UseForwardedHeaders();
app.UseExceptionHandler();
// Browser hardening for an operations console: no framing (clickjacking), no MIME sniffing, no referrer
// leakage, and a CSP that permits only this origin's scripts and the Blazor Server WebSocket.
app.Use(async (context,next)=>{
    var h=context.Response.Headers;
    h["X-Content-Type-Options"]="nosniff";
    h["X-Frame-Options"]="DENY";
    h["Referrer-Policy"]="no-referrer";
    h["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
    h["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; "+
        "connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
    await next();
});
if(!demo) {app.UseHsts();app.UseHttpsRedirection();}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Defense in depth against cross-site configuration submissions through Identity cookies.
// The Blazor server UI uses service methods and does not bypass its own authorization checks.
if(!demo) app.Use(async (context,next) => {
    bool unsafeApi = context.Request.Path.StartsWithSegments("/api/v1") &&
        (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
         HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method));
    if(unsafeApi && context.User.Identity?.IsAuthenticated==true)
    {
        var antiforgery=context.RequestServices.GetRequiredService<IAntiforgery>();
        if(!await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode=StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new {error="Anti-forgery token required; GET /api/v1/security/csrf first."});
            return;
        }
    }
    await next();
});
app.UseAntiforgery();

if(!demo) app.MapLocalAuth();
else app.MapDemoPersonas();
app.MapGet("/readyz",async(IDbContextFactory<NetworkDbContext> factory)=>{
    try {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Database.CanConnectAsync() ? Results.Ok(new{status="ready"}) : Results.StatusCode(503);
    }
    catch { return Results.StatusCode(503); }
}).AllowAnonymous();
app.MapOpenApi("/api/openapi.json").RequireAuthorization("Admin");
app.MapGet("/healthz",()=>Results.Ok(new {app="JCF Network Manager",status="up",version=AppInfo.Version,demo})).AllowAnonymous();
// Read-only external application APIs; all mutation requests are handled through the authenticated UI workflows.
var api=app.MapGroup("/api/v1").RequireAuthorization("Viewer");
api.MapGet("/security/csrf",(HttpContext context,IAntiforgery antiforgery) => {
    var tokens=antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new {header="X-CSRF-TOKEN",token=tokens.RequestToken});
});
api.MapGet("/devices",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    return await scope.Devices(db.Devices.AsNoTracking()).Select(d=>new {
        d.Id,d.Name,Model=d.Kind.ToString(),d.ManagementIp,d.LocationId,d.Firmware,d.LastStatus,d.LastSeenUtc
    }).ToListAsync(ct);
});
api.MapGet("/locations",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    return await scope.Locations(db.Locations.AsNoTracking()).ToListAsync(ct);
});
api.MapGet("/clusters",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    var visible=scope.Devices(db.Devices.AsNoTracking());
    return await db.ClusterMembers.AsNoTracking().Where(m=>visible.Any(d=>d.Id==m.ConductorDeviceId)).ToListAsync(ct);
}).RequireAuthorization("Admin"); // VC members may reside in different physical locations.
api.MapGet("/recommendations",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    var visible=scope.Devices(db.Devices.AsNoTracking());
    return await db.Recommendations.AsNoTracking().Where(r=>visible.Any(d=>d.Id==r.DeviceId))
        .OrderByDescending(r=>r.AtUtc).Take(200).ToListAsync(ct);
});
api.MapGet("/analysis-runs",async(IDbContextFactory<NetworkDbContext> dbf,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    return await db.AnalysisRuns.AsNoTracking().OrderByDescending(x=>x.AtUtc).Take(100).ToListAsync(ct);
}).RequireAuthorization("Admin"); // Fleet-wide run totals have no site dimension.
api.MapGet("/latest-samples",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    var visible=scope.Devices(db.Devices.AsNoTracking());
    return await db.TelemetrySnapshots.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId))
        .OrderByDescending(x=>x.AtUtc).Take(100).ToListAsync(ct);
});
api.MapPost("/execution-jobs",()=>Results.Problem(
    title:"Configuration deployment not enabled",detail:"No automatic or manual device writes until approval/execution gateway certification.",statusCode:501))
    .RequireAuthorization("Operator");
api.MapGet("/config-proposals",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    var visible=scope.Devices(db.Devices.AsNoTracking());
    return await db.ConfigProposals.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId))
        .OrderByDescending(x=>x.RequestedUtc).Take(100)
        .Select(x=>new{x.Id,x.DeviceId,x.Action,x.State,x.RequestedBy,x.RequestedUtc,x.ApprovedBy,x.ApprovedUtc,x.LastOutcome}).ToListAsync(ct);
});
api.MapGet("/capabilities",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    var visible=scope.Devices(db.Devices.AsNoTracking());
    return await db.DeviceCapabilities.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).ToListAsync(ct);
});
api.MapGet("/groups",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    return await db.DeviceGroups.AsNoTracking().Where(g=>scope.AllLocations ||
        (g.LocationId.HasValue && scope.LocationIds.Contains(g.LocationId.Value))).ToListAsync(ct);
});
api.MapGet("/alerts",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
    await using var db=await dbf.CreateDbContextAsync(ct);
    var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
    var visible=scope.Devices(db.Devices.AsNoTracking());
    return await db.NetworkAlerts.AsNoTracking().Where(x=>x.DeviceId.HasValue && visible.Any(d=>d.Id==x.DeviceId.Value) ||
        scope.AllLocations && !x.DeviceId.HasValue).OrderByDescending(x=>x.LastSeenUtc).Take(150).ToListAsync(ct);
});
app.MapEnterpriseApis(identityEnabled:!demo);
api.MapGet("/radio-policy",()=>new {approvedWidthsMhz=NetworkChangePolicy.ApprovedWidthsMhz,
    permittedNonDfsChannels5Ghz=NetworkChangePolicy.PreferredNonDfs5GhzChannels,
    excludedDfsChannels5Ghz=NetworkChangePolicy.ExcludedDfs5GhzChannels});
// The app's own CSP already sends frame-ancestors 'none'; suppress Blazor's default second CSP header ('self').
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(o=>o.ContentSecurityFrameAncestorsPolicy=null).RequireAuthorization("Viewer");
await DemoSeed.InitialiseAsync(app.Services,demo);
if(!demo) await LocalIdentityBootstrap.InitialiseAsync(app.Services,app.Configuration);
app.Run();

static Task ApiAwareRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> ctx,int status)
{
    if(ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode=status;
    else ctx.Response.Redirect(ctx.RedirectUri);
    return Task.CompletedTask;
}

public partial class Program { } // integration-test WebApplicationFactory entry point
