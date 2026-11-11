"""Public Unity settings must not contain platform signing or account credentials."""
from pathlib import Path
import re
import unittest
from unittest.mock import patch


class ProjectCredentialTests(unittest.TestCase):
    def test_duplicate_credential_cannot_hide_behind_empty_entry(self):
        with patch.object(Path, 'read_text', return_value='  ps4Passcode:\n  ps4Passcode: test-only-sensitive-value\n'):
            with self.assertRaises(AssertionError):
                self.test_platform_credentials_are_empty_in_public_settings()

    def test_rejected_value_is_not_in_assertion_message(self):
        with patch.object(Path, 'read_text', return_value='  ps4Passcode: test-only-sensitive-value\n'):
            try:
                self.test_platform_credentials_are_empty_in_public_settings()
            except AssertionError as error:
                self.assertNotIn('test-only-sensitive-value', str(error))
            else:
                self.fail('Nonempty credential accepted')

    def test_platform_credentials_are_empty_in_public_settings(self):
        text = Path('ProjectSettings/ProjectSettings.asset').read_text()
        for key in ('ps4Passcode', 'ps4NPTitleSecret', 'metroCertificatePassword',
                    'AndroidKeystorePass', 'AndroidKeyaliasPass', 'iosAppleDeveloperTeamID'):
            matches = re.finditer(r'^[ \t]*' + re.escape(key) + r':([^\n]*)$', text, re.MULTILINE)
            for match in matches:
                self.assertTrue(match.group(1).strip() in ('', '""', "''"), key + ' must remain private')


if __name__ == '__main__':
    unittest.main()
