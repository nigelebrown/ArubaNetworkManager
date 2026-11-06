using System.Text;
using System.Security.Claims;
using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Jcf.NetworkManager.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Jcf.NetworkManager.Services;

public static class EnterpriseApiEndpoints
{
    public sealed record PlaceDeviceRequest(Guid LocationId);
    public sealed record NewDeviceRequest(string Name,DeviceKind Kind,string ManagementIp,string SecretRef,Guid? LocationId);
    public sealed record NewLocationRequest(string Name,string Type,Guid? ParentId,string? Address);
    public sealed record NewSpaceRequest(Guid LocationId,Guid? ParentSpaceId,string Type,string Name);
    public sealed record NewAccountRequest(string Username,string Password,string DisplayName,string Role);
    public sealed record ChangePasswordRequest(string CurrentPassword,string NewPassword);
    public sealed record SetRoleRequest(string Role);
    public sealed record SubmitProposalRequest(Guid DeviceId,ConfigAction Action,string PayloadJson,string Justification);
    public sealed record ReviewProposalRequest(string Decision);
    public sealed record NewIncidentRequest(Guid? DeviceId,Guid? LocationId,string Title,string Description,string Severity);
    public sealed record UpdateIncidentRequest(string State,string? Owner);
    public sealed record NewTemplateRequest(string Name,string Scope,Guid? LocationId,string NonSecretJson);
    public sealed record ScheduleRequest(bool Enabled,int EveryMinutes);
    public sealed record ProbeTargetRequest(Guid DeviceId);
    public sealed record SiteGrantRequest(Guid UserId,Guid LocationId,string Role);

    public static void MapEnterpriseApis(this WebApplication app,bool identityEnabled=true)
    {
        var read=app.MapGroup("/api/v1").RequireAuthorization("Viewer");

        read.MapGet("/dashboard/summary",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=> {
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return Results.Ok(new {
                devices=await visible.CountAsync(ct),healthy=await visible.CountAsync(x=>x.LastStatus=="Healthy",ct),
                aps=await visible.CountAsync(x=>x.Kind==DeviceKind.Ap505||x.Kind==DeviceKind.Ap535,ct),
                switches=await visible.CountAsync(x=>x.Kind==DeviceKind.Cx6100||x.Kind==DeviceKind.Cx6200,ct),
                pendingRecommendations=await db.Recommendations.CountAsync(x=>(x.State==RecommendationState.Pending) && visible.Any(d=>d.Id==x.DeviceId),ct),
                pendingChanges=await db.ConfigProposals.CountAsync(x=>(x.State==ConfigProposalState.PendingApproval||x.State==ConfigProposalState.PendingSecondApproval) && visible.Any(d=>d.Id==x.DeviceId),ct),
                openIncidents=await db.OperationsIncidents.CountAsync(x=>x.Status=="Open" && (scope.AllLocations ||
                    x.DeviceId.HasValue && visible.Any(d=>d.Id==x.DeviceId.Value) ||
                    x.LocationId.HasValue && scope.LocationIds.Contains(x.LocationId.Value)),ct),
                openCompliance=await db.ComplianceFindings.CountAsync(x=>(x.State=="Open") && visible.Any(d=>d.Id==x.DeviceId),ct),
                updatedUtc=DateTime.UtcNow
            });
        });
        read.MapGet("/physical-spaces",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            return await db.PhysicalSpaces.AsNoTracking().Where(x=>scope.AllLocations || scope.LocationIds.Contains(x.LocationId)).OrderBy(x=>x.Name).Take(1000).ToListAsync(ct);
        });
        read.MapGet("/placements",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            return await db.DevicePlacements.AsNoTracking().Where(x=>scope.AllLocations || scope.LocationIds.Contains(x.LocationId)).Where(x=>x.ThroughUtc==null).Take(2000).ToListAsync(ct);
        });
        read.MapGet("/devices/{id:guid}/members",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            // In demo mode the inventory is synthetic; in production, expose member identifiers
            // only to a global administrator until each real member has a verified physical site.
            if(!(await sites.ForUserAsync(ctx.User,"Read",ct)).AllLocations) return Results.Forbid();
            await sites.RequireDeviceAsync(ctx.User,id,"Read",ct);
            await using var db=await dbf.CreateDbContextAsync(ct);
            return Results.Ok(await db.ClusterMembers.AsNoTracking().Where(x=>x.ConductorDeviceId==id).OrderBy(x=>x.Name).ToListAsync(ct));
        });
        read.MapGet("/devices/{id:guid}/radio-observations",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await sites.RequireDeviceAsync(ctx.User,id,"Read",ct);
            await using var db=await dbf.CreateDbContextAsync(ct);
            return await db.RadioObservations.AsNoTracking().Where(x=>x.DeviceId==id).OrderByDescending(x=>x.AtUtc).Take(250).ToListAsync(ct);
        });
        read.MapGet("/devices/{id:guid}/ports",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await sites.RequireDeviceAsync(ctx.User,id,"Read",ct);
            await using var db=await dbf.CreateDbContextAsync(ct);
            return await db.PortObservations.AsNoTracking().Where(x=>x.DeviceId==id).OrderByDescending(x=>x.AtUtc).Take(500).ToListAsync(ct);
        });
        read.MapGet("/devices/{id:guid}/topology",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await sites.RequireDeviceAsync(ctx.User,id,"Read",ct);
            await using var db=await dbf.CreateDbContextAsync(ct);
            return await db.TopologyLinks.AsNoTracking().Where(x=>x.DeviceId==id).ToListAsync(ct);
        });
        read.MapGet("/clients/sessions",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            // MAC/hash is intentionally withheld from ordinary Viewer role.
            return await db.ClientSessions.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.LastSeenUtc).Take(250)
                .Select(x=>new{x.DeviceId,x.Ssid,x.Band,x.FirstSeenUtc,x.LastSeenUtc,x.RssiDbm,x.SnrDb,x.RetryPct,x.AssociatedVlan,x.Source}).ToListAsync(ct);
        });
        read.MapGet("/compliance/findings",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.ComplianceFindings.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.DetectedUtc).Take(300).ToListAsync(ct);
        });
        read.MapGet("/configuration/snapshots",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.ConfigurationSnapshots.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.CapturedUtc).Take(100)
                .Select(x=>new{x.Id,x.DeviceId,x.Firmware,x.DataSha256,x.CapturedUtc,x.CapturedBy}).ToListAsync(ct);
        });
        read.MapGet("/configuration/templates",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            return await db.PolicyTemplates.AsNoTracking().Where(x=>scope.AllLocations || (x.LocationId.HasValue && scope.LocationIds.Contains(x.LocationId.Value))).OrderBy(x=>x.Name).ToListAsync(ct);
        });
        read.MapGet("/incidents",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.OperationsIncidents.AsNoTracking().Where(x=>scope.AllLocations ||
                x.DeviceId.HasValue && visible.Any(d=>d.Id==x.DeviceId.Value) ||
                x.LocationId.HasValue && scope.LocationIds.Contains(x.LocationId.Value))
                .OrderByDescending(x=>x.OpenedUtc).Take(300).ToListAsync(ct);
        });
        read.MapGet("/security/events",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.SecurityObservations.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.ObservedUtc).Take(300).ToListAsync(ct);
        });
        read.MapGet("/diagnostics",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.ServicePathProbes.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.AtUtc).Take(300).ToListAsync(ct);
        });
        read.MapGet("/telemetry/rollups",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.TelemetryRollups.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.BucketUtc).Take(400).ToListAsync(ct);
        });
        read.MapGet("/ml/shadow-predictions",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.ShadowPredictions.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.AtUtc).Take(100).ToListAsync(ct);
        });
        read.MapGet("/change-proposals",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.ConfigProposals.AsNoTracking().Where(x=>visible.Any(d=>d.Id==x.DeviceId)).OrderByDescending(x=>x.RequestedUtc).Take(200)
                .Select(x=>new {x.Id,x.DeviceId,x.Action,x.State,x.RequestedBy,x.ApprovedBy,x.SecondApprovedBy,x.PayloadSha256,x.LastOutcome,x.RequestedUtc}).ToListAsync(ct);
        });
        read.MapGet("/execution-history",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var visible=scope.Devices(db.Devices.AsNoTracking());
            return await db.ConfigExecutionEvents.AsNoTracking().Where(e=>db.ConfigProposals.Any(p=>p.Id==e.ProposalId && visible.Any(d=>d.Id==p.DeviceId))).OrderByDescending(x=>x.AtUtc).Take(300).ToListAsync(ct);
        });
        read.MapGet("/audit",async(int? take,DateTime? beforeUtc,IDbContextFactory<NetworkDbContext> dbf,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var query=db.AuditEvents.AsNoTracking();
            if(beforeUtc is DateTime before) query=query.Where(x=>x.AtUtc<before);
            return await query.OrderByDescending(x=>x.AtUtc).Take(Math.Clamp(take??200,1,1000)).ToListAsync(ct);
        }).RequireAuthorization("Admin");
        read.MapGet("/reports/device-health.csv",async(IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var scope=await sites.ForUserAsync(ctx.User,"Read",ct);
            var devices=await scope.Devices(db.Devices.AsNoTracking()).OrderBy(x=>x.Name).Take(5000).ToListAsync(ct);
            static string Cell(string? value) {
                var content=value??"";
                if(content.Length>0 && "=+-@\t\r".Contains(content[0])) content="'"+content;
                return "\""+content.Replace("\"","\"\"")+"\"";
            }
            var builder=new StringBuilder("Name,Model,ManagementIP,Location,Status,Firmware,LastSeenUtc\r\n");
            foreach(var d in devices) builder.AppendLine(string.Join(",",new[]{Cell(d.Name),Cell(d.Kind.ToString()),Cell(d.ManagementIp),
                Cell(d.LocationId?.ToString()),Cell(d.LastStatus),Cell(d.Firmware),Cell(d.LastSeenUtc?.ToString("O"))}));
            return Results.File(Encoding.UTF8.GetBytes(builder.ToString()),"text/csv","jcf-device-health.csv");
        });

        var oper=app.MapGroup("/api/v1").RequireAuthorization("Operator");
        oper.MapPost("/devices",async(NewDeviceRequest request,DeviceService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.RegisterAsync(request.Name,request.Kind,request.ManagementIp,request.SecretRef,request.LocationId,Actor(ctx),ct)));
        oper.MapPost("/locations",async(NewLocationRequest request,IDbContextFactory<NetworkDbContext> dbf,SiteAccessService sites,HttpContext ctx,CancellationToken ct)=> {
            if(!(await sites.ForUserAsync(ctx.User,"Operate",ct)).AllLocations) return Results.Forbid();
            if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Length>120)return Results.BadRequest(new {error="Location name is required (max 120)."});
            if(!new[]{"HQ","Portfolio","Area","Branch","Division","Location","Station","Building","Floor","Zone"}.Contains(request.Type))
                return Results.BadRequest(new{error="Invalid location category"});
            await using var db=await dbf.CreateDbContextAsync(ct);
            if(request.ParentId is Guid parent && !await db.Locations.AnyAsync(x=>x.Id==parent,ct))return Results.BadRequest(new{error="Parent location not found"});
            var location=new Location{Name=request.Name.Trim(),Type=request.Type,ParentId=request.ParentId,Address=request.Address};
            db.Locations.Add(location);
            db.AuditEvents.Add(new AuditEvent{Actor=Actor(ctx),Action="Create location",Target=location.Id.ToString(),Outcome=request.Type});
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/locations/{location.Id}",location);
        });
        oper.MapPost("/incidents",async(NewIncidentRequest request,IncidentService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.OpenAsync(request.DeviceId,request.LocationId,request.Title,request.Description,request.Severity,Actor(ctx),ct)));
        oper.MapPost("/incidents/{id:guid}/state",async(Guid id,UpdateIncidentRequest request,IncidentService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.TransitionAsync(id,request.State,request.Owner,Actor(ctx),ct)));
        oper.MapPost("/devices/{id:guid}/probe",async(Guid id,DeviceService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.ProbeAsync(id,Actor(ctx),ct)));
        oper.MapPost("/devices/{id:guid}/placement",async(Guid id,PlaceDeviceRequest request,EnterpriseOperationsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.PlaceAsync(id,request.LocationId,Actor(ctx),ct)));
        oper.MapPost("/physical-spaces",async(NewSpaceRequest request,EnterpriseOperationsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.CreateSpaceAsync(request.LocationId,request.ParentSpaceId,request.Type,request.Name,Actor(ctx),ct)));
        oper.MapPost("/devices/{id:guid}/snapshot",async(Guid id,EnterpriseOperationsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.SnapshotAsync(id,Actor(ctx),ct)));
        oper.MapPost("/analysis-runs",async(AnalysisService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.RunAsync("Manual",Actor(ctx),ct)));
        oper.MapPost("/change-proposals",async(SubmitProposalRequest request,ChangeWorkflowService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.SubmitAsync(request.DeviceId,request.Action,request.PayloadJson,Actor(ctx),request.Justification,ct)));
        oper.MapPost("/diagnostics/device/{id:guid}/tcp/{port:int}",async(Guid id,int port,NetworkDiagnosticsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.CheckRegisteredDeviceAsync(id,port,Actor(ctx),ct)));
        // Read-only fleet policy inspection; same Operator role set as the Operations page.
        oper.MapPost("/compliance/evaluate",async(EnterpriseOperationsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(new {findings=await service.EvaluateComplianceAsync(Actor(ctx),ct)}));
        oper.MapPost("/ml/evaluate-shadow/{id:guid}",async(Guid id,EnterpriseOperationsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.EvaluateShadowRiskAsync(id,ct,Actor(ctx))));

        var approver=app.MapGroup("/api/v1").RequireAuthorization("Approver");
        approver.MapPost("/change-proposals/{id:guid}/decision",async(Guid id,ReviewProposalRequest request,ChangeWorkflowService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.DecideAsync(id,request.Decision,Actor(ctx),true,ct)));
        approver.MapPost("/recommendations/{id:guid}/decision",async(Guid id,ReviewProposalRequest request,AnalysisService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.DecideAsync(id,request.Decision,Actor(ctx),ct)));
        // No generic device-write API. The existing gateway is lab-restricted and available only by distinct explicit workflow.

        // Acceptance uses real SQL Server + a configured physical Aruba pilot device only.
        // It cannot run in demo mode or against an address not explicitly allowlisted.
        var pilot=app.MapGroup("/api/v1/pilot").RequireAuthorization("Admin");
        pilot.MapGet("/readiness",async(IDbContextFactory<NetworkDbContext> dbf,IConfiguration config,CancellationToken ct)=> {
            await using var db=await dbf.CreateDbContextAsync(ct);
            var sql=db.Database.IsSqlServer();
            var reachable=await db.Database.CanConnectAsync(ct);
            var migrations=sql ? (await db.Database.GetAppliedMigrationsAsync(ct)).Count() : 0;
            var pending=sql ? (await db.Database.GetPendingMigrationsAsync(ct)).Count() : -1;
            return Results.Ok(new { sqlServerProvider=sql, sqlReachable=reachable, appliedMigrations=migrations,
                pendingMigrations=pending, schemaManagedByMigrations=migrations>0 && pending==0,
                pilotEnabled=config.GetValue<bool>("Jcf:PilotAcceptance:Enabled"),
                approvedIpCount=(config.GetSection("Jcf:PilotAcceptance:AllowedDeviceIps").Get<string[]>()??[]).Length,
                deviceWritesEnabled=config.GetValue<bool>("Jcf:EnableDeviceWrites"), note="The acceptance endpoint never writes to devices." });
        });
        pilot.MapPost("/devices/{id:guid}/read-acceptance",async(Guid id,HardwareAcceptanceService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(await service.CaptureAsync(id,Actor(ctx),ct)));
        pilot.MapGet("/acceptance-runs/{id:guid}",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,CancellationToken ct)=> {
            await using var db=await dbf.CreateDbContextAsync(ct);
            var run=await db.HardwareAcceptanceRuns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });
        pilot.MapGet("/devices/{id:guid}/last-sample",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,CancellationToken ct)=> {
            await using var db=await dbf.CreateDbContextAsync(ct);
            var snapshot=await db.TelemetrySnapshots.AsNoTracking().Where(x=>x.DeviceId==id)
                .OrderByDescending(x=>x.AtUtc).Select(x=>new {x.Id,x.DeviceId,x.AtUtc,x.ApiConnected,x.ApCount,x.ClientCount,
                    x.VlanCount,x.PortCount,x.RadioChannel,x.ChannelWidthMhz,x.ChannelUtilizationPct,
                    x.NoiseFloorDbm,x.ObservationSource,x.Problem}).FirstOrDefaultAsync(ct);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });

        var admin=app.MapGroup("/api/v1/admin").RequireAuthorization("Admin");
        admin.MapPost("/evaluate-compliance",async(EnterpriseOperationsService service,HttpContext ctx,CancellationToken ct)=>
            Results.Ok(new {findings=await service.EvaluateComplianceAsync(Actor(ctx),ct)}));
        admin.MapPost("/telemetry/rollup",async(EnterpriseOperationsService service,CancellationToken ct)=>
            Results.Ok(new{created=await service.CreateRollupsAsync(ct)}));
        // Local account administration only exists when ASP.NET Core Identity is registered (non-demo).
        if(identityEnabled) {
            admin.MapGet("/users",async(OperatorAccountService accounts,CancellationToken ct)=>Results.Ok(await accounts.ListAsync(ct)));
            admin.MapPost("/users",async(NewAccountRequest request,OperatorAccountService accounts,HttpContext ctx,CancellationToken ct)=>{
                var created=await accounts.CreateAsync(request.Username,request.DisplayName,request.Password,request.Role,Actor(ctx),ct);
                return Results.Created($"/api/v1/admin/users/{created.Id}",created);
            });
            admin.MapPost("/users/{id:guid}/disable",async(Guid id,OperatorAccountService accounts,HttpContext ctx,CancellationToken ct)=>{
                await accounts.SetDisabledAsync(id,true,Actor(ctx),ct);return Results.NoContent();});
            admin.MapPost("/users/{id:guid}/enable",async(Guid id,OperatorAccountService accounts,HttpContext ctx,CancellationToken ct)=>{
                await accounts.SetDisabledAsync(id,false,Actor(ctx),ct);return Results.NoContent();});
            admin.MapPost("/users/{id:guid}/unlock",async(Guid id,OperatorAccountService accounts,HttpContext ctx,CancellationToken ct)=>{
                await accounts.UnlockAsync(id,Actor(ctx),ct);return Results.NoContent();});
            admin.MapPost("/users/{id:guid}/role",async(Guid id,SetRoleRequest request,OperatorAccountService accounts,HttpContext ctx,CancellationToken ct)=>{
                await accounts.SetRoleAsync(id,request.Role,Actor(ctx),ct);return Results.NoContent();});
        }
        admin.MapGet("/site-permissions",async(IDbContextFactory<NetworkDbContext> dbf,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            return await db.SitePermissions.AsNoTracking().OrderBy(x=>x.UserId).Take(5000).ToListAsync(ct);
        });
        admin.MapPost("/site-permissions",async(SiteGrantRequest request,IDbContextFactory<NetworkDbContext> dbf,HttpContext ctx,CancellationToken ct)=>{
            if(!JcfRoles.All.Contains(request.Role,StringComparer.Ordinal))return Results.BadRequest(new{error="Invalid site permission role"});
            await using var db=await dbf.CreateDbContextAsync(ct);
            if(!await db.Users.AnyAsync(u=>u.Id==request.UserId,ct) || !await db.Locations.AnyAsync(l=>l.Id==request.LocationId,ct))
                return Results.BadRequest(new{error="User or location does not exist"});
            if(await db.SitePermissions.AnyAsync(p=>p.UserId==request.UserId && p.LocationId==request.LocationId && p.Role==request.Role,ct))
                return Results.Conflict(new{error="Site permission already exists"});
            var grant=new SitePermission{UserId=request.UserId,LocationId=request.LocationId,Role=request.Role};
            db.SitePermissions.Add(grant);
            db.AuditEvents.Add(new AuditEvent{Actor=Actor(ctx),Action="Grant site permission",Target=grant.Id.ToString(),Outcome=request.Role});
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/admin/site-permissions/{grant.Id}",grant);
        });
        admin.MapDelete("/site-permissions/{id:guid}",async(Guid id,IDbContextFactory<NetworkDbContext> dbf,HttpContext ctx,CancellationToken ct)=>{
            await using var db=await dbf.CreateDbContextAsync(ct);
            var grant=await db.SitePermissions.FindAsync([id],ct);
            if(grant is null)return Results.NotFound();
            db.SitePermissions.Remove(grant);
            db.AuditEvents.Add(new AuditEvent{Actor=Actor(ctx),Action="Revoke site permission",Target=id.ToString(),Outcome=grant.Role});
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        admin.MapGet("/readiness",(IConfiguration configuration)=> Results.Ok(new{
            localIdentity=true,sqlConfigured=!string.IsNullOrWhiteSpace(configuration.GetConnectionString("NetworkManager")),
            labWritesEnabled=configuration.GetValue<bool>("Jcf:LiveLabWrites:Enabled"),
            policyWidthsMhz=NetworkChangePolicy.ApprovedWidthsMhz,
            supportedModels=new[]{"AP-505","AP-535","CX6100","CX6200"},
            certification="Required per firmware/device before any write",updatedUtc=DateTime.UtcNow
        }));
    }

    private static string Actor(HttpContext context)=>context.User.Identity?.Name??throw new UnauthorizedAccessException("Authenticated actor missing");
}
