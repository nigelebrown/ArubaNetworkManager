#!/usr/bin/env python3
"""Export only sanitized build errors to the isolated reviewer; NEVER raw device logs."""
from pathlib import Path
import re
import sys
FILTERS = [
    (r'(?i)(password|secret|token|api[_-]?key)(\s*[:=]\s*)([^\s;,]+)', r'\1\2[REDACTED]'),
    (r'(?i)bearer\s+[A-Za-z0-9._=-]+', 'Bearer [REDACTED]'),
    (r'\b(?:10|127|192\.168|172\.(?:1[6-9]|2\d|3[01]))(?:\.\d{1,3}){2,3}\b', '[PRIVATE_IP]'),
    (r'\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b', '[EMAIL]')
]
if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit('Usage: sanitize_log.py BUILDLOG OUTPUT')
    src, dest = Path(sys.argv[1]), Path(sys.argv[2])
    lines = src.read_text(encoding="utf-8", errors="replace")[-40000:].splitlines()
    filtered = "\n".join(x for x in lines if re.search(r'(?i)(error |error:|failed|exception|warning NU|\bCS\d{4}\b)', x))
    for p, r in FILTERS:
        filtered = re.sub(p, r, filtered)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(filtered[:10000], encoding="utf-8")
