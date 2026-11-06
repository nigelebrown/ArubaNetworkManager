import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

path=Path(__file__).resolve().parents[1] / 'ai-reviewer' / 'reviewer.py'
spec=importlib.util.spec_from_file_location('reviewer',path)
agent=importlib.util.module_from_spec(spec)
spec.loader.exec_module(agent)

class SafetyTests(unittest.TestCase):
    def test_no_lan_url(self):
        self.assertEqual(agent.OLLAMA,'http://ollama:11434/api/chat')
    def test_sensitive_values_redacted(self):
        x=agent.redact('password=abcXYZ123 token=qwerty 10.14.5.20')
        for secret in ('abcXYZ123','qwerty','10.14.5.20'):
            self.assertNotIn(secret,x)
    def test_no_source_executors(self):
        s=path.read_text()
        for bad in ('import subprocess','os.system(', 'docker.sock'):
            self.assertNotIn(bad,s)
    def test_ignores_non_source_and_symlinks(self):
        with tempfile.TemporaryDirectory() as t:
            base=Path(t)
            (base/'a.cs').write_text('public class A {}')
            (base/'.env').write_text('secret')
            (base/'b.log').write_text('private')
            with patch.object(agent,'SEARCH',(base,)):
                self.assertEqual([x.name for x in agent.eligible_files()], ['a.cs'])

if __name__=='__main__':
    unittest.main(verbosity=2)
