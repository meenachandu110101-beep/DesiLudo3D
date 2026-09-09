#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate unity/Assets/_Core/Core/Comedy/ComedyPools.cs from the curated
Python comedy library (tools/reference-engine/ludo/comedy.py).

Same protocol as the rules sync (IL-014/IL-020): the Python module is the
curated source of truth (its data integrity is covered by tests/test_comedy.py);
this generator emits the C# data table; `--check` verifies the checked-in C#
is up to date (CI-able). Never hand-edit the generated file.
"""

import argparse
import hashlib
import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "reference-engine"))

from ludo.comedy import CATEGORIES, CONTEXTS, EMOTIONS, POOLS  # noqa: E402

OUT_PATH = os.path.join(ROOT, "unity", "Assets", "_Core", "Core", "Comedy",
                        "ComedyPools.cs")

PERSONALITY_CS = {
    "balanced": "PersonalityStyle.Balanced",
    "overconfident": "PersonalityStyle.Overconfident",
    "drama": "PersonalityStyle.DramaKing",
    "sweet": "PersonalityStyle.SweetButDeadly",
    "excited": "PersonalityStyle.Excited",
}


def esc(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"')


def ctx_enum_name(ctx: str) -> str:
    parts = ctx.split("_")
    return "".join(p.capitalize() for p in parts)


def generate() -> str:
    lines = []
    a = lines.append
    a("// AUTO-GENERATED from tools/reference-engine/ludo/comedy.py by")
    a("// tools/generate_comedy_pools.py — DO NOT EDIT BY HAND.")
    a("// Regenerate with: python3 tools/generate_comedy_pools.py")
    a("// Data integrity is enforced by tools/reference-engine/tests/test_comedy.py.")
    a("")
    a("namespace DesiLudo3D.Core")
    a("{")
    a("    /// <summary>Comedy reaction contexts (canonical order — mirrored")
    a("    /// from ludo/comedy.py CONTEXTS).</summary>")
    a("    public enum ComedyContext")
    a("    {")
    for i, ctx in enumerate(CONTEXTS):
        sep = "," if i < len(CONTEXTS) - 1 else ""
        a("        %s = %d%s" % (ctx_enum_name(ctx), i, sep))
    a("    }")
    a("")
    a("    /// <summary>One dialogue line: EN (romanized Hinglish) + HI")
    a("    /// (Devanagari) text and optional personality affinities")
    a("    /// (empty = any personality — A3/A4).</summary>")
    a("    public sealed class ComedyLine")
    a("    {")
    a("        public readonly string Id;")
    a("        public readonly string En;")
    a("        public readonly string Hi;")
    a("        public readonly PersonalityStyle[] Personalities;")
    a("")
    a("        public ComedyLine(string id, string en, string hi,")
    a("                          PersonalityStyle[] personalities)")
    a("        {")
    a("            Id = id; En = en; Hi = hi; Personalities = personalities;")
    a("        }")
    a("    }")
    a("")
    a("    /// <summary>The dialogue library (generated data). Selection")
    a("    /// logic lives in ComedyDirector; event mapping in ComedyEvents.</summary>")
    a("    public static class ComedyPools")
    a("    {")
    a("        public const int ContextCount = %d;" % len(CONTEXTS))
    a("        public const int CategoryCount = %d;" % len(CATEGORIES))
    a("")
    a("        /// <summary>Context names in ComedyContext order (lowercase,")
    a("        /// mirrors the Python context strings).</summary>")
    a("        public static readonly string[] ContextNames =")
    a("        {")
    a("            " + ", ".join('"%s"' % c for c in CONTEXTS) + ",")
    a("        };")
    a("")
    a("        /// <summary>R1 emotion tags per context (prerecorded clip")
    a("        /// lookup — never camera, Amendment A1).</summary>")
    a("        public static readonly string[] Emotions =")
    a("        {")
    a("            " + ", ".join('"%s"' % EMOTIONS[c] for c in CONTEXTS) + ",")
    a("        };")
    a("")
    a("        /// <summary>Pools[(int)context][(int)category] — never empty")
    a("        /// (asserted at generation time by the Python tests).</summary>")
    a("        public static readonly ComedyLine[][][] Pools = BuildPools();")
    a("")
    a("        static ComedyLine[][][] BuildPools()")
    a("        {")
    a("            var p = new ComedyLine[%d][][];" % len(CONTEXTS))
    for ci, ctx in enumerate(CONTEXTS):
        a("            p[%d] = new ComedyLine[%d][]" % (ci, len(CATEGORIES)))
        a("            {")
        for ki, cat in enumerate(CATEGORIES):
            pool = POOLS[(ctx, cat)]
            a("                new ComedyLine[]",)
            a("                {")
            for line in pool:
                if line.personalities:
                    tags = ", ".join(PERSONALITY_CS[t] for t in line.personalities)
                    pers = "new[] { %s }" % tags
                else:
                    pers = "System.Array.Empty<PersonalityStyle>()"
                a('                    new ComedyLine("%s", "%s", "%s", %s),'
                  % (line.id, esc(line.en), esc(line.hi), pers))
            a("                },")
        a("            };")
    a("            return p;")
    a("        }")
    a("    }")
    a("}")
    return "\n".join(lines) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="verify the generated file is up to date")
    args = ap.parse_args()

    content = generate()
    digest = hashlib.sha256(content.encode("utf-8")).hexdigest()

    if args.check:
        if not os.path.exists(OUT_PATH):
            print("STALE: %s does not exist" % OUT_PATH)
            return 1
        current = open(OUT_PATH, encoding="utf-8").read()
        if hashlib.sha256(current.encode("utf-8")).hexdigest() == digest:
            print("IN SYNC: ComedyPools.cs")
            return 0
        print("STALE: ComedyPools.cs differs from the curated Python data")
        print("       regenerate with: python3 tools/generate_comedy_pools.py")
        return 1

    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write(content)
    total = sum(len(POOLS[(c, k)]) for c in CONTEXTS for k in CATEGORIES)
    print("GENERATED: %s (%d lines, sha256 %s)"
          % (OUT_PATH, total, digest[:12]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
