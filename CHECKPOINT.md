# CHECKPOINT — DesiLudo3D persistent progress record

> Per owner build directive §12: never rely on conversational memory for progress.
> This file is updated at every meaningful milestone. Last validated state = resume point.

| Field | Value |
|---|---|
| **Updated (UTC)** | 2026-09-09 (M4 authoring pass complete) |
| **Current milestone** | M4 — Bots, dialogue 150+, VO packs, tutorial: agents VALIDATED / Unity AUTHORED |
| **Current task** | M4 code complete. Next: M5 — NetTransport seam + LAN play ($0) + EOS-vs-Nakama evaluation → D6 final. Owner validation of M1–M4 runs in parallel via `docs/UNITY-VALIDATION.md` (tests 1–29). |
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


### M3 — Comedy & game feel: 🟡 engine VALIDATED / Unity AUTHORED
| Component | Status |
|---|---|
| `ludo/comedy.py` — 94 original Hinglish lines (13 contexts × 3 categories × personality variants, A3 native female/child pools), splitmix64 selector, event mapping | ✅ **VALIDATED** (18 tests; 79/79 total) |
| Personality-bias fix (repeat window inverted bias 25%→correct 46%/16% enrichment) | ✅ **VALIDATED** (found by tests, fixed, re-tested) |
| `tools/generate_comedy_pools.py` → `Core/Comedy/ComedyPools.cs` | ✅ VALIDATED (IN SYNC) |
| `Core/Comedy/` ComedyDirector + ComedyEvents (C# mirrors) | ✅ Authored |
| `Gameplay/Comedy/` UiStrings (EN/HI live toggle), SpeechBubbleView, ComedyPlayback, ISpeechSynthesis + AndroidTtsSpeaker (free system TTS, A4 fallback precedence) | ✅ Authored |
| Save/resume: SaveService (atomic batch autosave) + resume panel + `InitializeWithRunner` | ✅ Authored (round-trip logic VALIDATED at rules level in M0/M1) |
| LookDev (procedural painted cells/wood/gold, gradient sky, URP volume), DiceView pip die (lands on predetermined face), SfxSynth (all SFX synthesized), GameFeel (slow-mo/shake/flash) | ✅ Authored |
| R1 clips: IVideoImporter (system picker, camera-free A1), ReactionClipLibrary (local copies + trim offsets), ClipReactionOverlay + ClipLibraryPanel, ComedyPlayback→overlay hook | ✅ Authored |
| Event streams: EventStarted / EventAnimated / BatchCompleted on PassAndPlayController | ✅ Authored |
| **Validation** | Python 79/79 ✅ · pools IN SYNC ✅ · rules sync IN SYNC ✅ · all new/edited C# brace/paren-balanced ✅ · owner checklist tests 17–24 ⏳ |


### M4 — Bots, dialogue 150+, VO packs, tutorial: 🟡 agents VALIDATED / Unity AUTHORED
| Component | Status |
|---|---|
| `ludo/agent.py` — tiered heuristic agents (Easy/Medium/Hard, threat-aware Hard) | ✅ **VALIDATED** (8 tests; suite 87/87; Hard≥60% over 120 matches) |
| `Core/Agents.cs` — HeuristicAgent C# mirror + MatchSetup.BotTier + controller/bootstrap wiring | ✅ Authored |
| Dialogue library 94 → **152 lines** (13 contexts × 3 categories, A3 native pools) | ✅ **VALIDATED** (integrity tests + pools IN SYNC) |
| VO packs: IAudioImporter (system audio picker, camera-free A1), VoicePackLibrary (local copies + preload), VoicePackPanel; A4 precedence prerecorded VO > TTS > bubble | ✅ Authored |
| TutorialFlow — six bilingual first-launch steps (once-flag) | ✅ Authored |
| **Validation** | Python 87/87 ✅ · comedy pools IN SYNC ✅ · rules sync IN SYNC ✅ · all touched C# balanced ✅ · owner checklist tests 25–29 ⏳ |


### M5 — Online multiplayer (LAN/hotspot/direct IP): 🟡 protocol VALIDATED / Unity AUTHORED
| Component | Status |
|---|---|
| `ludo/netproto.py` — host-authoritative protocol: lobby/room codes, commit–reveal, fair splitmix64 stream, HMAC-signed cmd/state, resync, shadow replay | ✅ **VALIDATED** (11 tests incl. fault injection; suite 98/98) |
| Protocol livelock fix (state messages double as gap detectors) | ✅ **VALIDATED** (found by tests, fixed, re-tested) |
| `Core/Net/NetSession.cs` — NetProtocol + NetHostSession (wraps MatchRunner) + NetGuestSession (shadow replay + verification) | ✅ Authored |
| `Gameplay/Net/` — EventAnimator (shared pipeline), LanTransport (UDP beacons + TCP), OnlineHostController + OnlineGuestController, OnlineLobby | ✅ Authored |
| IBoardStateView abstraction — comedy/feel/identity mode-agnostic | ✅ Authored |
| Wizard: INTERNET permission (reflection-safe); no camera/mic/storage permissions | ✅ Authored |
| **D6 final decision** — P2P for V1; EOS post-V1 drop-in; Nakama rejected (`docs/D6-TRANSPORT-DECISION.md`) | ✅ Decided + documented |
| **Validation** | Python 98/98 ✅ · pools/rules sync IN SYNC ✅ · all touched C# balanced ✅ · owner two-device checklist tests 30–38 ⏳ |


### M6 — online hardening (reconnect/resilience): 🟡 protocol VALIDATED / Unity AUTHORED
| Component | Status |
|---|---|
| Proof rejoin (`hello code seat keyhex name`) — strangers rejected, session-key proof rebinds the seat, start+seeds replayed | ✅ **VALIDATED** (3 new tests) |
| App-restart reconnect — `persisted_session` (Python) / `PersistedSession`+PlayerPrefs (C#/Unity REJOIN button) | ✅ VALIDATED (py) / Authored (C#) |
| Bot takeover of a departed guest's seat + rejoin reclaim | ✅ VALIDATED (py) / Authored (C# host controller + transport GuestDisconnected) |
| Seat-parameterized `roll_for`/`move_for` (dice always from the fair stream) | ✅ VALIDATED / Authored (`RollFor`/`MoveFor`) |
| Non-animated resync (`resynced` → SyncAll snap) | ✅ Authored (protocol VALIDATED) |
| Flaky unfair-die test fixed (deterministic injection point) | ✅ 25× re-run clean |
| **Validation** | Python suite **101/101** ✅ · touched C# structurally balanced ✅ · owner checklist tests 39–42 ⏳ |

## Remaining (milestone order)

- [ ] **Owner validations (parallel, non-blocking):** ① `dotnet run --project tests/DesiLudo3D.Rules.Tests` (C# M0 closure) ② Unity first-open checklist (`docs/UNITY-VALIDATION.md`, M1 closure)
- [ ] **Owner validations (parallel, non-blocking):** ① `dotnet run --project tests/DesiLudo3D.Rules.Tests` ② Unity first-open checklist — now covers M1 tests 1–9 + M2 tests 10–16 (NativeGallery package auto-resolves on first open; internet needed once)
- [ ] **Owner validations (parallel, non-blocking):** ① `dotnet run --project tests/DesiLudo3D.Rules.Tests` ② Unity checklist tests 1–24 (M1 gray-box, M2 identity, M3 comedy/UI/save/look/clips)
- [ ] **M7** hardening/release: device-tier quality, orientation validation, Play compliance checklist (Data Safety: P2P, no data collected), packaging, final audit
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
