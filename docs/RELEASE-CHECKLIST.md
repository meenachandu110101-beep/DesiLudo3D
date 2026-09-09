# Release Readiness Checklist — DesiLudo3D V1 (M7)

Status legend: ✅ done & machine-validated · 📝 done, owner-executed validation
pending (see `docs/UNITY-VALIDATION.md`) · ⏳ owner action at submission time.

## 1. Full spec-to-repo audit (Master Spec v1.1 → code)

| Spec area | Requirement | Where | Status |
|---|---|---|---|
| §4 rules | Indian Ludo ruleset, blockades, 3-sixes, capture-extra, placings | `Rules/Engine.cs`, `Rules/Model.cs` + Python reference (`ludo/engine.py`) | ✅ 61 rule tests green; C# mirror structurally balanced |
| §4.2 dice | Fair dice, commit–reveal, splitmix64 stream | `Rules/Dice.cs`, `Core/Net/NetSession.cs` | ✅ 11+3 net tests green |
| §5 identity | Profiles (name/photo/voice), picker-only photos (A1) | `Identity/*` | 📝 authored; picker-only confirmed (NativeGallery, no camera permission) |
| §5.2 reactions | Prerecorded clips, pooled playback, FaceReactionProvider seam | `Identity/ReactionClips.cs` | 📝 authored; no camera capture anywhere (A1 audited) |
| §6 comedy | ReactionDirector, 152-line Hinglish pools, guardrails §6.3 | `Core/Comedy/*`, `Gameplay/Comedy/*` | ✅ pools IN SYNC Python↔C# (test-enforced), 18 comedy tests |
| §6 comedy VO | Voice follows PROFILE (male/female/child), never token colour | `Identity/VoicePacks.cs`, `ComedyPlayback.cs` | ✅ authored + unit-checked pools; 📝 device listen-test |
| §7 capture choreography | Shared animation pipeline, slow-mo, screen shake | `Net/EventAnimator.cs`, `GameFeel.cs` | 📝 authored (same pipeline offline + online) |
| §8 premium visuals | Look-dev pass, art direction | `LookDev.cs`, `docs/concepts` | 📝 gray-box look pass authored |
| §8.2 quality matrix | T1/T2/T3 same-direction levers | `PerformanceTuner.cs` | ✅ authored (auto-probe at boot); 📝 T1 device run |
| §9 online (P7) | Friends-only rooms, codes, 2–4 devices, sync, reconnect, fairness, failure handling | `Core/Net/NetSession.cs`, `Gameplay/Net/*` | ✅ protocol 14/14 net tests (101/101 suite); 📝 two-device checklist tests 30–42 |
| §9.3 verifiable fairness | Host-authoritative + commit–reveal + signed logs + shadow replay | netproto (py+C#) | ✅ VALIDATED incl. fault injection (drop/tamper/lies) |
| §10 perf budgets | 30/60 fps tiers, AAB ≤ 80 MB | `PerformanceTuner.cs`, wizard step 5 | ✅ code-only AAB well under budget; 📝 device FPS check |
| §10 compliance | Target API 36, min API 26 | `ProjectSetupWizard` step 1 | ✅ set via reflection (Auto fallback documented) |
| §12 privacy | No analytics/ads/trackers; P2P only; no camera/mic/storage permission | whole repo + manifest (INTERNET only) | ✅ audited: no network call except P2P transport; picker permissionless (Android 13+) |
| §13 UX screens | Setup → match → results; EN/HI; tutorial | `HudView`, `UiStrings`, `TutorialFlow` | 📝 authored; 📝 device pass |
| §14 pipeline | All-free assets, procedural audio | `SfxSynth.cs`, concept art | ✅ zero paid assets/deps (cost audit below) |
| A1–A5 | No camera capture; voice rule; offline-first; online V1; no silent scope cuts | throughout | ✅ audited (see §4 below) |

## 2. Owner validation status (the gate to release)

- **Unity editor (M1–M7):** `docs/UNITY-VALIDATION.md` tests 1–29 (offline),
  30–38 (online), 39–42 (resilience). Any failure → diagnose → fix → re-run.
- **Real devices:** at least one T1-class (₹7–10k, Android 8+) and one modern
  device; full pass of the tables above.
- **Two-device online:** host + guest over one hotspot and over a router.

## 3. Store submission steps (owner, in order)

1. **Device lab pass complete** (section 2) — no crashes, 30 fps on T1.
2. **AAB:** Unity menu `Tools/DesiLudo3D/5. Build Android AAB (release)` →
   `Builds/DesiLudo3D-1.0.0.aab`. Budget: ≤ 80 MB (alert > 120).
3. **Signing:** create a private upload keystore (Player > Publishing
   Settings); **back it up** — losing it means a new listing forever.
4. **Age rating questionnaire:** Everyone; honest answers (no violence, no
   gambling — no real money anywhere, board game).
5. **Data Safety form:** *No data collected, no data shared* with us;
   P2P game traffic and P2P profile photo/clip exchange happen directly
   between the players' devices on their own network (disclose "app data"
   P2P exchange if the form requires it). No ads SDK, no analytics SDK.
6. **App name / trademark (D9):** final Play Store + trademark search — stay
   clearly distinct from "Ludo King" etc.
7. **Privacy policy URL:** required by Play for all apps — a simple static
   page stating: no data collection, P2P only, photos never leave the device
   except directly to match participants.
8. **Closed testing track** (free): upload AAB, add testers, run ≥ the
   checklist tables, then soft launch.

## 4. Cost audit ($0 mandate)

| Item | Cost |
|---|---|
| Unity 6 Personal | $0 (revenue < threshold) |
| All code/art/audio | in-repo / procedural / concept art — $0 |
| NativeGallery plugin | MIT, $0 |
| Online transport | P2P — no server, $0 (D6 final) |
| Play Console one-time fee | $25 — the ONLY planned payment (owner decision, D11) |
| Everything else | $0 — no TTS subscriptions (bundled VO + optional on-device TTS), no relay, no analytics |

## 5. Known V1 boundaries (documented, not hidden)

- Internet play across arbitrary NATs needs host reachability or the
  post-V1 EOS Relay drop-in (D6 decision doc) — same-room/hotspot play is
  the designed use case.
- Reactions are prerecorded clips only (A1); the FaceReactionProvider seam
  stays dormant.
- TTS comedy is an optional fallback; prerecorded original VO is primary.
- Gray-box visuals: the look pass (LookDev) sets direction; final art
  polish is the owner's asset pass, not code.
