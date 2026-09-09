# CHECKPOINT — DesiLudo3D persistent progress record

> Per owner build directive §12: never rely on conversational memory for progress.
> This file is updated at every meaningful milestone. Last validated state = resume point.

| Field | Value |
|---|---|
| **Updated (UTC)** | 2026-09-09 (M7 COMPLETE — checkpoint documentation repair pass) |
| **Current milestone** | **M7 COMPLETE — all milestones M0–M7 code-complete.** Engine/protocol layers VALIDATED (Python suite 101/101 green); Unity layers AUTHORED, awaiting owner-side editor/device validation |
| **Current task** | No further implementation milestones remain. All remaining work is OWNER-EXECUTED: ① Unity editor validation (tests 1–29), ② C# rules closure (`dotnet test`), ③ two-device online + reconnect/takeover validation (tests 30–42), ④ real Android device + performance-tier testing (tests 43–45), ⑤ release AAB + store preparation (tests 46–48, `docs/RELEASE-CHECKLIST.md`). Every reported failure → Arena diagnoses → fixes → re-validates. |
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


### M7 — hardening & release readiness: 🟡 AUTHORED (owner validation gate)
| Component | Status |
|---|---|
| PerformanceTuner — §8.2 quality matrix auto-probe (mips/shadows/AA/fps; PlayerPrefs override) | ✅ Authored (boot-time, one path) |
| Wizard — target API 36 (reflection, Auto fallback), version stamp 1.0.0/1 | ✅ Authored |
| Wizard — one-click release AAB + §10 size-budget warning | ✅ Authored |
| docs/RELEASE-CHECKLIST.md — full spec-to-repo audit + store steps + $0 cost audit | ✅ Written |
| Orientation (D12) both allowed, portrait preferred | ✅ Set in M0; device test 45 ⏳ |
| **Validation** | Python 101/101 ✅ · all touched C# balanced ✅ · owner checklist tests 43–48 ⏳ |

## Final spec-to-repo audit (2026-09-09, M7 close-out)

Deep verification pass beyond the RELEASE-CHECKLIST table:

- **A1 camera/mic**: zero occurrences of WebCamTexture / Microphone /
  permission requests for camera or mic across all 46 C# files. The only
  permission is INTERNET (P2P sockets). ✅
- **Network surface**: the only UnityWebRequest in the repo loads LOCAL
  `file://` VO clips (VoicePacks). No http(s) endpoints, no analytics, no
  ads. TTS is the on-device OS service (android.speech.tts), no cloud TTS. ✅
- **Voice rule (absolute)**: ComedyPlayback picks VO via
  `VoicePacks.GetClip(identity.Category, …)` — the PROFILE's category
  (male/female/child…), never the seat/colour. Comment-enforced +
  pool-sync-tested. ✅
- **IL2CPP safety**: no `dynamic`, no reflection-emit; the two reflection
  uses (internetAccess, targetSdk) are editor-only with runtime fallbacks. ✅
- **No silent removal**: WireSharedOnline rebuilds identity/feel/comedy for
  online modes over IBoardStateView (same comedy/feel/identity stack
  offline AND online); teardown unsubscribes the persistent HUD from the
  destroyed IdentityCenter. ✅
- **Symbol audit**: FixedSequenceDiceProvider, MatchRunner.TryRollValue/
  TryLeave, GameState.StateHash, UiStrings keys, lobby UI helpers,
  CommitRevealDice — all exist and are used consistently. ✅
- **Guest-command guards (C# = Python)**: not-your-turn check, phase
  checks, die always drawn host-side from the fair stream (guests cannot
  inject values). ✅
- **Structural balance**: 46/46 C# files brace/paren balanced. ✅
- **Python suites**: 101/101 (61 rules + 18 comedy + 8 agents + 14 netproto,
  flake-swept 25×). ✅

## Remaining work (ALL owner-executed; Arena fixes on failure reports)

Nothing implementable remains in the build environment. The genuinely
remaining work, in order:

- [ ] **C# rules closure (owner):** `dotnet run --project tests/DesiLudo3D.Rules.Tests` on any .NET SDK 8+ machine — M0 conformance vectors against the canonical C# port
- [ ] **Unity editor validation — offline (owner, tests 1–29):** first-open setup, M1 gray-box match, M2 identity/photo medallions, M3 comedy/EN-HI/save-resume/look/clips, M4 bots/VO packs/tutorial (`docs/UNITY-VALIDATION.md`)
- [ ] **Two-device online multiplayer validation (owner, tests 30–38):** room create/join (LAN beacon + manual `ip:CODE` + hotspot), full online match with animations/comedy parity, fairness console check (no `unfair-die`/`hash-mismatch`)
- [ ] **Reconnect / rejoin / takeover validation (owner, tests 39–42):** app-restart REJOIN button, wrong-key rejection, guest-exit bot takeover, Wi-Fi-drop recovery + seat reclaim
- [ ] **Real-device performance validation (owner, tests 43–45):** T1-class (~3 GB RAM, Android 8+) holds 30 fps, mid/high 60 fps, portrait/landscape rotation clean (D12)
- [ ] **Release build (owner, tests 46–47):** wizard Player Settings verified (API 36 target, INTERNET-only permission), `Tools > DesiLudo3D > 5. Build Android AAB (release)`, size ≤ 80 MB, installs + launches
- [ ] **Store preparation (owner, test 48 + `docs/RELEASE-CHECKLIST.md` §3):** upload keystore (+backup), age-rating questionnaire, Data Safety form (no data collected; P2P only), privacy-policy URL, name/trademark check (D9), closed testing track
- [ ] **Defect loop (Arena, continuous):** every failure reported from any item above → diagnose → fix → re-run suite → record in IMPLEMENTATION-LOG → commit/push. Never mark untested features complete.

## Final state of the repository (M0–M7 complete)

- **Engine + protocol (VALIDATED):** `tools/reference-engine/` — `ludo/` (model, engine, dice, boardmap, netproto, comedy, agents, cli, generate_vectors) + 101 tests incl. fault injection (drop/tamper/disconnect/lies)
- **Unity (AUTHORED — 46 C# files + 4 asmdefs):** `unity/Assets/_Core/` — Rules (synced port), Core (runner, agents, comedy, net sessions), Gameplay (board/dice/camera/HUD/identity/comedy/net/save/look/feel/perf tuner/tutorial), Editor (setup + release wizard)
- **Docs:** MASTER-SPEC v1.1 (locked), IMPLEMENTATION-LOG IL-001…IL-041, UNITY-VALIDATION (48 owner tests), RELEASE-CHECKLIST, D6-TRANSPORT-DECISION (final: P2P), RULES-FORMAT, BUILD, README
- **Cross-checks:** Rules↔Unity sync IN SYNC; comedy pools Python↔C# IN SYNC (test-enforced)

## Tests (last full sweep — 2026-09-09, HEAD 525ffed)

- Python suite: **101/101 green** (61 rules + 18 comedy + 8 agents + 14 netproto; netproto flake-swept 25x)
- Structural: 46/46 C# files brace/paren balanced; asmdefs + manifest JSON valid
- C# compile/runtime + Unity editor + real devices: **PENDING OWNER** (no toolchain in sandbox — IL-001/R12)

## Known issues / environment constraints

1. No C# toolchain / Unity Editor in this sandbox (IL-001, R12) — dual-track: Python reference VALIDATED, C# statically mirrored; the owner checklists close the gap.
2. C# Gameplay code authored against Unity 6 APIs from reference — first-open compile slip possible; UNITY-VALIDATION instructs capturing any console error for a repo fix rather than hand-editing.
3. Unity 6000.0.32f1 authored; any 6000.0.x+ opens the project (upgrade prompt OK).
4. UDP room discovery may be blocked by AP isolation on some Wi-Fi networks — manual `ip:CODE` join always works (M5 documented boundary).

## Next action (resume point)

**Owner:** run the validation gate in order — Unity first-open checklist (tests 1–29) → C# rules `dotnet test` → two-device online (tests 30–42) → device-tier performance (tests 43–45) → release build + store prep (tests 46–48). Report every failure into the repo.
**Arena:** on any failure — diagnose → fix → re-run tests → update IL-LOG + this checkpoint → commit/push. Validation discipline: B/C/D/E below are NOT complete until the corresponding owner validation has actually passed.

**Completion ladder (do not exaggerate):** A. Code-complete ✅ · B. Unity-runtime validated ❌ pending owner · C. Real Android-device validated ❌ pending owner · D. Release-ready ❌ pending owner · E. Published on Google Play ❌ pending owner.

## Commit state

- Branch: `arena/01a08213-desiludo3d`
- M7 code close-out: **`525ffed`** — all M0–M7 code, tests and docs (last commit touching code)
- This checkpoint repair: documentation-only commit directly on top of `525ffed` (becomes the new HEAD)
- Full chain: `60639db` spec lock → `a0789dc` M0 → `7c4a97a` M1 → `592e039` M2 → M3 `2b26ba8`/`de977d9`/`1a14be2`/`a42a344`/`b26bd7d` → M4 `ed706d1`/`4fc0422` → M5 `517bb40`/`6ee9236` → M6 `2fa8d01` → M7 `8eabf80`/`525ffed`
