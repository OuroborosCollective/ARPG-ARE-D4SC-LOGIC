#!/usr/bin/env python3
"""Loopback bridge behind the existing HTTPS owner-auth router; no gameplay state.

Never expose this listener publicly. Traefik authenticates the owner before forwarding.
The Editor token is read from a private file and never returned to the browser.
"""
import argparse
import http.client
import http.server
import json
import pathlib
import re
import socket
import urllib.parse

MAX_BODY = 4 * 1024 * 1024
MAX_RESPONSE = 32 * 1024 * 1024
ORIGIN = 'https://arelogic.space'


def validate_request(method, path, origin, content_type, length, transfer, marker):
    if method not in ('GET', 'POST'):
        return 405, 'Methode nicht erlaubt.'
    parsed = urllib.parse.urlsplit(path)
    if parsed.scheme or parsed.netloc or not re.fullmatch(r'/(?:index\.html|favicon\.ico|api/[A-Za-z0-9_/-]+)?', parsed.path):
        return 404, 'Nicht gefunden.'
    if transfer:
        return 400, 'Chunked-Anfragen werden nicht angenommen.'
    if method == 'POST':
        if origin != ORIGIN or marker != 'proxy':
            return 403, 'Öffne Fadenwerk über den geschützten Browser-Link.'
        if (content_type or '').split(';')[0].strip().lower() != 'application/json':
            return 415, 'JSON erforderlich.'
        if length is None or not re.fullmatch(r'[0-9]+', length):
            return 400, 'Ungültige Inhaltslänge.'
        if int(length) > MAX_BODY:
            return 413, 'Anfrage zu groß.'
    return None


class Handler(http.server.BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(20)

    def log_message(self, fmt, *args):
        # Log only status/method, never token-bearing URLs, headers or authoring data.
        pass

    def respond(self, status, body, content_type='application/json; charset=utf-8'):
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'private, no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Referrer-Policy', 'no-referrer')
        self.send_header('X-Frame-Options', 'DENY')
        self.send_header('X-Robots-Tag', 'noindex, nofollow')
        self.send_header('Connection', 'close')
        self.end_headers()
        self.wfile.write(body)

    def error(self, status, message):
        self.respond(status, json.dumps({'ok': False, 'error': message}).encode())

    def handle_request(self):
        if len(self.headers.get_all('Content-Length', [])) > 1:
            self.error(400, 'Mehrdeutige Inhaltslänge.'); return
        failure = validate_request(self.command, self.path, self.headers.get('Origin'),
            self.headers.get('Content-Type'), self.headers.get('Content-Length'),
            self.headers.get('Transfer-Encoding'), self.headers.get('X-Fadenwerk-Token'))
        if failure:
            self.error(*failure); return
        if self.command == 'POST' and not self.path.startswith('/api/'):
            self.error(404, 'Nicht gefunden.'); return
        connection = None
        try:
            body = self.rfile.read(int(self.headers['Content-Length'])) if self.command == 'POST' else None
            if body is not None and len(body) != int(self.headers['Content-Length']):
                self.error(400, 'Unvollständige Anfrage.'); return
            token = self.server.token_path.read_text().strip()
            if not re.fullmatch(r'[A-Za-z0-9_-]{32}', token):
                raise ValueError('Token unavailable')
            connection = http.client.HTTPConnection('127.0.0.1', self.server.editor_port, timeout=45)
            headers = {'X-Fadenwerk-Token': token, 'Content-Type': 'application/json'}
            connection.request(self.command, self.path, body, headers)
            response = connection.getresponse()
            data = response.read(MAX_RESPONSE + 1)
            if len(data) > MAX_RESPONSE:
                self.error(502, 'Unity-Antwort zu groß.'); return
            content_type = response.getheader('Content-Type', 'application/octet-stream')
            if response.status == 200 and self.path.split('?')[0] in ('/', '/index.html'):
                data = data.replace(b'<head>', b'<head><meta name="fadenwerk-owner-proxy" content="true">', 1)
            self.respond(response.status, data, content_type)
        except (OSError, ValueError, http.client.HTTPException, socket.timeout):
            self.error(503, 'Unity ist gerade nicht bereit. Deine ungespeicherten Eingaben bleiben im Browser; gleich erneut versuchen.')
        finally:
            if connection:
                connection.close()

    do_GET = do_POST = do_PUT = do_DELETE = do_OPTIONS = do_HEAD = do_PATCH = handle_request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--token-file', type=pathlib.Path, required=True)
    parser.add_argument('--port', type=int, default=8096)
    parser.add_argument('--editor-port', type=int, default=8095)
    args = parser.parse_args()
    server = http.server.ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    server.token_path, server.editor_port = args.token_file, args.editor_port
    server.serve_forever()


if __name__ == '__main__':
    main()
