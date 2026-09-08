# CHECKPOINT — DesiLudo3D persistent progress record

> Per owner build directive §12: never rely on conversational memory for progress.
> This file is updated at every meaningful milestone. Last validated state = resume point.

| Field | Value |
|---|---|
| **Updated (UTC)** | 2026-09-09 (M2 authoring pass complete) |
| **Current milestone** | M2 — Identity layer: photo import, medallions, profiles (AUTHORED; editor validation pending) |
| **Current task** | M2 code complete & statically checked (61/61 Python green, sync in sync). Next: M3 comedy & game feel — ReactionDirector, R1 clips, styled UI EN+HI, save/resume wiring, premium look pass. M1/M2 editor validation runs in parallel on the owner's machine via `docs/UNITY-VALIDATION.md`. |
| **Spec status** | 🔒 LOCKED — `docs/MASTER-SPEC.md` v1.1 (D1–D14 + amendments A1–A5) |

## Milestone status

### M0 — rules engine: ✅ COMPLETE (validated)
- Python reference engine: **61/61 tests green** (rules suite 57 + board-map suite 4 new)
- 14 conformance vectors self-verified (80 captures, 9 D2 voids, 71 finishes, 6 full matches)
- CLI runner: 31 matches replay-verified across all configs
- Canonical C# `src/DesiLudo3D.Rules`: **AUTHORED** — compile+vector validation needs any .NET SDK 8+ machine: `dotnet run --project tests/DesiLudo3D.Rules.Tests`

### M1 — Unity foundation + gray-box: 🟡 AUTHORED (editor validation pending)
| Component | Status |
|---|---|
| `unity/` project (manifest URP 17.0.3/InputSystem/ugui, ProjectVersion 6000.0.32f1) | ✅ Authored (JSON-validated) |
| `DesiLudo3D.Core` (pure, no UnityEngine): MatchRunner (turn flow + save/load with hash-checked replay), provider seams (IDiceProvider, IOpponentAgent, IFaceReactionProvider, INetTransport, IProfileStore), SeededRandomDice, FixedSequenceDice, RandomAgent placeholder, BoardGridMap (mirror of Python-validated table) | ✅ Authored + statically checked |
| `DesiLudo3D.Gameplay`: GameBootstrap (procedural scene), BoardBuilder (gray-box board from BoardGridMap), TokenView (hop/squash-stretch/capture-flight animations), DiceView (tumble), CameraRig (adaptive portrait/landscape fit + pass-and-play seat rotation), HudView (in-code uGUI), PassAndPlayController (event→animation pipeline, humans + placeholder bots, restart), GrayBoxInput (compiles under any input handling) | ✅ Authored + statically checked |
| `DesiLudo3D.Editor` ProjectSetupWizard (`Tools > DesiLudo3D`: PlayerSettings for Android API26/IL2CPP/ARM64/GLES3/both orientations, build target switch, Main scene creation, URP asset assignment) | ✅ Authored + statically checked |
| Rules sync `tools/sync_rules_to_unity.py` (hash-verified, `--check`) | ✅ VALIDATED (in sync: 5 files) |
| Board map: Python self-check caught the classic track's 4 diagonal corner hops; table asserted & mirrored | ✅ VALIDATED (Python) / Authored (C# mirror) |
| **Owner editor validation** | ⏳ PENDING — checklist in `docs/UNITY-VALIDATION.md` (~5 min setup + 9 acceptance tests) |


### M2 — Identity layer: 🟡 AUTHORED (editor validation pending)
| Component | Status |
|---|---|
| `Core/Identity.cs` — pure `PlayerIdentity` + `CharacterCategory` (Male/Female/Child) + `PersonalityStyle` (5 styles) | ✅ Authored |
| `Gameplay/Identity/PhotoImporter.cs` — `IPhotoImporter` seam: NativeGallery (Android device, `NATIVE_GALLERY` define), Editor file panel, Stub | ✅ Authored |
| `Gameplay/Identity/PhotoProcessor.cs` — centre-crop, bilinear 256px, soft-oval alpha mask, PNG persist/load | ✅ Authored |
| `Gameplay/Identity/ProfileService.cs` — `FileProfileStore : IProfileStore` + `PlayerProfileData` JSON + `ProfileService` (load/save/set-photo/clear-photo) | ✅ Authored |
| `Gameplay/Identity/IdentityCenter.cs` — gray-box per-seat cards (VOICE cycle, STYLE cycle, PICK/REPLACE/CLEAR photo), live medallion application, persisted-photo restore | ✅ Authored |
| `TokenView.SetFaceTexture` — camera-facing transparent medallion quad above every token of the seat | ✅ Authored |
| HudView PROFILES button + GameBootstrap wiring (ProfileService/PhotoImporters.Create/IdentityCenter) | ✅ Authored |
| `manifest.json` += `com.yasirkula.nativegallery` (git URL); Gameplay asmdef += `NativeGallery.Runtime` ref + `NATIVE_GALLERY` versionDefine | ✅ Authored (JSON-validated) |
| **Validation** | Python 61/61 ✅ · rules sync IN SYNC ✅ · 8 C# files statically balanced ✅ · owner editor checklist extended (tests 10–16) ⏳ |

## Remaining (milestone order)

- [ ] **Owner validations (parallel, non-blocking):** ① `dotnet run --project tests/DesiLudo3D.Rules.Tests` (C# M0 closure) ② Unity first-open checklist (`docs/UNITY-VALIDATION.md`, M1 closure)
- [ ] **Owner validations (parallel, non-blocking):** ① `dotnet run --project tests/DesiLudo3D.Rules.Tests` ② Unity first-open checklist — now covers M1 tests 1–9 + M2 tests 10–16 (NativeGallery package auto-resolves on first open; internet needed once)
- [ ] **M3** comedy & game feel: ReactionDirector (event pools × male/female/child, cooldowns, history, context), R1 clip import+playback, capture choreography polish, VFX, sound, styled UI (EN+HI), save/resume wiring, look-dev premium pass
- [ ] **M4** bots (Easy/Medium/Hard), tutorial, dialogue expansion (150+), self-recorded VO (male/female/child profiles)
- [ ] **M5** NetTransport + LAN play ($0) + EOS-vs-Nakama evaluation → D6 final
- [ ] **M6** internet play: room codes, commit–reveal over the wire, reconnect, profile/clip sync
- [ ] **M7** hardening: device lab, thermal/battery, AAB ≤80MB, Play compliance (API 36, Data Safety: no camera permission), closed testing

## Files changed this stage (M1)

- New: `unity/` — Packages/manifest.json, ProjectSettings/ProjectVersion.txt, Assets/_Core/{Rules (synced), Core, Gameplay, Editor} (18 C# files + 4 asmdefs)
- New: `tools/sync_rules_to_unity.py`, `tools/reference-engine/ludo/boardmap.py`, `tests/test_boardmap.py`, `docs/UNITY-VALIDATION.md`
- Updated: `docs/IMPLEMENTATION-LOG.md` (IL-016…IL-022), `CHECKPOINT.md`, `README.md`

## Tests (last full sweep)

- Python suite: **61/61 OK** (added board-map self-check + spot checks)
- Structural checks: all 24 C# files balanced; all asmdefs + manifest JSON valid
- Rules↔Unity sync: IN SYNC (5 files)
- C# compile: pending owner .NET SDK machine; Unity editor validation: pending owner checklist

## Known issues / environment constraints

1. No C# toolchain / Unity Editor in this sandbox (IL-001, R12) — dual-track mitigations in place; both owner validation steps are single commands/checklists.
2. C# `MatchRunner`/Gameplay code authored against Unity 6 APIs from reference — first-open compile slip possible; `docs/UNITY-VALIDATION.md` instructs capturing any console error for a repo fix rather than hand-editing.
3. Unity 6000.0.32f1 authored; any 6000.0.x+ opens the project (upgrade prompt OK).

## Next action

Proceed to **M2 (identity layer)**: Android Photo Picker integration (native picker via Unity's `NativeGallery`-free approach — evaluate Unity 6's built-in `FileBrowser`/`WebGLFileBrowser` alternatives; likely a small Java plugin or Unity 6's `AndroidPhotoPicker` API if available), oval crop + masking to texture, token photo medallion, profile model with character type + voice profile (A2/A5), replace/delete controls, default-token skip path. Any native-plugin work will be authored with compile-time `#if UNITY_ANDROID` guards.

## Commit state

- Branch: `arena/01a08213-desiludo3d`
- Last commits: `60639db` (spec lock) → `a0789dc` (M0) → M1 commit (this stage)
