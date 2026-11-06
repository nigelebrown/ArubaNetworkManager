using Jcf.NetworkManager.Data;
using Jcf.NetworkManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jcf.NetworkManager.Services;

public sealed class PollingWorker(IServiceScopeFactory scopes,IConfiguration config,ILogger<PollingWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Demo mode never polls real hardware. Actual device writes are not part of this worker.
        if(config.GetValue<bool>("Jcf:DemoMode")) return;
        var seconds=Math.Clamp(config.GetValue<int>("Jcf:PollIntervalSeconds",120),60,3600);
        var maxConcurrent=Math.Clamp(config.GetValue<int>("Jcf:MaxConcurrentClusterPolls",4),1,8);
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        using var concurrency=new SemaphoreSlim(maxConcurrent,maxConcurrent);
        while(await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                List<(Guid Id,string Key)> inventory;
                using(var scope=scopes.CreateScope())
                {
                    var factory=scope.ServiceProvider.GetRequiredService<IDbContextFactory<NetworkDbContext>>();
                    await using var db=await factory.CreateDbContextAsync(ct);
                    var rows=await db.Devices.AsNoTracking().Where(d=>d.Enabled)
                        .Select(d=>new{d.Id,d.Kind,d.ClusterName}).ToListAsync(ct);
                    inventory=rows.Select(d=>(d.Id,
                        d.Kind is DeviceKind.Ap505 or DeviceKind.Ap535
                            ? "instant:"+(string.IsNullOrWhiteSpace(d.ClusterName)?d.Id.ToString("N"):d.ClusterName)
                            : "cx:"+d.Id.ToString("N"))).ToList();
                }
                var groups=inventory.GroupBy(x=>x.Key).ToList();
                // Each VC cluster is polled serially. Different confirmed clusters and
                // independent switches may run concurrently within a small fleet-wide cap.
                var tasks=groups.Select(async group=>
                {
                    await concurrency.WaitAsync(ct);
                    try
                    {
                        using var deviceScope=scopes.CreateScope();
                        var probe=deviceScope.ServiceProvider.GetRequiredService<DeviceService>();
                        foreach(var item in group)
                        {
                            ct.ThrowIfCancellationRequested();
                            try {await probe.ProbeAsWorkerAsync(item.Id,ct);}
                            catch(Exception ex) when(ex is not OperationCanceledException)
                            {log.LogWarning("Probe {DeviceId} failed: {Kind}",item.Id,ex.GetType().Name);}
                            await Task.Delay(TimeSpan.FromMilliseconds(300),ct);
                        }
                    }
                    finally {concurrency.Release();}
                }).ToArray();
                await Task.WhenAll(tasks);
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {log.LogError("Polling cycle failed: {Kind}",ex.GetType().Name);}
        }
    }
}

public sealed class HourlyAnalysisWorker(IServiceScopeFactory scopes,ILogger<HourlyAnalysisWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1));
        while(await timer.WaitForNextTickAsync(ct))
        {
            using var scope=scopes.CreateScope();
            try
            {
                var factory=scope.ServiceProvider.GetRequiredService<IDbContextFactory<NetworkDbContext>>();
                await using var db=await factory.CreateDbContextAsync(ct);
                var schedule=await db.AnalysisSchedules.FirstOrDefaultAsync(ct);
                if(schedule is null || !schedule.Enabled || schedule.LastRunUtc is not null &&
                   DateTime.UtcNow - schedule.LastRunUtc < TimeSpan.FromMinutes(Math.Max(60,schedule.EveryMinutes))) continue;
                // Record due time first to prevent duplicate scheduled analyses in this single-node pilot.
                schedule.LastRunUtc=DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                var analysis=scope.ServiceProvider.GetRequiredService<AnalysisService>();
                await analysis.RunAsync("Hourly","analysis-worker",ct);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {log.LogError("Hourly analysis failed: {Kind}",ex.GetType().Name);}
        }
    }
}

/// <summary>Never blindly resume a possibly completed REST write after an app crash.</summary>
public sealed class ExecutionRecoveryWorker(IServiceScopeFactory scopes,ILogger<ExecutionRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Give SQL/EF initializer a moment to complete startup, then reconcile stranded writes.
        await Task.Delay(TimeSpan.FromSeconds(3),ct);
        using var scope=scopes.CreateScope();
        try {
            var factory=scope.ServiceProvider.GetRequiredService<IDbContextFactory<NetworkDbContext>>();
            await using var db=await factory.CreateDbContextAsync(ct);
            var stranded=await db.ConfigProposals.Where(p=>p.State==Jcf.NetworkManager.Domain.ConfigProposalState.Executing).ToListAsync(ct);
            foreach(var proposal in stranded) {
                proposal.State=Jcf.NetworkManager.Domain.ConfigProposalState.RecoveryRequired;
                proposal.LastOutcome="App restart interrupted execution; verify actual device state manually before retrying.";
                db.ConfigExecutionEvents.Add(new Jcf.NetworkManager.Domain.ConfigExecutionEvent{ProposalId=proposal.Id,
                    Stage="Startup reconciliation",Status="RecoveryRequired",Detail=proposal.LastOutcome});
                db.NetworkAlerts.Add(new Jcf.NetworkManager.Domain.NetworkAlert{DeviceId=proposal.DeviceId,
                    Fingerprint="RESTORE:"+proposal.Id.ToString("N"),Severity="Critical",
                    Category="Interrupted change execution",Detail=proposal.LastOutcome});
            }
            if(stranded.Count>0) await db.SaveChangesAsync(ct);
        } catch(Exception ex) when(ex is not OperationCanceledException) {logger.LogError("Change recovery reconciliation failed: {ExceptionType}",ex.GetType().Name);}
    }
}

/// <summary>Bounded retention and durable snapshot rollup maintenance.</summary>
public sealed class MaintenanceWorker(IServiceScopeFactory scopes,ILogger<MaintenanceWorker> logger) : BackgroundService
{
    internal const int PurgeBatchSize=4000; // below SQL Server's ~5,000-lock escalation threshold

    internal static async Task<long> PurgeAsync<T>(IQueryable<T> expired,CancellationToken ct)
    {
        long total=0;
        int batch;
        do { batch=await expired.Take(PurgeBatchSize).ExecuteDeleteAsync(ct); total+=batch; }
        while(batch==PurgeBatchSize && !ct.IsCancellationRequested);
        return total;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromHours(6));
        while(await timer.WaitForNextTickAsync(stop))
        {
            try
            {
                using var scope=scopes.CreateScope();
                var dbf=scope.ServiceProvider.GetRequiredService<IDbContextFactory<NetworkDbContext>>();
                await using var db=await dbf.CreateDbContextAsync(stop);
                var operations=scope.ServiceProvider.GetRequiredService<EnterpriseOperationsService>();
                await operations.CreateRollupsAsync(stop);
                // Retention uses set-based SQL deletes; the in-memory demo store keeps its small data set.
                if(!db.Database.IsRelational()) continue;
                var threshold=DateTime.UtcNow.AddDays(-30);
                // Set-based SQL deletes on time indexes, in bounded batches (DELETE TOP(n)) so a large backlog never
                // escalates to a table lock or one huge log transaction while the poller is writing.
                var purged=0L;
                purged+=await PurgeAsync(db.TelemetrySnapshots.Where(x=>x.AtUtc<threshold),stop);
                purged+=await PurgeAsync(db.RadioObservations.Where(x=>x.AtUtc<threshold),stop);
                purged+=await PurgeAsync(db.PortObservations.Where(x=>x.AtUtc<threshold),stop);
                purged+=await PurgeAsync(db.ClientSessions.Where(x=>x.LastSeenUtc<threshold),stop);
                purged+=await PurgeAsync(db.ServicePathProbes.Where(x=>x.AtUtc<threshold),stop);
                purged+=await PurgeAsync(db.SecurityObservations.Where(x=>x.ObservedUtc<threshold),stop);
                if(purged>0) logger.LogInformation("Retention purge removed {Rows} rows older than 30 days",purged);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                logger.LogError("Maintenance error {Error}",ex.GetType().Name);
            }
        }
    }
}
