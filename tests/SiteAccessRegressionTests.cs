using System.Security.Claims;
using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Jcf.NetworkManager.Services;
using Jcf.NetworkManager.Workflow;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Jcf.NetworkManager.Tests;

/// <summary>Security regressions for site-scoped users. Live SQL/Aruba certification is separate.</summary>
public sealed class SiteAccessRegressionTests
{
    sealed class Factory(DbContextOptions<NetworkDbContext> options) : IDbContextFactory<NetworkDbContext>
    {
        public NetworkDbContext CreateDbContext()=>new(options);
        public Task<NetworkDbContext> CreateDbContextAsync(CancellationToken ct=default)=>Task.FromResult(CreateDbContext());
    }
    sealed class NoWriteGateway : IApprovedChangeGateway
    {
        public Task<ChangeExecutionOutcome> ExecuteAsync(Device device,ConfigProposal proposal,CancellationToken ct)
            => throw new InvalidOperationException("No physical device writes in authorization tests.");
    }
    sealed record TestEnvironment(Factory Factory,IConfiguration Config,SiteAccessService Sites,OperatorUser User,
        Location Parent,Location Child,Location Other,Guid AssignedDevice,Guid OtherDevice,Guid UnassignedDevice);
    static async Task<TestEnvironment> SetupAsync(string userRole=JcfRoles.NetworkOperator,string grantRole=JcfRoles.NetworkOperator)
    {
        var opts=new DbContextOptionsBuilder<NetworkDbContext>().UseInMemoryDatabase("site-permissions-"+Guid.NewGuid()).Options;
        var factory=new Factory(opts);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Jcf:DemoMode"]="false",["Jcf:AllowedManagementCidrs:0"]="192.0.2.0/24"
        }).Build();
        var user=new OperatorUser{UserName="site.operator",NormalizedUserName="SITE.OPERATOR",DisplayName="Site Operator"};
        var parent=new Location{Name="Area 1",Type="Area"};
        var child=new Location{Name="Commissioner's Office",Type="Location",ParentId=parent.Id};
        var other=new Location{Name="Area 2",Type="Area"};
        var a=new Device{Name="AP in site",Kind=DeviceKind.Ap505,ManagementIp="192.0.2.71",
            LocationId=child.Id,SecretRef="LAB",Firmware="8.10.0.21"};
        var b=new Device{Name="AP in other site",Kind=DeviceKind.Ap535,ManagementIp="192.0.2.72",
            LocationId=other.Id,SecretRef="LAB",Firmware="8.10.0.21"};
        var unassigned=new Device{Name="AP without location",Kind=DeviceKind.Ap505,
            ManagementIp="192.0.2.73",SecretRef="LAB"};
        await using(var db=await factory.CreateDbContextAsync())
        {
            var role=new IdentityRole<Guid>(userRole){NormalizedName=userRole.ToUpperInvariant()};
            db.Users.Add(user);db.Roles.Add(role);
            db.UserRoles.Add(new IdentityUserRole<Guid>{UserId=user.Id,RoleId=role.Id});
            db.Locations.AddRange(parent,child,other);
            db.Devices.AddRange(a,b,unassigned);
            db.SitePermissions.Add(new SitePermission{UserId=user.Id,LocationId=parent.Id,Role=grantRole});
            db.TelemetrySnapshots.AddRange(
                new TelemetrySnapshot{DeviceId=a.Id,ApiConnected=true,ArmJson="{\"80mhz-support\":\"enabled\"}"},
                new TelemetrySnapshot{DeviceId=b.Id,ApiConnected=true,ArmJson="{\"80mhz-support\":\"enabled\"}"});
            await db.SaveChangesAsync();
        }
        return new(factory,config,new SiteAccessService(factory,config),user,parent,child,other,a.Id,b.Id,unassigned.Id);
    }
    static ClaimsPrincipal Principal(OperatorUser user,string role)=>new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.UserName!),
        new Claim(ClaimTypes.Role,role)
    },"Test"));

    [Fact]
    public async Task Parent_grant_includes_child_but_not_unrelated_site_or_unassigned_devices()
    {
        var e=await SetupAsync();
        var scope=await e.Sites.ForUserAsync(Principal(e.User,JcfRoles.NetworkOperator));
        Assert.False(scope.AllLocations);
        Assert.True(scope.Contains(e.Parent.Id));
        Assert.True(scope.Contains(e.Child.Id));
        Assert.False(scope.Contains(e.Other.Id));
        Assert.False(scope.Contains(null));
        await using var db=await e.Factory.CreateDbContextAsync();
        var visible=await scope.Devices(db.Devices.AsNoTracking()).Select(d=>d.Id).ToListAsync();
        Assert.Equal(new[]{e.AssignedDevice},visible);
    }

    [Fact]
    public async Task Unauthorized_actor_cannot_probe_or_submit_changes_to_other_site()
    {
        var e=await SetupAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>e.Sites.RequireActorDeviceAsync(e.User.UserName!,e.OtherDevice));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>e.Sites.RequireActorDeviceAsync(e.User.UserName!,e.UnassignedDevice));
        await e.Sites.RequireActorDeviceAsync(e.User.UserName!,e.AssignedDevice);
        var workflows=new ChangeWorkflowService(e.Factory,new NoWriteGateway(),e.Config,e.Sites);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>workflows.SubmitAsync(e.OtherDevice,
            ConfigAction.ChangeBandSteering,"{\"value\":\"balance-bands\"}",e.User.UserName!,"Unauthorized target"));
    }

    [Fact]
    public async Task Read_only_site_grant_does_not_confer_operate_or_approve_rights()
    {
        var e=await SetupAsync(grantRole:JcfRoles.Viewer);
        Assert.True((await e.Sites.ForActorAsync(e.User.UserName!,"Read")).Contains(e.Child.Id));
        Assert.False((await e.Sites.ForActorAsync(e.User.UserName!,"Operate")).Contains(e.Child.Id));
        Assert.False((await e.Sites.ForActorAsync(e.User.UserName!,"Approve")).Contains(e.Child.Id));
    }

    [Fact]
    public async Task Analyze_Now_only_creates_recommendations_for_authorized_devices()
    {
        var e=await SetupAsync();
        var analysis=new AnalysisService(e.Factory,e.Sites);
        var run=await analysis.RunAsync("Manual",e.User.UserName!);
        Assert.Equal(1,run.DevicesEvaluated);
        await using var db=await e.Factory.CreateDbContextAsync();
        var recommendations=await db.Recommendations.ToListAsync();
        Assert.NotEmpty(recommendations);
        Assert.All(recommendations,r=>Assert.Equal(e.AssignedDevice,r.DeviceId));
    }

    [Fact]
    public async Task Admin_remains_global_and_missing_user_fails_closed()
    {
        var e=await SetupAsync();
        var unknown=new ClaimsPrincipal(new ClaimsIdentity(new[]{
            new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,JcfRoles.Viewer)
        },"Test"));
        Assert.Empty((await e.Sites.ForUserAsync(unknown)).LocationIds);
        var admin=Principal(e.User,JcfRoles.PlatformAdmin);
        Assert.True((await e.Sites.ForUserAsync(admin)).AllLocations);
    }
}
