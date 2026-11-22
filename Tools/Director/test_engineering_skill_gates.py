"""Real filesystem and pure-logic tests; never gameplay or Unity runtime evidence."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from engineering_skill_gates import (
    SOURCE_REVISION, SKILL_PATHS, DEFAULT_EDGES, audit_catalog,
    task_plan, validate_graph, check_authorities, review_gate,
    mutation_gate, lint_wiki_page, build_local_catalog,
)


class EngineeringSkillGatesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)

    def write_catalog(self):
        skills = []
        for name in SKILL_PATHS:
            if name == 'chief-of-staff':
                continue
            path = self.root / 'skills' / name / 'SKILL.md'
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('---\nname: '+name+'\ndescription: test fixture\n---\n\nWorkflow', encoding='utf-8')
            skills.append({'name':name, 'path':str(path), 'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        catalog = self.root / 'engineering-catalog.json'
        catalog.write_text(json.dumps({'schema':'ashen.engineering-skills.v1','source_revision':SOURCE_REVISION,'skills':skills}), encoding='utf-8')
        return catalog

    def test_catalog_reads_real_skills_and_leaves_experimental_disabled(self):
        catalog = self.write_catalog()
        result = audit_catalog(catalog)
        self.assertEqual('NEEDS_REVIEW', result['status'])
        self.assertEqual('EXPERIMENTAL_NOT_EXECUTABLE', result['skills']['chief-of-staff'])
        self.assertEqual('LOCAL_BYTES_MATCH_OTHER_REVISION', result['skills']['code-review'])

    def test_changed_skill_bytes_are_not_trusted(self):
        catalog = self.write_catalog()
        (self.root/'skills/code-review/SKILL.md').write_text('different', encoding='utf-8')
        result = audit_catalog(catalog)
        self.assertEqual('CHANGED', result['skills']['code-review'])
        self.assertNotEqual('UPSTREAM_PIN_MATCH', result['status'])

    def test_catalog_rejects_wrong_pin_and_duplicate_names(self):
        catalog = self.write_catalog()
        payload = json.loads(catalog.read_text())
        payload['source_revision'] = '0'*40
        catalog.write_text(json.dumps(payload))
        self.assertEqual('SOURCE_PIN_MISMATCH', audit_catalog(catalog)['status'])
        payload['source_revision'] = SOURCE_REVISION
        payload['skills'].append(payload['skills'][0])
        catalog.write_text(json.dumps(payload))
        self.assertEqual('INVALID_CATALOG', audit_catalog(catalog)['status'])

    def test_routing_requires_actual_skill_files_not_description(self):
        catalog = self.write_catalog()
        result = task_plan('NPC bug reparieren', catalog)
        self.assertIn('diagnosing-bugs', result['selected'])
        self.assertIn('tdd', result['selected'])
        self.assertIn('code-review', result['selected'])
        self.assertEqual('LOCAL_BYTES_MATCH_OTHER_REVISION', result['skills']['diagnosing-bugs'])
        (self.root/'skills/tdd/SKILL.md').unlink()
        self.assertEqual('MISSING', task_plan('NPC bug reparieren', catalog)['skills']['tdd'])

    def test_missing_catalog_does_not_claim_installed_skills(self):
        result = task_plan('Implement spec', self.root/'nonexistent.json')
        self.assertEqual('CATALOG_MISSING', result['catalog_status'])
        self.assertTrue(all(x != 'UPSTREAM_PIN_MATCH' for x in result['skills'].values()))

    def test_task_dag_has_approval_and_readback_dominators(self):
        result = validate_graph(DEFAULT_EDGES)
        self.assertEqual('VALID', result['status'])
        self.assertTrue(result['approval_mandatory'])
        self.assertTrue(result['evidence_mandatory'])
        self.assertTrue(result['device_optional'])

    def test_bypass_edges_and_cycles_fail_closed(self):
        self.assertEqual('INVALID_GATES', validate_graph(DEFAULT_EDGES + (('Plan','DirectorApply'),))['status'])
        self.assertEqual('CYCLIC', validate_graph(DEFAULT_EDGES + (('Merge','Plan'),))['status'])
        self.assertEqual('INVALID_GATES', validate_graph(DEFAULT_EDGES + (('OptionalDevice','Merge'),))['status'])

    def test_economy_cycles_are_allowed_but_task_cycles_are_not(self):
        economy=(('Baeume','Holzfaeller'),('Holzfaeller','Schreiner'),('Schreiner','Baeume'))
        self.assertEqual('CYCLIC', validate_graph(economy)['status'])
        self.assertEqual('CYCLE_ALLOWED', validate_graph(economy, graph_kind='economy')['status'])

    def test_single_authority_fails_if_second_npc_controller_is_added(self):
        base={'Core':['AreArpg.Core'],'NPC':['ExistingNPCOwner'],'Save':['ExistingSaveOwner'],
              'Editor':['UnityGameDirector'],'Quest':['ExistingQuestOwner']}
        self.assertEqual('VALID', check_authorities(base)['status'])
        base['NPC'].append('CompetingNPCFramework')
        self.assertEqual('DUPLICATE_AUTHORITY', check_authorities(base)['status'])

    def test_review_requires_distinct_results_and_same_sha(self):
        sha='a'*40
        standards={'status':'PASS','revision':sha,'sources':['AGENTS.md'],'report_sha256':'a'*64}
        spec={'status':'PASS','revision':sha,'sources':['docs/SPEC_DIRECTOR_WORKFLOW.md'],'report_sha256':'b'*64}
        self.assertEqual('REVIEW_EVIDENCE_CONSISTENT', review_gate(standards,spec,sha)['status'])
        self.assertEqual('REVIEW_BLOCKED', review_gate(standards,{**spec,'status':'SKIPPED'},sha)['status'])
        self.assertEqual('REVIEW_BLOCKED', review_gate(standards,{**spec,'revision':'b'*40},sha)['status'])
        self.assertEqual('REVIEW_BLOCKED', review_gate(standards,{**spec,'report_sha256':'a'*64},sha)['status'])

    def test_mutation_must_be_nonempty_and_show_real_red_green_exit_codes(self):
        before=b'key = 1\n'; after=b'key = 2\n'
        self.assertEqual('METADATA_CONSISTENT_NOT_RUNTIME',mutation_gate(before,after,1,0)['status'])
        self.assertEqual('MUTATION_NOT_APPLIED',mutation_gate(before,before,1,0)['status'])
        self.assertEqual('RED_NOT_OBSERVED',mutation_gate(before,after,0,0)['status'])
        self.assertEqual('GREEN_NOT_OBSERVED',mutation_gate(before,after,1,1)['status'])

    def test_wiki_requires_dated_sha_bound_claims_and_real_source_reference(self):
        sha='a'*40
        text=f'# Director knowledge\n\n- 2026-11-22 | OuroborosCollective/ARPG-ARE-D4SC-LOGIC@{sha} | Skill gate audited | source=https://github.com/mattpocock/skills/releases/tag/v1.3.2\n'
        self.assertEqual([],lint_wiki_page(text))
        self.assertIn('MISSING_COMMIT',lint_wiki_page(text.replace('@'+sha,'@unknown')))
        self.assertIn('MISSING_SOURCE',lint_wiki_page(text.replace('source=https://','source=unknown')))


    def test_catalog_builder_rejects_absent_skills_without_creating_output(self):
        target = self.root / 'catalog.json'
        with self.assertRaises(ValueError):
            build_local_catalog(self.root / 'skills', target)
        self.assertFalse(target.exists())


if __name__ == '__main__':
    unittest.main()
