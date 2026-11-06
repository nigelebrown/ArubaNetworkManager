using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jcf.NetworkManager.Services;

public static class DemoSeed
{
    public static async Task InitialiseAsync(IServiceProvider services,bool demo)
    {
        await using var scope=services.CreateAsyncScope();
        var factory=scope.ServiceProvider.GetRequiredService<IDbContextFactory<NetworkDbContext>>();
        await using var db=await factory.CreateDbContextAsync();
        // Production always uses SQL Server. A non-relational store can only be substituted by the
        // automated test host, which has no migrations to verify.
        if(demo || !db.Database.IsRelational()) await db.Database.EnsureCreatedAsync();
        else {
            // Database migrations are applied out-of-band by the DBA after a tested backup.
            // Do not automatically execute DDL on an operational SQL Server/AG at application startup.
            var pending=await db.Database.GetPendingMigrationsAsync();
            var applied=await db.Database.GetAppliedMigrationsAsync();
            if(!applied.Any() || pending.Any())
                throw new InvalidOperationException("SQL schema is not at the reviewed migration level. Apply source-controlled migrations before application startup.");
        }
        if(!await db.AnalysisSchedules.AnyAsync()) db.AnalysisSchedules.Add(new AnalysisSchedule {Enabled=false});
        if(demo && !await db.Locations.AnyAsync())
        {
            var area=new Location{Name="Technology Branch",Type="Branch"};
            var lab=new Location{Name="ICT Test Lab",Type="Location",ParentId=area.Id};
            db.Locations.AddRange(area,lab);
            db.Devices.AddRange(
                new Device{Name="Demo AP-505",Kind=DeviceKind.Ap505,ManagementIp="192.0.2.71",LocationId=lab.Id,SecretRef="DEMO_AP",ClusterName="LAB-VC"},
                new Device{Name="Demo CX 6100",Kind=DeviceKind.Cx6100,ManagementIp="198.51.100.21",LocationId=lab.Id,SecretRef="DEMO_CX"});
        }
        await db.SaveChangesAsync();
        if(demo && !await db.DeviceGroups.AnyAsync()) {
            var lab=await db.Locations.FirstAsync(l=>l.Name=="ICT Test Lab");
            var group=new DeviceGroup{Name="ICT Demo Access Points",LocationId=lab.Id,Notes="Administrative location group only"};
            db.DeviceGroups.Add(group);
            var ap=await db.Devices.FirstAsync(d=>d.Kind==DeviceKind.Ap505);
            db.GroupMemberships.Add(new GroupMembership{GroupId=group.Id,DeviceId=ap.Id});
            // DEMO-ONLY simulated certification, never applicable to real hardware.
            ap.Firmware="8.10.0.21";
            db.DeviceCapabilities.AddRange(
                new DeviceCapability{DeviceId=ap.Id,Firmware=ap.Firmware,Operation="ssid.create",State=ApiCapabilityState.WriteVerified,EvidenceReference="DEMO-SIMULATED-ONLY"},
                new DeviceCapability{DeviceId=ap.Id,Firmware=ap.Firmware,Operation="arm.band-steering.write",State=ApiCapabilityState.WriteVerified,EvidenceReference="DEMO-SIMULATED-ONLY"});
        }
        await db.SaveChangesAsync();
    }
}
