# DesiLudo3D

**Premium stylized 3D Indian Ludo for Android** — real player faces on 3D tokens, original spoken Hinglish comedy (male/female/child character voices), offline pass-and-play + bots, and friends-only online multiplayer in V1. Built at **$0 service cost**.

- 🔒 **Authoritative build contract:** [`docs/MASTER-SPEC.md`](docs/MASTER-SPEC.md) (v1.1 — LOCKED; decisions D1–D14 + owner amendments A1–A5)
- 📌 **Live progress:** [`CHECKPOINT.md`](CHECKPOINT.md)
- 🧾 **Autonomous implementation decisions:** [`docs/IMPLEMENTATION-LOG.md`](docs/IMPLEMENTATION-LOG.md)
- 📐 **Rules/state/event text formats:** [`docs/RULES-FORMAT.md`](docs/RULES-FORMAT.md)

## Repository layout

```
docs/                    Master spec, implementation log, formats, concept art, Unity validation guide
tools/reference-engine/  Python reference rules engine (validation oracle — 61 tests)
tools/sync_rules_to_unity.py  Hash-verified rules sync into the Unity project
tests/conformance-vectors/    Cross-implementation conformance vectors (C# must match)
src/                     Canonical C# libraries (DesiLudo3D.Rules — Unity-ready, netstandard2.1)
tests/                   C# test suites (consume the shared vectors)
unity/                   Unity 6 + URP project (M1: gray-box pass-and-play; see docs/UNITY-VALIDATION.md)
  Assets/_Core/Rules/    canonical rules (SYNCED COPY — edit src/ only)
  Assets/_Core/Core/     pure orchestration: MatchRunner, provider seams, board map
  Assets/_Core/Gameplay/ Unity presentation (bootstrap, board, tokens, dice, camera, HUD)
  Assets/_Core/Editor/   one-click project setup wizard
```

## Running the M0 reference tests (no dependencies — Python 3 stdlib only)

```bash
python3 -m unittest discover -s tools/reference-engine/tests -v
```

## Status

See `CHECKPOINT.md` — milestone-based progress (M0 → M7), AI-assisted development per spec §15.
