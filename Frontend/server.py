#!/usr/bin/env python3
"""
Minimal SPA (React / Vue / etc.) server.
Serves static files and falls back to index.html for client-side routing.
Optionally proxies /api requests to a backend origin.

Usage:
    python server.py                         # default: dist/ on port 3000
    python server.py --dir build             # custom directory
    python server.py --port 8080             # custom port
    python server.py --api-origin http://127.0.0.1:5093
    python server.py --quiet                 # suppress request logs
"""

import argparse
import http.client
import os
import signal
import sys
from http.server import HTTPServer, SimpleHTTPRequestHandler
from urllib.parse import urlsplit

HOP_BY_HOP_HEADERS = {
    "connection",
    "keep-alive",
    "proxy-authenticate",
    "proxy-authorization",
    "te",
    "trailers",
    "transfer-encoding",
    "upgrade",
}


class SPAHandler(SimpleHTTPRequestHandler):
    """Serve static files; proxy /api requests; SPA fallback for unknown routes."""

    def _is_api_request(self) -> bool:
        return self.path == "/api" or self.path.startswith("/api/")

    def _proxy_api_request(self) -> None:
        target = self.server.api_origin_parts
        if target is None:
            self.send_error(502, "API proxy is disabled. Set --api-origin to enable /api forwarding.")
            return

        base_path = target.path.rstrip("/")
        request_path = self.path if self.path.startswith("/") else f"/{self.path}"
        upstream_path = f"{base_path}{request_path}" if base_path else request_path

        body = None
        content_length_header = self.headers.get("Content-Length")
        if content_length_header:
            content_length = int(content_length_header)
            body = self.rfile.read(content_length)

        forward_headers = {}
        for key, value in self.headers.items():
            lower_key = key.lower()
            if lower_key in HOP_BY_HOP_HEADERS or lower_key == "host":
                continue
            forward_headers[key] = value

        forward_headers["Host"] = target.netloc
        forward_headers["X-Forwarded-For"] = self.client_address[0]
        forward_headers["X-Forwarded-Proto"] = "http"
        forward_headers["X-Forwarded-Host"] = self.headers.get("Host", "")

        connection_cls = (
            http.client.HTTPSConnection
            if target.scheme == "https"
            else http.client.HTTPConnection
        )
        port = target.port or (443 if target.scheme == "https" else 80)
        connection = connection_cls(target.hostname, port, timeout=60)

        try:
            connection.request(self.command, upstream_path, body=body, headers=forward_headers)
            response = connection.getresponse()
            payload = response.read()
        except Exception as exc:
            self.send_error(502, f"Upstream API request failed: {exc}")
            return
        finally:
            connection.close()

        self.send_response(response.status, response.reason)
        for key, value in response.getheaders():
            lower_key = key.lower()
            if lower_key in HOP_BY_HOP_HEADERS or lower_key in {"content-length", "date", "server"}:
                continue
            self.send_header(key, value)
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()

        if self.command != "HEAD" and payload:
            self.wfile.write(payload)

    def do_GET(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return

        # translate_path strips query strings and normalizes the path
        fs_path = self.translate_path(self.path)

        # Serve the file if it exists and is not a directory
        if os.path.isfile(fs_path):
            super().do_GET()
            return

        # SPA fallback: let the client-side router handle the route
        self.path = "/index.html"
        super().do_GET()

    def do_HEAD(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return

        fs_path = self.translate_path(self.path)
        if os.path.isfile(fs_path):
            super().do_HEAD()
            return

        self.path = "/index.html"
        super().do_HEAD()

    def do_POST(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return
        self.send_error(405, "Method Not Allowed")

    def do_PUT(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return
        self.send_error(405, "Method Not Allowed")

    def do_PATCH(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return
        self.send_error(405, "Method Not Allowed")

    def do_DELETE(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return
        self.send_error(405, "Method Not Allowed")

    def do_OPTIONS(self) -> None:
        if self._is_api_request():
            self._proxy_api_request()
            return
        self.send_error(405, "Method Not Allowed")

    def log_message(self, fmt, *args):  # type: ignore[override]
        if not self.server.quiet:
            super().log_message(fmt, *args)


class SPAServer(HTTPServer):
    """HTTPServer subclass that carries extra config."""

    def __init__(self, *args, quiet: bool = False, api_origin: str = "", **kwargs):
        self.quiet = quiet
        self.api_origin = api_origin.rstrip("/")
        self.api_origin_parts = urlsplit(self.api_origin) if self.api_origin else None
        super().__init__(*args, **kwargs)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Minimal SPA static server")
    p.add_argument("--dir", default="dist", help="Directory to serve (default: dist)")
    p.add_argument("--port", default=3000, type=int, help="Port to listen on (default: 3000)")
    p.add_argument("--host", default="0.0.0.0", help="Host to bind (default: 0.0.0.0)")
    p.add_argument(
        "--api-origin",
        default=os.environ.get("API_PROXY_TARGET", ""),
        help="Forward /api/* to this backend origin (example: http://127.0.0.1:5093)",
    )
    p.add_argument("--quiet", action="store_true", help="Suppress per-request logs")
    return p.parse_args()


def main() -> None:
    args = parse_args()

    if not os.path.isdir(args.dir):
        sys.exit(f"Error: directory '{args.dir}' does not exist. Did you run the build step?")

    api_origin = args.api_origin.strip().rstrip("/")
    if api_origin:
        parsed = urlsplit(api_origin)
        if parsed.scheme not in {"http", "https"} or not parsed.netloc:
            sys.exit("Error: --api-origin must be a full URL, e.g. http://127.0.0.1:5093")

    os.chdir(args.dir)

    # Allow rapid restarts without "Address already in use"
    SPAServer.allow_reuse_address = True

    server = SPAServer(
        (args.host, args.port),
        SPAHandler,
        quiet=args.quiet,
        api_origin=api_origin,
    )

    # Graceful shutdown on SIGTERM (e.g. systemd / Docker stop)
    signal.signal(signal.SIGTERM, lambda *_: server.shutdown())

    proxy_info = f" | API proxy -> {api_origin}" if api_origin else " | API proxy disabled"
    print(
        f"Serving '{args.dir}' at http://{args.host}:{args.port}{proxy_info} (Ctrl+C to stop)"
    )

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        print("\nServer stopped.")


if __name__ == "__main__":
    main()
