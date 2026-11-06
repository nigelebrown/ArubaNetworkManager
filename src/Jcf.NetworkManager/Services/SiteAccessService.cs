using System.Security.Claims;
using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jcf.NetworkManager.Services;

/// <summary>
/// Fail-closed site authorization. A grant applies to the named location and its descendants,
/// but NEVER to another location just because devices share an Instant VC cluster.
/// Null location/unknown identity is inaccessible to non-admin accounts.
/// </summary>
public sealed class SiteAccessService(IDbContextFactory<NetworkDbContext> factory,IConfiguration config)
{
    public sealed record Scope(bool AllLocations, Guid[] LocationIds)
    {
        public bool Contains(Guid? location) => AllLocations || location.HasValue && LocationIds.Contains(location.Value);
        public IQueryable<Device> Devices(IQueryable<Device> query) => AllLocations ? query :
            query.Where(d => d.LocationId.HasValue && LocationIds.Contains(d.LocationId.Value));
        public IQueryable<Location> Locations(IQueryable<Location> query) => AllLocations ? query :
            query.Where(l => LocationIds.Contains(l.Id));
    }

    public static bool RoleGrants(string grantRole,string operation) => operation switch
    {
        "Read" => JcfRoles.All.Contains(grantRole,StringComparer.Ordinal),
        "Operate" => JcfRoles.Operators.Contains(grantRole,StringComparer.Ordinal),
        "Approve" => JcfRoles.Approvers.Contains(grantRole,StringComparer.Ordinal),
        _ => false
    };

    public async Task<Scope> ForUserAsync(ClaimsPrincipal principal,string operation="Read",CancellationToken ct=default)
    {
        if(config.GetValue<bool>("Jcf:DemoMode")) return new(true,[]);
        if(principal.Identity?.IsAuthenticated!=true) return new(false,[]);
        if(JcfRoles.IsAdmin(principal)) return new(true,[]);
        if(operation=="Operate" && !JcfRoles.IsOperator(principal) ||
           operation=="Approve" && !JcfRoles.IsApprover(principal)) return new(false,[]);
        if(!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier),out var userId)) return new(false,[]);
        return await ForUserIdAsync(userId,operation,ct);
    }

    public async Task<Scope> ForActorAsync(string actor,string operation="Read",CancellationToken ct=default)
    {
        if(config.GetValue<bool>("Jcf:DemoMode")) return new(true,[]);
        if(string.IsNullOrWhiteSpace(actor)) return new(false,[]);
        await using var db=await factory.CreateDbContextAsync(ct);
        // Operational identities cannot be obtained from caller-controlled REST request fields.
        var normalized=actor.ToUpperInvariant();
        var user=await db.Users.AsNoTracking().Where(u=>u.NormalizedUserName==normalized && !u.IsDisabled)
            .Select(u=>new {u.Id}).FirstOrDefaultAsync(ct);
        if(user is null) return new(false,[]);
        var roles=await (from ur in db.UserRoles.AsNoTracking()
                         join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id
                         where ur.UserId==user.Id select role.Name).ToListAsync(ct);
        if(roles.Contains(JcfRoles.PlatformAdmin,StringComparer.Ordinal)) return new(true,[]);
        var validRole=operation switch
        {
            "Operate"=>roles.Any(r=>r is not null && JcfRoles.Operators.Contains(r,StringComparer.Ordinal)),
            "Approve"=>roles.Any(r=>r is not null && JcfRoles.Approvers.Contains(r,StringComparer.Ordinal)),
            _=>roles.Any(r=>r is not null && JcfRoles.All.Contains(r,StringComparer.Ordinal))
        };
        if(!validRole) return new(false,[]);
        return await ForUserIdAsync(user.Id,operation,ct);
    }

    private async Task<Scope> ForUserIdAsync(Guid userId,string operation,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        var grants=await db.SitePermissions.AsNoTracking().Where(p=>p.UserId==userId).ToListAsync(ct);
        var roots=grants.Where(p=>RoleGrants(p.Role,operation)).Select(p=>p.LocationId).ToHashSet();
        if(roots.Count==0)return new(false,[]);
        var places=await db.Locations.AsNoTracking().Select(p=>new{p.Id,p.ParentId}).ToListAsync(ct);
        // Bounded expansion covers descendant locations without elevating to unrelated sites.
        var allowed=new HashSet<Guid>(roots);
        for(int pass=0;pass<places.Count;pass++)
        {
            int before=allowed.Count;
            foreach(var p in places)
                if(p.ParentId is Guid parent && allowed.Contains(parent))allowed.Add(p.Id);
            if(before==allowed.Count)break;
        }
        // Drop stale grants to deleted locations, rather than inventing visibility.
        var existing=places.Select(p=>p.Id).ToHashSet();
        allowed.IntersectWith(existing);
        return new(false,allowed.ToArray());
    }

    public async Task RequireDeviceAsync(ClaimsPrincipal principal,Guid deviceId,string operation="Read",CancellationToken ct=default)
    {
        var scope=await ForUserAsync(principal,operation,ct);
        await using var db=await factory.CreateDbContextAsync(ct);
        var loc=await db.Devices.AsNoTracking().Where(d=>d.Id==deviceId).Select(d=>d.LocationId).FirstOrDefaultAsync(ct);
        if(!scope.Contains(loc))throw new UnauthorizedAccessException("Device is outside assigned site permissions.");
    }
    public async Task RequireActorDeviceAsync(string actor,Guid deviceId,string operation="Operate",CancellationToken ct=default)
    {
        var scope=await ForActorAsync(actor,operation,ct);
        await using var db=await factory.CreateDbContextAsync(ct);
        var loc=await db.Devices.AsNoTracking().Where(d=>d.Id==deviceId).Select(d=>d.LocationId).FirstOrDefaultAsync(ct);
        if(!scope.Contains(loc))throw new UnauthorizedAccessException("Device is outside assigned site permissions.");
    }
    public async Task RequireActorLocationAsync(string actor,Guid? locationId,string operation="Operate",CancellationToken ct=default)
    {
        var scope=await ForActorAsync(actor,operation,ct);
        if(!scope.Contains(locationId))throw new UnauthorizedAccessException("Location is outside assigned site permissions.");
    }
}
