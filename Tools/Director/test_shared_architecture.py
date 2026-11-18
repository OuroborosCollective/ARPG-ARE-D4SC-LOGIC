"""Behavioral tests with clearly synthetic advisor inputs, not live AI or Unity."""
import json
import tempfile
import unittest
from pathlib import Path

from shared_architecture import prepare_council, compile_council, ArchitectureError


class SharedArchitectureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.doc = self.root / 'ARCHITECTURE.md'
        self.doc.write_text('# Ashen Crypt - common architecture\n\n'
                            '<!-- ashen.shared-architecture.v1 -->\n\n'
                            'Only a synthetic test fixture, never production evidence.\n',
                            encoding='utf-8')
        self.catalog = self.root / 'neural_catalog.json'
        self.catalog.write_text(json.dumps({
            'schema': 'ashen.director.neural-catalog.v1',
            'unity': '6000.6.8f1',
            'departments': [
                {'id': f'dep_{i:02d}', 'title': f'Test advisor {i}', 'triggers': ['test'],
                 'skills': [f'skill-{i}']} for i in range(28)]},
            ensure_ascii=False), encoding='utf-8')
        self.revision = 'a' * 40

    def freeze(self):
        return prepare_council(self.doc, self.catalog, self.revision, '6000.6.8f1')

    def responses(self, frozen):
        answers = []
        for index, packet in enumerate(frozen['packets']):
            answer = {
                'department': packet['department'],
                'snapshot_id': packet['snapshot_id'],
                'status': 'ABSTAIN',
            }
            if index < 3:
                answer = {
                    'department': packet['department'],
                    'snapshot_id': packet['snapshot_id'],
                    'status': 'PROPOSE',
                    'proposal': {
                        'track': ['gameplay', 'story', 'world'][index],
                        'title': f'Synthetic direction {index}',
                        'reason': 'Synthetic justification used only by a unit test',
                        'skills': [f'skill-{index}'],
                        'sources': [
                            'https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/60',
                        ],
                    },
                }
            answers.append(answer)
        return answers

    def test_repository_architecture_and_catalog_share_a_real_snapshot(self):
        repo_root = Path(__file__).resolve().parents[2]
        architecture = repo_root / 'docs/ARCHITECTURE.md'
        catalog = Path(__file__).with_name('neural_catalog.json')
        actual = prepare_council(architecture, catalog, 'a' * 40, '6000.6.8f1')
        self.assertEqual(28, len(actual['packets']))
        self.assertEqual(1, actual['packets'][0]['architecture_markdown'].count(
            '<!-- ashen.shared-architecture.v1 -->'))
        self.assertFalse(actual['source_revision_verified'])
        self.assertEqual('NOT_SENT', actual['dispatch'])

    def test_all_28_receive_identical_snapshot_bytes(self):
        frozen = self.freeze()
        self.assertEqual(28, len(frozen['packets']))
        self.assertEqual(1, len({p['snapshot_id'] for p in frozen['packets']}))
        self.assertEqual(1, len({p['architecture_markdown'] for p in frozen['packets']}))
        self.assertFalse(frozen['source_revision_verified'])
        self.assertEqual('NOT_SENT', frozen['dispatch'])
        self.assertEqual('PROPOSAL_ONLY', frozen['effect'])

    def test_unmodified_snapshot_is_deterministic(self):
        a, b = self.freeze(), self.freeze()
        self.assertEqual(a, b)
        self.assertRegex(a['snapshot_id'], r'^[a-f0-9]{64}$')

    def test_old_snapshot_fails_after_doc_change(self):
        frozen = self.freeze()
        self.doc.write_text(self.doc.read_text() + '\nchanged\n', encoding='utf-8')
        with self.assertRaisesRegex(ArchitectureError, 'ARCHITECTURE_DIGEST_MISMATCH'):
            prepare_council(self.doc, self.catalog, self.revision, '6000.6.8f1',
                            expected_doc_sha256=frozen['architecture_sha256'])

    def test_old_catalog_fails_after_change(self):
        frozen = self.freeze()
        self.catalog.write_text(self.catalog.read_text() + '\n', encoding='utf-8')
        with self.assertRaisesRegex(ArchitectureError, 'CATALOG_DIGEST_MISMATCH'):
            prepare_council(self.doc, self.catalog, self.revision, '6000.6.8f1',
                            expected_catalog_sha256=frozen['catalog_sha256'])

    def test_unity_version_drift_is_refused(self):
        with self.assertRaisesRegex(ArchitectureError, 'UNITY_PIN_MISMATCH'):
            prepare_council(self.doc, self.catalog, self.revision, '2022.3.62f1')

    def test_all_advisors_respond_before_three_paths(self):
        frozen = self.freeze()
        result = compile_council(frozen, self.responses(frozen))
        self.assertEqual('THREE_PATHS_READY_FOR_DIRECTOR_REVIEW', result['status'])
        self.assertEqual(28, result['consulted'])
        self.assertEqual(['gameplay', 'story', 'world'],
                         [p['track'] for p in result['paths']])
        self.assertEqual('NONE', result['execution'])
        self.assertEqual('NOT_EXECUTED', result['runtime_evidence'])
        self.assertFalse(result['source_revision_verified'])

    def test_missing_advisor_fails_closed(self):
        frozen = self.freeze()
        with self.assertRaisesRegex(ArchitectureError, 'INCOMPLETE_COUNCIL'):
            compile_council(frozen, self.responses(frozen)[:-1])

    def test_duplicate_advisor_fails_closed(self):
        frozen = self.freeze()
        answers = self.responses(frozen)
        answers[1] = answers[0].copy()
        with self.assertRaisesRegex(ArchitectureError, 'UNKNOWN_OR_DUPLICATE_ADVISOR'):
            compile_council(frozen, answers)

    def test_old_advisor_snapshot_is_rejected(self):
        frozen = self.freeze()
        answers = self.responses(frozen)
        answers[0]['snapshot_id'] = '0' * 64
        with self.assertRaisesRegex(ArchitectureError, 'STALE_ADVISOR_RESPONSE'):
            compile_council(frozen, answers)

    def test_packet_tampering_is_rejected(self):
        frozen = self.freeze()
        answers = self.responses(frozen)
        frozen['packets'][0]['architecture_markdown'] += '\ninjected\n'
        with self.assertRaisesRegex(ArchitectureError, 'TAMPERED_COUNCIL_PACKET'):
            compile_council(frozen, answers)

    def test_unapproved_skill_cannot_be_smuggled(self):
        frozen = self.freeze()
        answers = self.responses(frozen)
        answers[0]['proposal']['skills'] = ['another-capability']
        with self.assertRaisesRegex(ArchitectureError, 'UNTRUSTED_SKILL'):
            compile_council(frozen, answers)

    def test_executable_command_field_cannot_be_smuggled(self):
        frozen = self.freeze()
        answers = self.responses(frozen)
        answers[0]['command'] = 'run-anything'
        with self.assertRaisesRegex(ArchitectureError, 'INVALID_ADVISOR_FIELDS'):
            compile_council(frozen, answers)

    def test_does_not_invent_third_distinct_path(self):
        frozen = self.freeze()
        answers = self.responses(frozen)
        answers[2] = {'department': answers[2]['department'],
                      'snapshot_id': answers[2]['snapshot_id'], 'status': 'ABSTAIN'}
        result = compile_council(frozen, answers)
        self.assertEqual('INSUFFICIENT_DISTINCT_PATHS', result['status'])
        self.assertEqual([], result['paths'])

    def test_duplicate_catalog_id_is_rejected(self):
        data = json.loads(self.catalog.read_text())
        data['departments'][-1]['id'] = data['departments'][0]['id']
        self.catalog.write_text(json.dumps(data))
        with self.assertRaisesRegex(ArchitectureError, 'INVALID_DEPARTMENT'):
            self.freeze()


if __name__ == '__main__':
    unittest.main()
