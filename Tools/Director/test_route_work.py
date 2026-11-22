import unittest

from route_work import plan


class RouteWorkTests(unittest.TestCase):
    """Deterministic offline planner. No connector is ever claimed as available."""

    def test_every_plan_is_advisory_and_always_covers_discovery_and_evidence(self):
        result = plan('irgendeine Aufgabe')
        self.assertEqual('ugd.route.v1', result['schema'])
        self.assertTrue(result['advisory_only'])
        phases = [p['phase'] for p in result['phases']]
        self.assertIn('discovery', phases)
        self.assertIn('evidence', phases)

    def test_fix_task_runs_implement_with_test_and_review(self):
        phases = [p['phase'] for p in plan('Bug reparieren und fixen')['phases']]
        self.assertIn('implement', phases)
        self.assertIn('test', phases)
        self.assertIn('review', phases)

    def test_android_task_adds_android_and_test(self):
        phases = [p['phase'] for p in plan('android apk build')['phases']]
        self.assertIn('android', phases)
        self.assertIn('test', phases)
        self.assertNotIn('implement', phases)

    def test_phases_follow_the_declared_order(self):
        phases = [p['phase'] for p in plan('feature implementieren und android apk bauen')['phases']]
        order = ['discovery', 'research', 'design', 'implement', 'test', 'android', 'review', 'evidence']
        self.assertEqual(sorted(phases, key=order.index), phases)

    def test_candidate_tools_are_not_verified_unless_explicitly_available(self):
        result = plan('test determinism')
        tools = [t for p in result['phases'] for t in p['candidate_tools']]
        self.assertTrue(tools)
        self.assertTrue(all(t['availability'] == 'NOT_VERIFIED' for t in tools))

    def test_declared_available_tool_is_marked_available_only_there(self):
        result = plan('test determinism', available=['Unity Test Runner', 'GitHub'])
        seen = {t['name']: t['availability'] for p in result['phases'] for t in p['candidate_tools']}
        self.assertEqual('AVAILABLE', seen['Unity Test Runner'])
        self.assertEqual('AVAILABLE', seen['GitHub'])
        self.assertEqual('NOT_VERIFIED', seen['EngineeringSuite TDD'])

    def test_plan_is_deterministic_for_the_same_input(self):
        self.assertEqual(plan('Quest und NPC entwerfen'), plan('Quest und NPC entwerfen'))

    def test_unknown_task_still_returns_a_valid_advisory_plan(self):
        result = plan('xyz völlig unbekannt')
        phases = [p['phase'] for p in result['phases']]
        self.assertEqual(['discovery', 'evidence'], phases)


    def test_engineering_skill_router_is_advisory_and_checks_catalog(self):
        result = plan('NPC bug reparieren')
        self.assertIn('engineering_skills', result)
        route = result['engineering_skills']
        self.assertTrue(route['advisory_only'])
        self.assertEqual('NONE', route['execution'])
        self.assertIn('diagnosing-bugs', route['selected'])
        self.assertNotIn('chief-of-staff', route['selected'])
        # With a pinned host catalog installed this is UPSTREAM_PIN_MATCH;
        # without one, fail closed. Neither state claims runtime execution.
        self.assertIn(route['skills']['diagnosing-bugs'],
                      ('UPSTREAM_PIN_MATCH', 'NOT_VERIFIED', 'MISSING', 'CHANGED',
                       'LOCAL_BYTES_MATCH_OTHER_REVISION'))
        self.assertEqual('d1478fcb140db0b93c0c8df17bcfd311844749d0', route['pin'])


if __name__ == '__main__':
    unittest.main()
