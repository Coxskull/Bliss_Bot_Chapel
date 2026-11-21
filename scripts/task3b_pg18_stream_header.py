#!/usr/bin/env python3
"""Validate a PostgreSQL custom-dump stream and strip at most one UTF-8 BOM.

This is only a streaming format adapter. It does not decrypt, log, create
plaintext files, or connect to databases. Reject any other leading bytes.
"""
import shutil
import sys

stream = sys.stdin.buffer
header = stream.read(8)
if header.startswith(b"\xef\xbb\xbfPGDMP"):
    header = header[3:]
elif not header.startswith(b"PGDMP"):
    sys.exit(42)

sys.stdout.buffer.write(header)
shutil.copyfileobj(stream, sys.stdout.buffer, length=1048576)
