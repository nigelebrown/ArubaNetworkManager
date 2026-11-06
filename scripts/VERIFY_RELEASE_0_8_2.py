#!/usr/bin/env python3
"""Offline source-level regression assertions. NOT a substitute for dotnet build/test or real SQL/Aruba checks."""
from pathlib import Path
import json
import unittest
ROOT=Path(__file__).resolve().parent.parent
SRC=ROOT/'src'/'Jcf.NetworkManager'
def code(path):return (SRC/path).read_text(encoding='utf-8')
class SecuritySourceChecks(unittest.TestCase):
    def test_site_access_is_fail_closed(self):
        s=code('Services/SiteAccessService.cs')
        for term in ['ForUserAsync','ForActorAsync','RoleGrants','SitePermissions','LocationIds.Contains','!scope.Contains(loc)','RequireActorDeviceAsync']:
            self.assertIn(term,s)
    def test_descendant_expansion_does_not_bypass_unassigned(self):
        s=code('Services/SiteAccessService.cs')
        self.assertIn('ParentId is Guid parent && allowed.Contains(parent)',s)
        self.assertIn('location.HasValue && LocationIds.Contains(location.Value)',s)
    def test_api_reads_filter_by_site(self):
        p=code('Program.cs')
        for endpoint in ['/devices','/locations','/clusters','/recommendations','/latest-samples','/capabilities','/groups','/alerts']:
            self.assertIn('api.MapGet("'+endpoint+'"',p)
        self.assertGreaterEqual(p.count('sites.ForUserAsync(ctx.User'),8)
        e=code('Services/EnterpriseApiEndpoints.cs')
        self.assertGreaterEqual(e.count('sites.ForUserAsync(ctx.User'),15)
    def test_no_global_audit_for_site_viewers(self):
        self.assertIn('RequireAuthorization("Admin")',code('Services/EnterpriseApiEndpoints.cs'))
        self.assertIn('Authorize(Roles="PlatformAdmin")',code('Components/Pages/Audit.razor'))
    def test_cross_location_mutations_guarded(self):
        for p,term in [
            ('Services/DeviceService.cs','RequireActorLocationAsync'),
            ('Workflow/ChangeWorkflowService.cs','RequireActorDeviceAsync'),
            ('Services/IncidentService.cs','RequireActorDeviceAsync'),
            ('Services/EnterpriseOperationsService.cs','RequireActorLocationAsync'),
            ('Services/NetworkDiagnosticsService.cs','RequireActorDeviceAsync')]:
            self.assertIn(term,code(p),p)
    def test_operator_may_analyze_only_own_scope(self):
        self.assertIn('scope?.Devices(db.Devices.AsNoTracking())',code('Services/AnalysisService.cs'))
        self.assertIn('scope?.Devices(db.Devices.AsNoTracking())',code('Services/EnterpriseOperationsService.cs'))
    def test_workers_have_separate_call_not_actor_name_bypass(self):
        self.assertIn('ProbeAsWorkerAsync',code('Services/Workers.cs'))
        self.assertIn('ProbeAsWorkerAsync',code('Services/DeviceService.cs'))
        self.assertNotIn('if(actor!="monitoring-worker")',code('Services/DeviceService.cs'))
    def test_cluster_polling_has_unique_unknown_group(self):
        self.assertIn('d.Id.ToString("N"):d.ClusterName',code('Services/Workers.cs'))
        self.assertNotIn('?"unclassified":d.ClusterName',code('Services/Workers.cs'))
    def test_defaults_are_narrow_and_writes_disabled(self):
        c=json.loads((SRC/'appsettings.json').read_text())
        self.assertFalse(c['Jcf']['EnableDeviceWrites'])
        self.assertFalse(c['Jcf']['LiveLabWrites']['Enabled'])
        self.assertFalse(c['Jcf']['PilotAcceptance']['Enabled'])
        self.assertNotIn('10.0.0.0/8',c['Jcf']['AllowedManagementCidrs'])
        self.assertNotIn('172.16.0.0/12',c['Jcf']['AllowedManagementCidrs'])
        self.assertNotIn('192.168.0.0/16',c['Jcf']['AllowedManagementCidrs'])
    def test_20_40_mhz_and_dfs_controls_remain(self):
        s=code('Workflow/NetworkChangePolicy.cs')
        self.assertIn('ApprovedWidthsMhz',s)
        self.assertIn('ExcludedDfs5GhzChannels',s)
        self.assertIn('20,40',s.replace(' ',''))
    def test_no_lab_credential_in_patch(self):
        for p in SRC.rglob('*.cs'):
            self.assertNotIn('Aruba'+'123',p.read_text(),str(p))
    def test_site_admin_can_manage_grants(self):
        e=code('Services/EnterpriseApiEndpoints.cs')
        self.assertIn('admin.MapPost("/site-permissions"',e)
        self.assertIn('admin.MapDelete("/site-permissions/{id:guid}"',e)
        self.assertIn('Site permissions',code('Components/Pages/Users.razor').replace('Location access grants','Site permissions'))
if __name__=='__main__':unittest.main(verbosity=2)
