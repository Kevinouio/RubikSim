"""Serve only the static site and Unity Web output on loopback. No backend or remote state."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class WebHandler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm',
                      '.data': 'application/octet-stream', '.js': 'application/javascript',
                      '.json': 'application/json', '.css': 'text/css', '.html': 'text/html'}

    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        self.send_header('X-Content-Type-Options', 'nosniff')
        super().end_headers()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8080)
    args = parser.parse_args()
    site = Path(__file__).resolve().parents[1] / 'website'
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(WebHandler, directory=str(site)))
    print(f'RubikSim: http://127.0.0.1:{args.port} (serving {site})', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        server.server_close()
