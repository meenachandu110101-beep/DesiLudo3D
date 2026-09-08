#!/usr/bin/env python3
"""Sync the canonical rules library (src/DesiLudo3D.Rules) into the Unity
project (unity/Assets/_Core/Rules). Unity cannot reference sources outside
Assets, so we copy — and this script FAILS LOUDLY if the copy is stale,
preventing silent drift (IL-014 change protocol: never edit one side alone).

Usage:
  python3 tools/sync_rules_to_unity.py            # sync + verify
  python3 tools/sync_rules_to_unity.py --check    # verify only (CI-style)
"""
from __future__ import annotations

import hashlib
import os
import shutil
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "src", "DesiLudo3D.Rules")
DST = os.path.join(REPO, "unity", "Assets", "_Core", "Rules")

ASMDEF = """{
    "name": "DesiLudo3D.Rules",
    "rootNamespace": "DesiLudo3D.Rules",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
"""


def digest(path: str) -> str:
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main() -> int:
    check_only = "--check" in sys.argv
    os.makedirs(DST, exist_ok=True)

    sources = sorted(f for f in os.listdir(SRC) if f.endswith(".cs"))
    if not sources:
        print("!! no sources found in", SRC)
        return 1

    stale = []
    for name in sources:
        src_file = os.path.join(SRC, name)
        dst_file = os.path.join(DST, name)
        if not os.path.exists(dst_file) or digest(src_file) != digest(dst_file):
            stale.append(name)

    asmdef_path = os.path.join(DST, "DesiLudo3D.Rules.asmdef")
    asmdef_ok = os.path.exists(asmdef_path) and digest(asmdef_path) == hashlib.sha256(
        ASMDEF.encode()).hexdigest()

    dst_files = sorted(f for f in os.listdir(DST) if f.endswith(".cs"))
    orphans = [f for f in dst_files if f not in sources]

    if check_only:
        if stale or orphans or not asmdef_ok:
            print("STALE: missing/diff=%s orphans=%s asmdef_ok=%s" % (stale, orphans, asmdef_ok))
            print("Run: python3 tools/sync_rules_to_unity.py")
            return 1
        print("IN SYNC: %d files" % len(sources))
        return 0

    for name in stale:
        shutil.copy2(os.path.join(SRC, name), os.path.join(DST, name))
        print("synced:", name)
    for name in orphans:
        os.remove(os.path.join(DST, name))
        print("removed orphan:", name)
    if not asmdef_ok:
        with open(asmdef_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(ASMDEF)
        print("wrote asmdef")

    ok = all(digest(os.path.join(SRC, n)) == digest(os.path.join(DST, n)) for n in sources)
    print("VERIFY:", "IN SYNC (%d files)" % len(sources) if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
