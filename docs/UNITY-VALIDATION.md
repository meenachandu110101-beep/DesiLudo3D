# Unity Validation Guide (M1 — first open)

> The AI build environment cannot run the Unity Editor (spec risk R12).
> Everything under `unity/` is **AUTHORED + statically checked** (structure,
> APIs, JSON) but not editor-validated. This is the exact checklist to
> validate M1 on a machine with Unity 6 (6000.0.x LTS or newer). Report any
> step that fails back into the repo issue tracker so it can be fixed —
> nothing here is expected to fail silently.

## One-time setup (~5 minutes)

1. **Open the project**: Unity Hub → Add project from disk → select `unity/`.
   Editor version: 6000.0.32f1 was authored; any 6000.0.x+ works (accept the
   upgrade prompt if offered). First import generates `Library/` and `.meta`
   files (git-ignored, as designed).

2. **If the Package Manager shows a resolve warning** (exact package versions
   vary by editor patch): accept Unity's suggestion to *use the recommended
   versions*. The manifest pins versions known for 6000.0 (URP 17.0.3,
   InputSystem 1.11.2, uGUI 2.0.0) — all free, no paid packages.

3. **Run the setup wizard** (menu: `Tools > DesiLudo3D`):
   - **1. Configure Player Settings** — Android (min API 26, IL2CPP, ARM64,
     GLES3), both orientations (D12), product name.
   - **2. Switch Build Target to Android** (optional now; required before
     first device build).
   - **3. Create Main Scene** — creates `Assets/Scenes/Main.unity` with the
     `GameBootstrap` object and registers it in Build Settings.
   - **4. Assign URP Asset** — if no URP asset exists yet, the console will
     tell you how to create one (right-click → Create → Rendering → URP Asset
     with Universal Renderer), then re-run step 4. Until then the gray-box
     renders on the Built-in pipeline automatically (materials pick the right
     shader at runtime — by design).

4. **Press Play.** You should see:
   - a cream 15×15 Ludo board on a wooden frame, four coloured base yards,
     coloured runways, gold star markers on safe cells, a centre finish area
   - 4 token capsules per seat (with head placeholder spheres — the M2 photo
     medallion goes there)
   - a die cube to the right, a ROLL button, a turn banner
   - the camera looking at **Red's corner** (bottom of screen)

## What to test (the M1 acceptance list)

| # | Test | Expected |
|---|---|---|
| 1 | Tap ROLL repeatedly | Die tumbles; 6 releases a token (arc hop onto the start cell) and grants an extra roll |
| 2 | Roll 1–5 with everything in base | "no legal move" toast, turn passes; camera rotates to next player's corner |
| 3 | Move onto an opponent (non-safe cell) | Capture: victim spins home in a high arc, attacker keeps rolling (D3) |
| 4 | Land on a gold star cell with an opponent on it | NO capture — coexist |
| 5 | Roll three 6s in a row | "turn is VOID" — the whole turn's progress is undone (D2) |
| 6 | Bot seats (set e.g. Red+Yellow = bot on the GameBootstrap inspector) | Bots roll and move automatically ~1s apart |
| 7 | Rotate device / resize Game view portrait↔landscape | Board always fully fits (D12 adaptive framing) |
| 8 | Play to the end | Standings panel; PLAY AGAIN restarts with a fresh seed |
| 9 | Toggle `blockades` off in the inspector | Own pairs no longer block opponents |

## Known boundaries (by design, not bugs)

- **Gray-box visuals** — primitives + flat colours. The premium festival look
  (D7) is the M3 art pass; the *structure* validated here is what persists.
- **HUD is legacy uGUI built in code** — M3 swaps in the styled UI (TMP,
  truck-art bubbles, EN/HI string tables) behind the same `HudView` surface.
- **Die value shows in the HUD** (the cube is decorative) — M3 replaces with
  the pip-textured physics die that lands on the predetermined face.
- **Save/resume** — `MatchRunner.Save()/Load()` are implemented and
  round-trip-tested at the rules level; the Unity auto-save wiring is M3.
- **First-compile fixes**: the code was authored against Unity 6 APIs and
  statically checked, but a minor compile slip is always possible on first
  open — if the console shows a compile error, capture it verbatim; it will
  be fixed in the repo (do not hand-edit `unity/Assets/_Core/Rules/` — that
  folder is synced from `src/` via `tools/sync_rules_to_unity.py`).

## C# rules validation (no Unity needed, any machine with .NET SDK 8+)

```bash
dotnet run --project tests/DesiLudo3D.Rules.Tests   # must pass all 14 conformance vectors
python3 tools/sync_rules_to_unity.py --check        # must print IN SYNC
```

## Where things live

```
unity/Assets/_Core/Rules/     canonical rules library (SYNCED COPY — do not edit)
unity/Assets/_Core/Core/      pure C# orchestration: MatchRunner, providers, board map (no UnityEngine)
unity/Assets/_Core/Gameplay/  Unity presentation: bootstrap, board, tokens, dice, camera, HUD, controller
unity/Assets/_Core/Editor/    setup wizard (menu tools)
```
