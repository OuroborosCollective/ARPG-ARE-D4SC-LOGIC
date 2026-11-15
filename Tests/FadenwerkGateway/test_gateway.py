import importlib.util
import pathlib
import unittest

source = pathlib.Path(__file__).resolve().parents[2] / 'Tools/Fadenwerk/gateway.py'
spec = importlib.util.spec_from_file_location('gateway', source)
gateway = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gateway)


class RequestPolicyTests(unittest.TestCase):
    def check(self, method='POST', path='/api/quest/save', origin='https://arelogic.space', content_type='application/json', length='2', transfer=None, marker='proxy'):
        return gateway.validate_request(method, path, origin, content_type, length, transfer, marker)

    def test_valid_local_application_requests(self):
        self.assertIsNone(self.check())
        self.assertIsNone(self.check(method='GET', path='/api/state?rev=abc', origin=None, length=None))
        self.assertIsNone(self.check(method='GET', path='/', origin=None, length=None))

    def test_cross_site_and_simple_form_posts_rejected(self):
        for origin in [None, 'null', 'https://evil.example', 'http://arelogic.space']:
            self.assertEqual(403, self.check(origin=origin)[0])
        self.assertEqual(415, self.check(content_type='text/plain')[0])
        self.assertEqual(403, self.check(marker=None)[0])

    def test_bounded_body_and_framing(self):
        for length in ['-1', 'x', None]:
            self.assertEqual(400, self.check(length=length)[0])
        self.assertEqual(413, self.check(length=str(4*1024*1024+1))[0])
        self.assertEqual(400, self.check(transfer='chunked')[0])

    def test_only_known_path_shape_and_methods(self):
        for path in ['http://evil.example/', '//evil.example/', '/api/../secret', '/api/%2e%2e/secret', '/secret', '/api/%2fsecret']:
            self.assertEqual(404, self.check(path=path)[0])
        for method in ['PUT', 'DELETE', 'OPTIONS', 'CONNECT']:
            self.assertEqual(405, self.check(method=method)[0])


if __name__ == '__main__':
    unittest.main()
