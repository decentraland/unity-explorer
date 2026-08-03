#!/usr/bin/env python3
"""Accepts TCP connections and never writes a byte.

nginx proxies /stall/* here and holds the client while waiting for a response
that never comes, which is the shape rw_timeout exists to bound: a server that
completes the TLS handshake, takes the request, and then stalls. Without the
timeout that connection owns a playback thread for the rest of the session,
because FFmpeg's interrupt callback only fires between blocking calls.

Connections are kept in a list purely so the sockets are not garbage collected
and closed under us - closing would turn this into an EOF test, not a stall.
"""

import signal
import socket
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8444
HOST = sys.argv[2] if len(sys.argv) > 2 else "127.0.0.1"

held = []


def main() -> None:
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(0))
    listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    listener.bind((HOST, PORT))
    listener.listen(64)
    print(f"uuav-test stall server on {HOST}:{PORT}", flush=True)

    while True:
        try:
            conn, _ = listener.accept()
        except OSError:
            return
        # Drop the oldest holds so a long session cannot exhaust the fd table.
        held.append(conn)
        while len(held) > 256:
            held.pop(0).close()


if __name__ == "__main__":
    main()
