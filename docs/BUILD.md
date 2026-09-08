# Building & Testing DesiLudo3D (M0 state)

## Python reference engine — runs anywhere (stdlib only, no dependencies)

```bash
# full unit suite (57 tests)
python3 -m unittest discover -s tools/reference-engine/tests -v

# headless match runner (deterministic random matches + replay verification)
python3 tools/reference-engine/cli.py --players 4 --seed 1 --games 25

# regenerate conformance vectors (after any intentional rule change)
python3 tools/reference-engine/generate_vectors.py
```

## Canonical C# library (`src/DesiLudo3D.Rules`)

- `netstandard2.1`, **zero NuGet dependencies**, C# 8 — drops into Unity 6 as a
  source/assembly dependency and onto any future .NET server.
- **Status: AUTHORED — compile + vector validation requires a .NET SDK**
  (blocked in the AI build environment; see IL-001). On a machine with the
  .NET SDK (8.0+):

```bash
dotnet run --project tests/DesiLudo3D.Rules.Tests
```

This replays every conformance vector in `tests/conformance-vectors/` and
fails on any divergence (event, state text, or hash) from the validated
reference, then runs embedded unit tests and a simulation smoke.

## Changing the rules (locked change protocol)

1. Change the Python reference + its hand-written tests first.
2. Re-run the Python suite to green.
3. Regenerate vectors (`generate_vectors.py`).
4. Mirror the change in C# and re-run the C# vector suite.
5. Both green = the change is conformance-locked. Update
   `docs/IMPLEMENTATION-LOG.md` with a new IL entry.
