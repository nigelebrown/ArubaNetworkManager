"""Offline checks for Docker role separation and AI network isolation."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[3]

class DeploymentSafety(unittest.TestCase):
    def test_ai_and_app_networks_separate(self):
        s=(ROOT/'compose.yaml').read_text()
        self.assertIn('internal: true',s)
        self.assertIn('networks: [app_net]',s)
        self.assertIn('networks: [ai_private]',s)
        self.assertIn('127.0.0.1:',s)
        self.assertIn('read_only: true',s)
        self.assertIn('cap_drop: [ALL]',s)
    def test_no_docker_socket_or_privileged(self):
        s=(ROOT/'compose.yaml').read_text()
        self.assertNotIn(':/var/run/docker.sock',s)
        self.assertNotIn('privileged: true',s)
    def test_model_reviewer_has_fixed_local_endpoint(self):
        s=(ROOT/'deploy/docker/ai-reviewer/reviewer.py').read_text()
        self.assertIn('http://ollama:11434/api/chat',s)
        self.assertNotIn('import subprocess',s)
        self.assertNotIn('os.system(',s)
    def test_app_default_device_writes_off(self):
        s=(ROOT/'compose.yaml').read_text()
        self.assertIn("Jcf__EnableDeviceWrites: 'false'",s)
        self.assertIn("Jcf__LiveLabWrites__Enabled: 'false'",s)
    def test_migrations_separate(self):
        s=(ROOT/'INSTALL_DOCKER_CENTOS9.sh').read_text()
        self.assertNotIn('dotnet ef database update',s)
        self.assertIn('docker compose build app',s)
    def test_nonroot_runtime(self):
        self.assertIn('USER $APP_UID',(ROOT/'Dockerfile').read_text())

if __name__=='__main__':
    unittest.main(verbosity=2)
