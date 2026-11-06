#!/usr/bin/env python3
"""Offline safety tests for one-click CentOS deployment package."""
from pathlib import Path
import subprocess
import unittest
R=Path(__file__).resolve().parents[2]
class InstallerSafety(unittest.TestCase):
    def test_bash_syntax(self):
        for f in (R/'deploy/centos9').glob('*.sh'):
            p=subprocess.run(['bash','-n',str(f)],capture_output=True,text=True)
            self.assertEqual(p.returncode,0,f'{f}: {p.stderr}')
    def test_pilot_database_not_automigrated(self):
        install=(R/'deploy/centos9/ONE_CLICK_INSTALL.sh').read_text()
        self.assertIn('DB_MIGRATION_REVIEWED',install)
        self.assertNotIn('dotnet ef database update',install)
    def test_hardware_writes_blocked(self):
        install=(R/'deploy/centos9/ONE_CLICK_INSTALL.sh').read_text()
        setup=(R/'deploy/centos9/CONFIGURE_SQL_PILOT.sh').read_text()
        self.assertIn('Jcf__EnableDeviceWrites=false',install)
        self.assertIn("'false'",setup)
        self.assertIn('Jcf__LiveLabWrites__Enabled=false',install)
    def test_ai_review_never_uses_jcf_runner(self):
        workflow=(R/'.github/workflows/ai-app-only-review.yml').read_text()
        installer=(R/'deploy/centos9/ONE_CLICK_APP_AI.sh').read_text()
        self.assertIn('runs-on: ubuntu-latest',workflow)
        self.assertIn("permission-profile: ':read-only'",workflow)
        self.assertNotIn('self-hosted',workflow)
        self.assertIn('PrivateNetwork=true',installer)
        self.assertFalse((R/'.github/workflows/centos9-lab-acceptance.yml').exists())
    def test_no_default_password(self):
        scripts='\n'.join(f.read_text() for f in (R/'deploy/centos9').glob('*.sh'))
        self.assertNotIn('Aruba'+'123',scripts)
        self.assertIn("read -r -s",scripts)
    def test_no_public_listeners(self):
        install=(R/'deploy/centos9/ONE_CLICK_INSTALL.sh').read_text()
        self.assertIn('127.0.0.1:',install)
        self.assertNotIn('0.0.0.0:',install)
    def test_source_present(self):
        self.assertTrue((R/'src/Jcf.NetworkManager/Jcf.NetworkManager.csproj').exists())
        self.assertTrue(list((R/'src/Jcf.NetworkManager/Migrations').glob('*InitialProduction.cs')))
if __name__=='__main__': unittest.main(verbosity=2)
