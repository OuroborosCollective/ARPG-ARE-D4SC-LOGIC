"""Exercise real extraction and provenance without executing proposal code."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class OpenRouterAuditTests(unittest.TestCase):
    def test_same_names_keep_distinct_sources_and_exact_utf8_bytes(self):
        """Different exports with the same basename retain unambiguous byte provenance."""
        script = Path(__file__).resolve().parents[2] / 'scripts/imports/audit_openrouter.py'
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            inputs = []
            for index in range(2):
                folder = root / str(index)
                folder.mkdir()
                path = folder / 'export.json'
                data = {'artifactFileContents': {'a': {'content': 'ä\r\nclass Example' + str(index) + ' {}\n'}}}
                path.write_bytes(json.dumps(data, ensure_ascii=False).encode('utf-8'))
                inputs.append(str(path))
            subprocess.run([sys.executable, str(script), *inputs, '--output', str(root/'out')], check=True, capture_output=True)
            manifest = json.loads((root/'out/manifest.json').read_text(encoding='utf-8'))
            ids = [source['source_id'] for source in manifest['sources']]
            self.assertEqual(2, len(set(ids)))
            for record in manifest['records']:
                self.assertIn(record['source'], ids)
                raw = (root/'out'/record['extracted_file']).read_bytes()
                self.assertEqual(record['sha256'], hashlib.sha256(raw).hexdigest())
                self.assertEqual(record['bytes'], len(raw))
                self.assertIn(b'\r\n', raw)

if __name__ == '__main__':
    unittest.main()
