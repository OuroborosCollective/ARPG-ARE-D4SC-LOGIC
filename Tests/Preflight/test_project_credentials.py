"""Public Unity settings must not contain platform signing or account credentials."""
from pathlib import Path
import re
import unittest


class ProjectCredentialTests(unittest.TestCase):
    def test_platform_credentials_are_empty_in_public_settings(self):
        text = Path('ProjectSettings/ProjectSettings.asset').read_text()
        for key in ('ps4Passcode', 'ps4NPTitleSecret', 'metroCertificatePassword',
                    'AndroidKeystorePass', 'AndroidKeyaliasPass', 'iosAppleDeveloperTeamID'):
            match = re.search(r'^\s*' + re.escape(key) + r':([^\n]*)$', text, re.MULTILINE)
            if match:
                self.assertIn(match.group(1).strip(), ('', '""', "''"), key + ' must remain private')


if __name__ == '__main__':
    unittest.main()
