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
import threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
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
        # Strip query string so "/api?health=1" is correctly identified as an API path.
        clean_path = self.path.split("?", 1)[0]
        return clean_path == "/api" or clean_path.startswith("/api/")

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
            try:
                content_length = int(content_length_header)
                if content_length < 0:
                    raise ValueError("Content-Length must be non-negative")
            except ValueError:
                self.send_error(400, "Invalid Content-Length header")
                return
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
        except (OSError, http.client.HTTPException) as exc:
            # Log full details internally; expose only a generic message to the client
            # so internal hostnames / stack traces are not leaked in the response body.
            self.log_error("Upstream proxy error: %s", exc)
            self.send_error(502, "Upstream API request failed")
            return
        finally:
            connection.close()

        self.send_response(response.status, response.reason)
        upstream_content_length: str | None = None
        for key, value in response.getheaders():
            lower_key = key.lower()
            if lower_key in HOP_BY_HOP_HEADERS or lower_key in {"date", "server"}:
                continue
            if lower_key == "content-length":
                # Capture the upstream value; we will decide below which to emit.
                upstream_content_length = value
                continue
            self.send_header(key, value)
        if self.command == "HEAD":
            # For HEAD the body is always empty (per HTTP spec), so we must preserve
            # the upstream Content-Length so clients learn the true resource size.
            if upstream_content_length is not None:
                self.send_header("Content-Length", upstream_content_length)
        else:
            # For all other methods rewrite with the actual payload length we read.
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

        if os.path.isfile(fs_path):
            super().do_GET()
            return

        # If the request looks like a static asset (has a file extension), return 404
        # so browsers get a proper error instead of HTML masquerading as the asset.
        url_path = self.path.split("?", 1)[0]
        if os.path.splitext(url_path)[1]:
            self.send_error(404, "File not found")
            return

        # SPA fallback: let the client-side router handle extensionless routes.
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

        url_path = self.path.split("?", 1)[0]
        if os.path.splitext(url_path)[1]:
            self.send_error(404, "File not found")
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


class SPAServer(ThreadingHTTPServer):
    """ThreadingHTTPServer subclass that carries extra config."""

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

    # Graceful shutdown on SIGTERM (e.g. systemd / Docker stop).
    # server.shutdown() blocks until serve_forever() returns, so it must run on a
    # different thread than the one running serve_forever() to avoid a deadlock.
    signal.signal(
        signal.SIGTERM,
        lambda *_: threading.Thread(target=server.shutdown, daemon=True).start(),
    )

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
