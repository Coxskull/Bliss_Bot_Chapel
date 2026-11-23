#!/usr/bin/env python3
"""Task4 no-DB WSL ASCII armor receiver. Never logs decrypted bytes or pg_restore errors."""
import base64
import binascii
import hashlib
import os
import subprocess
import sys

CAP = 64 * 1024 * 1024
PG18 = "/usr/lib/postgresql/18/bin/pg_restore"
PROBE_ONLY = len(sys.argv) == 2 and sys.argv[1] == "--probe-only"
digest = hashlib.sha256()
pending = b""
header = bytearray()
unforwarded = bytearray()
total = 0
pg = None
feed_closed = False
header_invalid = False

def send_pg(data):
    global feed_closed
    if pg is None or feed_closed:
        return
    remaining = memoryview(data)
    while len(remaining) and not feed_closed:
        try:
            sent = pg.stdin.write(remaining)
            if not sent:
                feed_closed = True
            else:
                remaining = remaining[sent:]
        except (BrokenPipeError, OSError, ValueError):
            feed_closed = True

def receive(decoded):
    global total, pg, header_invalid
    total += len(decoded)
    if total > CAP:
        raise ValueError("bounded source exceeded")
    digest.update(decoded)
    if header_invalid:
        return
    if len(header) < 5:
        count = min(5 - len(header), len(decoded))
        header.extend(decoded[:count])
        unforwarded.extend(decoded)
        if len(header) < 5:
            return
        if bytes(header) != b"PGDMP":
            header_invalid = True
            unforwarded.clear()
            return
        if PROBE_ONLY:
            unforwarded.clear()
            return
        pg = subprocess.Popen(
            [PG18, "--list"], stdin=subprocess.PIPE,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            bufsize=0,
        )
        send_pg(unforwarded)
        unforwarded.clear()
    elif not PROBE_ONLY:
        send_pg(decoded)

try:
    if len(sys.argv) > 2 or (len(sys.argv) == 2 and not PROBE_ONLY):
        raise ValueError("unsupported arguments")
    while True:
        block = os.read(3, 65536)
        if not block:
            break
        # Windows may normalize line endings in an ASCII text pipe, not data.
        # Strip only ASCII CR/LF in the Base64 envelope; all other bytes must
        # be a valid Base64 quartet. The decoded payload is never modified.
        clean = pending + block.replace(b"\r", b"").replace(b"\n", b"")
        aligned = (len(clean) // 4) * 4
        if aligned:
            receive(base64.b64decode(clean[:aligned], validate=True))
        pending = clean[aligned:]
    if pending:
        raise ValueError("incomplete base64 quartet")
    if pg is not None:
        try:
            pg.stdin.close()
        except OSError:
            pass
    if len(header) != 5 or header_invalid:
        print("TASK3B_ARMOR_WSL_HEADER=NOT_PGDMP")
        sys.exit(2)
    print("TASK3B_ARMOR_WSL_SHA256=" + digest.hexdigest())
    if PROBE_ONLY:
        print("TASK3B_ARMOR_WSL_PG18=PROBE_ONLY")
        sys.exit(0)
    if pg is None:
        print("TASK3B_ARMOR_WSL_PG18=PG18_UNAVAILABLE")
        sys.exit(2)
    result = pg.wait()
    if result == 0 and not feed_closed:
        print("TASK3B_ARMOR_WSL_PG18=PASS_ARCHIVE_FORMAT")
        sys.exit(0)
    print("TASK3B_ARMOR_WSL_PG18=PG_FORMAT_REFUSED")
    sys.exit(2)
except (ValueError, binascii.Error, OSError, subprocess.SubprocessError):
    print("TASK3B_ARMOR_WSL_HEADER=BRIDGE_REFUSED")
    sys.exit(2)
finally:
    if pg is not None and pg.poll() is None:
        pg.kill()
        pg.wait()
