# DesiLudo3D — Master Specification

| | |
|---|---|
| **Version** | 1.1 — LOCKED (owner build-directive amendments A1–A5 applied; D1–D14 resolved) |
| **Status** | 🔒 LOCKED FOR IMPLEMENTATION — authoritative build contract. Progress: `CHECKPOINT.md` · implementation decisions: `docs/IMPLEMENTATION-LOG.md` |
| **Date** | 2026-09-09 (v1.0 decisions 2026-09-08) |
| **Amendment policy** | This full-document update is the *only* full rewrite. From now on: each decision answer updates the **Decision Log (§17)** plus a targeted section edit — never a full rewrite. |

> **Legend:** *Decision* (proposed, needs ✅) · *Open Decision* (❓ — asked one at a time) · *Non-Goal* (postponed).
> All open decisions are resolved; the Decision Log (§17) additionally records the owner's final build-directive amendments (A1–A5).

---

## 1. Vision

DesiLudo3D is a **premium-looking**, stylized 3D Ludo game for Android with a warm desi (Indian) flavor, where **every player's token carries that player's real face**, and where **the players' real faces — never emoji, never generic avatars — perform the reactions**, backed by an **original Hinglish comedy system**.

- **Premium visual identity** — polished models, materials, lighting, shadows, animations, camera work. NOT a cheap/basic Ludo clone. Optimized geometry is welcome; a "cheap" look is not.
- Playable **fully offline** (pass-and-play + bots) and **online with friends over the internet — in V1**, built on a networking-ready core from day one.
- Built and tested at **$0 service cost**; the only planned mandatory payment is Google Play's one-time $25 fee at publish time.
- **No generative AI in V1**; architecture keeps clean seams so it can be added later without redesign.

## 2. Non-negotiable pillars

| # | Pillar |
|---|---|
| P1 | **Premium stylized 3D desi visuals** — same art direction scales low-end → flagship via quality settings |
| P2 | 3D Ludo, desi-themed, 2–4 players |
| P3 | Player's real photo on their 3D token — identity always recognizable |
| P4 | Real-face reactions — no emoji, no generic characters; player's own face performs every reaction |
| P5 | Original desi/Hinglish comedy library (event pools, randomization, no repeats) |
| P6 | Offline play: pass-and-play + vs-computer + save/resume |
| P7 | Online friends multiplayer **in V1** (private rooms, room codes, 2–4 devices, sync, reconnect, fair simulation) |
| P8 | $0 development/service cost; no paid APIs, SDKs, hosting, or asset subscriptions |
| P9 | No generative-AI dependency in V1 |

## 3. Non-goals for V1 (postponed — per owner's scope discipline)

Public matchmaking/ranked/ELO · accounts/social/friends lists/cloud save · text & voice chat · **2v2 team play** · host migration (if it materially complicates V1) · Bluetooth/Wi-Fi-Direct · iOS · monetization/ads · generative AI · spectator mode · replay browser UI · leaderboards · server-authoritative anti-cheat beyond the fairness design in §9.3.

## 4. Gameplay rules

### 4.1 Base ruleset (Indian Ludo / Pachisi derivative) — locked

- 4 colors × 4 tokens. Modes: 2-player (opposite corners), 4-player free-for-all. *(2v2 postponed — Decision Log D1.)*
- Roll a **6 to release** from base; a 6 grants an extra roll.
- ✅ **Three consecutive 6s forfeits the turn** — all three rolls are void (D2, resolved). Classic desi rule; a guaranteed trigger for the frustration comedy pool.
- Landing on an opponent on a non-safe cell **captures** it. Safe: starred cells + each color's start cell.
- ✅ **Capturing grants an extra roll** (D3, resolved).
- ✅ **Blockades** (two own tokens on one cell block all passage) — **per-match toggle in match setup**, default ON (playtest-tunable); in online rooms the **host** sets it at room creation (D4, resolved).
- **Exact roll** to finish a token. First player to bring all 4 home is the **winner**.
- ✅ **Match continues for placings after the first winner** (D5, resolved): finished players stop rolling; play runs until only one player has unfinished tokens (remaining humans may "leave with placing"); standings = finish order → tokens home → track progress. In **2-player matches** the first winner ends the match. When only bots remain, a "fast-forward to results" option resolves the tail quickly.

### 4.2 Dice

- 3D physics dice tumble that lands on the **cryptographically predetermined** value — fair by construction, never visibly faked.
- `DiceProvider` abstraction: offline = seeded CSPRNG; online = **commit–reveal** (§9.3).

### 4.3 Turn flow & UX

- Pass-and-play: board auto-rotates so the active player's corner faces them (toggleable).
- Turn timer optional (off by default). Full save/resume on every state change (survives app kill / process death).

## 5. Player identity & the face-reaction system

### 5.1 Identity on the token (P3)

- Import via **Android Photo Picker** (guided oval crop). **No camera and no storage permissions in V1** (Amendment A1); in-app camera capture is a post-V1 capability behind the existing seams.
- Downsample ≤512 px → runtime texture on a stylized 3D token head; face always recognizable; material tuned so photos stay clean under scene lighting.
- **Rule:** any reaction that involves a face uses the **player's own face medium** (their photo / their imported reaction clip). A tongue-out is the *player's* tongue-out. **Emoji faces are never substituted.**
- Players who skip photos get themed default tokens: full dialogue + body-language animation comedy, **no face visuals** — never a second-class experience.
- **Character profile = source of identity (A5):** each player profile defines name, photo, **character type (male / female / child)**, **voice profile** (A2), personality/dialogue pool, R1 clip library, and reactions on/off. The profile — **never the token color** — determines voice and dialogue pools (§6).
- Profile controls: view / **replace / delete** photo & clips at any time (§12).

### 5.2 Reaction layers — all behind `FaceReactionProvider`

| Layer | What | V1? |
|---|---|---|
| **R1 — Clip library** | Player-owned prerecorded clips (~2 s per emotion: laugh, angry, surprise, tongue-out, sad, victory) — **imported via the system photo/video picker** (no camera in V1, A1), trimmed + emotion-tagged in-app; events play the clip as an animated texture on that player's token. | ✅ **Core of V1** |
| **R2 — Reaction moments (camera)** | Camera-based live reaction capture at big events. **Removed from V1 by Amendment A1** — no camera permission flow, no capture during gameplay, no clip encoding/upload/sync, no camera cooldowns, no camera privacy UI, no camera runtime systems. Architecture slot retained. | ❌ Post-V1 |
| **R3 — Continuous live (camera)** | Per-device face tracking (MediaPipe: 478 landmarks + 52 blendshapes, on-device, free); "tongue-out" is not a standard blendshape. Same camera-free boundary as R2 in V1. | ❌ Post-V1 (online mode) |

**Camera-free V1 (A1):** the absence of camera reactions must NOT reduce premium quality, comedy, character identity, or gameplay experience. V1 comedy = **spoken character voices (primary, A4)** + R1 face clips + text bubbles (secondary) + body-language animation. All camera-related capability sits behind the `FaceReactionProvider` seam so a future version can add it without gameplay redesign.

**Reaction events (drive both faces and dialogue, §6):** cutting a token · being cut · token reaches home · winning · losing · rolling a 6 · landing on an occupied cell · being shoved · celebrating · frustration (bad-luck streaks) · manual taunt.

## 6. Desi comedy & dialogue system (P5) — NEW in v0.2

### 6.1 ReactionDirector (the brain)

On each qualifying event, considering **game-state context** (who's leading, tokens-home count, capture streaks, losing streaks, match phase), the director:

1. Picks the **event pool** (+ situational sub-pools, e.g. frustration escalates after 3 bad rolls).
2. Filters by **cooldowns** (per event-class and global frequency cap, so it's funny, not noisy).
3. Filters by **recent-history** (no line repeats within the last N uses; no immediate repetition).
4. **Randomizes** the selection; big events (capture, win) may override smaller ones.
5. Renders (**voice primary, A4**): ① **character-profile voice clip** (male/female/child per the profile, A2) → ② **optional speech bubble** (world-space, above the token; readability/accessibility) → ③ **the player's R1 face clip** where available → ④ token body-language animation.

### 6.2 Original Hinglish line library — character-category pools (A2/A3)

The library is **data** (JSON consumed by ReactionDirector). Pools are organized per **event × character category** (male / female / child) with per-character personality variants. The starter seed below ships at M3 and is expanded (target: **150+ original lines**) during M4. Common colloquial phrases only — see §6.3 guardrails.

**Male pools (starter):**

| Event | Lines |
|---|---|
| Capture — attacker | "Kaat di na!" · "Chal beta, ghar chalein!" · "Sorry bhai, niyam niyam hai!" · "Mera raasta, meri marzi!" · "Bas itna hi tha?" · "Wapas base camp!" · "Ek-ek karke sab!" |
| Capture — victim | "Hawww! Yeh kya kar diya!" · "Ruko, main wapas aaunga!" · "Bas bhai, ab ghar jaana hai!" · "Mera token! Mera pyaara token!" · "Thoda daya karo!" · "Aaj toh mera kharab din hai!" |
| Rolled a six | "Aaj toh meri lottery hai!" · "Chhakka pe chhakka!" · "Isko kehte hai kismat!" · "Ek aur! Ek aur!" |
| Token home | "Ghar aaya ghar aaya!" · "Swagat nahi karoge hamara?" · "Ek down, teen baaki!" |
| Winning | "Champion! Champion!" · "Aaj ka din mera hai!" · "Jaise taise… jeet gaya!" · "Koi shaq?" |
| Losing / frustration | "Tumse na ho paega beta." · "Yeh dice hi saazish hai!" · "Kal phir khelenge. Pakka." · "Ek bhi chhakka nahi!" |
| Landed on occupied cell | "Chal hat!" · "Door ja." · "Side de bhai." · "Thoda adjust kar le!" · "Yeh public square hai, boss!" |
| Being shoved | "Oye! Hosh mein!" · "Dheere dheere, bhai zinda hai!" · "Dhakka-mukki band karo!" |
| Celebrating / taunt | "Balle balle!" · "Naach meri token, naach!" · "Aa raha hoon, bach ke!" · "Ghabra mat, main hoon na!" |

**Female pools (starter — written natively for female speakers, not male lines with word swaps):**

| Event | Lines |
|---|---|
| Capture — attacker | "Dekha ji? Birbal ki beti hoon main!" · "Arre, itni aasani se ghar? Chalo ji!" · "Mera raasta hai, zara hatiye!" · "Aaj toh mera jhakaas din hai!" · "Sorry na, niyam hai!" |
| Capture — victim | "Haww! Yeh kya tha?!" · "Chee! Bina dekhe kaat diya!" · "Zara sambhal ke na!" · "Mera pyaara token bhi gaya!" · "Theek hai, dekh lungi!" |
| Rolled a six | "Wah! Kismat saath de rahi hai!" · "Ekdum perfect roll!" · "Lagta hai aaj meetha banwana padega!" · "Ek aur? Kyun nahi!" |
| Token home | "Ghar pahunch gayi, finally!" · "Swagat nahi karoge meri token ka?" · "Ek kam ho gaya, teen baaki!" |
| Winning | "Kya baat hai! Jhakaas!" · "Aaj ka din mera hai, ji!" · "Dekha? Himmat se sab hota hai!" |
| Losing / frustration | "Yaar, yeh dice hi mujhse dushmani rakhta hai!" · "Hai rabba! Ek bhi chhakka nahi!" · "Patience ka baandh toot gaya!" · "Kal phir khelenge. Pakka wada." |
| Landed on occupied cell | "Zara side dijiye na!" · "Ek gaj jagah nahi milti yahan?" · "Adjust kar lijiye ji!" |
| Being shoved | "Oye hoye! Dheere!" · "Dhakka-mukki? Family game hai ji!" |
| Celebrating / taunt | "Naacho meri token, naacho!" · "Shabash, meri sher-ni!" · "Aa rahi hoon, bach ke!" · "Ghabraiye mat, main hoon na!" |

**Child pools (starter — simple, innocent, playful, always age-appropriate):**

| Event | Lines |
|---|---|
| Capture — attacker | "Dekho dekho, kaat diya!" · "Sorry! Niyam hai na!" · "Mummy ne sikhaya tha!" · "Ek-ek karke sab!" |
| Capture — victim | "Ye galat hai!" · "Mera token! Wapas do!" · "Bas! Ab main naraz hoon!" · "Next time bach ke!" |
| Rolled a six | "Six! Six! Six!" · "Ye mera lucky dice hai!" · "Yay! Ek aur chance!" |
| Token home | "Ghar aa gaya!" · "Mummy, dekho — ek poora!" · "Ab candy milegi?" |
| Winning | "Main jeet gaya! Main jeet gaya!" · "Sabko bataunga main jeeta!" |
| Frustration | "Ye dice bura hai!" · "Bas! Do minute break!" · "Mujhe nahi khelna… theek hai, khelunga." |
| Landed on occupied cell | "Thoda side ho jao na!" · "Adjust kar lo yaar!" |
| Greeting / celebrating | "Hi sabko!" · "Wah wah!" · "Chalo khelte hain!" |

**Personality variants (A3):** beyond category, each character profile selects a personality style (e.g., *Overconfident Hero*, *Dramatic Mamma's Boy*, *Sweet-but-Deadly*, *Drama Queen*, *Excited Bachcha*) that biases pool selection and adds variant lines — no two characters need sound identical. The system supports character-specific pools and contextual variations.

### 6.3 Content guardrails (hard rules)

- **100% original lines.** No copyrighted celebrity voices, no copyrighted characters, no imitation of identifiable celebrities, no lifted movie dialogues where identifiable. Common colloquial phrases only.
- Family-friendly, playful — teasing, never violent or abusive.
- **Delivery (D14, amended A2/A4, 2026-09-09): SPOKEN comedy is primary; bubbles are secondary — V1 comedy is never text-only.**
  - **Voice (primary, M4):** original recordings by owner/friends/family ($0), organized as **VoiceProfiles selected by the character profile** — male character → male voice, female character → female voice, child character → child-appropriate voice. **Token color never determines gender or voice.** Multiple profiles per category are supported cleanly (e.g., Male-Overconfident, Female-SweetButDeadly, Child-Excited).
  - **Bubbles (secondary, M3):** truck-art-styled text bubble shown alongside the voice line for readability/accessibility, and whenever voice assets/clips are absent.
  - **Players' own voices:** each player's R1 imported clips may include their own laugh/taunt — genuine voice, no extra system.
  - **TTS:** optional free on-device fallback for missing coverage only; it must **never replace** prerecorded original voice assets where they exist.

## 7. Token interactions & capture choreography — NEW in v0.2

All interactions are **cartoonish, squash-and-stretch, family-friendly** — comedy, not violence.

| Interaction | Choreography |
|---|---|
| **Capture (cut)** | Attacker winds up → strike → victim pops off with spin + dizzy stars → bounces home; attacker voice line (A4) + attacker's R1 face clip; victim voice line + victim's R1 clip |
| **Land on occupied safe cell** | Playful shove/bump: arriving token shoulder-bumps, occupant wobbles aside; "chal hat / side de" lines from the **arriver's player**; both tokens coexist |
| **Own blockade formed** | Tokens high-five / stack pose; small line |
| **Bonk into blockade** | Arriving token bonks, sees stars, steps back one step |
| **Token finishes** | Victory lap + confetti + line + face reaction |
| **Roll a 6** | Dice pulse + token anticipation lean + line (cooldown-gated) |

Reactions use **both** systems together: the player's real face (R1) **plus** the spoken Hinglish line (primary, A4) **plus** body-language animation.

## 8. Premium visual direction & scalability — NEW in v0.2

### 8.1 Direction

- **Motif (resolved, D7): Festival base + truck-art comedy accents.** The *stage* is festive — rangoli-mandala safe squares, marigold-garland board framing, diya-lit home columns, Holi color-burst capture VFX, warm evening palette (saffron, deep red, teal, gold). Truck-art energy lives where the comedy lives — painted accents on the board rim, speech-bubble styling, celebration props, UI flavor — never at the cost of board readability.
  - Concept references (AI-generated, directional only): `docs/concepts/board-concept-v1.png` · `docs/concepts/token-identity-concept-v1.png` — to be validated/superseded by the M1 look-dev slice on a real T1 device.
- Polished models (optimized geometry, clean silhouettes), stylized-PBR materials, warm palette, decorative board framing.
- **Lighting:** key directional light + baked ambient GI; soft dynamic shadows under tokens; tiered post-processing (bloom, tone-mapping, color grade; AO/SSAO on high tier).
- **Camera:** cinematic intro orbit; dynamic framing of the active player's zone; capture moments get a quick punch-in + micro-shake; smooth transitions everywhere.
- **Animation:** tweened hop arcs with squash-and-stretch, dice anticipation→tumble→settle, anticipation on every state change. Object pooling for all VFX (confetti, stars, dust).

### 8.2 Quality scaling — SAME art direction on every tier

| Lever | T1 (low) | T2 (mid) | T3 (flagship) |
|---|---|---|---|
| Geometry | LOD1 | LOD0/LOD1 | LOD0 |
| Textures | ½ res, ASTC 4×4 | Full res, ASTC 6×6 | Full res, ASTC 8×8 |
| Shadows | Blob/single cascade | 2 cascades | 4 cascades + contact |
| Post-FX | Tone-map only | + Bloom, grade | + AO |
| Target | 30 fps | 60 fps | 60 fps |

- SRP Batcher + GPU instancing for tokens; static board batched; texture streaming; no per-turn GC allocations in rules code.
- Premium look is **validated early** via a look-dev vertical slice in M1 on a real low-end device (see Risks R1/R2).

## 9. Online multiplayer (P7 — in V1, built on a networking-ready core)

### 9.1 Model (unchanged)

- Friends-only **6-character room codes**; creator = host; 2–4 players on separate Android devices.
- **Host-authoritative** simulation: guests send *intent commands*, host runs the rules engine and broadcasts *events*. Turn-based ⇒ tolerant of 200–500 ms latency.
- Bandwidth: commands/events (bytes) + **one-time profile sync at room join (photo + R1 clips + voice-pack reference)** — no streaming, no camera data (A1). Reconnect window 90 s with state snapshot; host drop within window = resume, else match voided (host migration postponed, D1-adjacent).

### 9.2 Transport comparison (required by owner before finalizing) — D6 (provisional)

| Option | Cost at any scale | Ops burden | Integration effort | Key risk |
|---|---|---|---|---|
| **Unity Relay + Lobby** | Free ≤ ~50 **average monthly CCU**, then ~$0.16/CCU/month | None | Lowest (first-party NGO path) | **Metered free tier** — exactly the "free tier won't last forever" trap |
| **Epic Online Services (EOS)** | **Free, unmetered** (no CCU fees; built for Fortnite, offered free to all devs) | **None** | Medium (community Unity plugin + Epic dev account; works with current NGO) | No *contractual* free-forever guarantee; platform dependence |
| **Nakama self-hosted** | $0 software (Apache-2.0); hosting = your own hardware or free cloud | **High** (24/7 server + Postgres, updates, TLS, backups) | Highest | Oracle Always Free **reclaims idle instances** (7-day idle rules); home server uptime/CGNAT |
| Custom self-built server | $0 software | Highest | Highest | You own every problem |

**Decision (provisional, 2026-09-08):** **EOS is the working assumption; final commitment happens at M5.** Binding conditions:

- Development remains **$0** apart from the eventual Play fee; **no recurring paid multiplayer dependency without the owner's explicit approval**.
- **Unity Relay is not eligible as the default** — its free tier is metered.
- **M5 must evaluate, on real devices:** EOS + NGO integration effort & stability, performance, reliability, fairness-model fit, Android compatibility across T1–T3, and long-term cost risk — **benchmarked against self-hosted Nakama** (ops burden assessed honestly, incl. Oracle idle-reclamation).
- If EOS genuinely satisfies the $0 long-term requirement → **use EOS for V1**. If it introduces any unavoidable future cost or limitation conflicting with requirements → **prefer a genuinely free/self-hostable solution (Nakama)**, provided the operational burden is acceptable.
- `NetTransport` stays provider-agnostic — a backend change must never require rewriting rules or gameplay systems.

**M5 evaluation checklist:** NGO+EOS plugin on T1/T2/T3 devices · lobby join-by-code UX · latency & reconnect behavior · commit–reveal integration · SDK size impact on AAB · Epic ToS/policy review · Nakama ops-cost memo (server, DB, TLS, uptime, idle-reclaim).

### 9.3 Fair dice & verifiable fairness (no game server required)

- **Commit–reveal dice:** host & guest each commit `SHA-256(seed)` before a roll, reveal after; result = `H(seedA‖seedB) mod 6`. Neither side can bias; verifiable post-hoc.
- **Signed event log:** host broadcasts every command→event; guests keep the log; discrepancies are replayable. **No client other than the host holds authority over critical game results, and the host's authority is verifiable post-hoc via the log.** For friends-only V1 this is verifiable fairness; strict *server*-authoritative simulation (Nakama authoritative matches) remains an upgrade path if the M5 review selects Nakama.

## 10. Device tiers & performance budgets

| Tier | Device class | FPS | Same-direction quality (§8.2) |
|---|---|---|---|
| T1 Low | Android 8+ (API 26+), 3 GB RAM, GLES 3.0 (₹7–10k) | 30 | ½-res textures, 1 shadow cascade, tone-map only |
| T2 Mid | Android 10+, 4–6 GB RAM | 60 | Full res, 2 cascades, bloom+grade |
| T3 High | Flagships | 60 | Full effects + AO |

- **AAB target ≤ 80 MB** (alert > 120 MB; hard cap 150 MB with free Play Asset Delivery).
- Thermal/battery: **no camera in V1 (A1)**; R1 clip playback is pooled/budgeted decoding; comedy frequency cooldowns; optional 30 fps cap; dim/idle menus.
- Compliance: target **Android 16 (API 36)** for new-app submission (post-Aug 2026 rule); min SDK stays low.
- Real T1 device in the test lab from M1 onward — non-negotiable.

### 10.1 Store readiness checklist (M7)

- **App name / trademark check** (D9, resolved): "DesiLudo3D" working title; final manual Play Store search + trademark check immediately before launch; name and art must stay clearly distinct from "Ludo King" and other trademarked competitors.
- **Age rating questionnaire** answered honestly — target Everyone / family-friendly (comedy content guardrails §6.3 support this).
- **Data Safety form:** no data collected/uploaded to us; P2P sharing of profile photo/clips with room participants disclosed (§12); **no camera permission requested in V1**.
- **Target API 36** build verified at upload (Play requirement for new apps post-Aug-2026).
- **Closed testing track → soft launch** (free Play Console features; the one-time $25 developer fee is the only planned payment).

## 11. Software architecture (future-AI-ready)

```
DesiLudo3D.Rules        → pure C# (.NET Standard), ZERO UnityEngine refs
                          MatchState, ApplyCommand → Events, win checks, RNG interface.
                          Unit-tested headless; reusable on a future server.
DesiLudo3D.Unity (URP)  → presentation, input, audio, VFX
   MatchRunner          → applies commands, raises events, drives animation/camera
   FaceReactionProvider → R1 picker-imported clips | (camera layers R2/R3 — post-V1 seam)
   DiceProvider         → local CSPRNG | online commit–reveal
   OpponentAgent        → bots: Easy (weighted random) / Medium (heuristics) / Hard (expectiminimax)
   ReactionDirector     → comedy brain (§6): pools, cooldowns, history, context
   NetTransport         → offline loopback | LAN | EOS / Relay / Nakama (swappable, §9.2)
   ProfileStore         → device-only persistence (photos, clips, profiles)
```

- **Event-sourced match log** (command sequence + seed) = free replays, free debugging, and a ready-made future AI training dataset. This is the AI seam; **no AI code in V1.**

## 12. Privacy (privacy-first, hard requirements)

- **No camera in V1 (A1)** — the app requests no camera permission; photos and reaction clips are imported via the Android system Photo Picker (no storage permission either). Nothing ever runs in the background.
- Photos/clips **local-only**, except: the profile (name, photo, R1 clip library, voice reference) is shared with participants of an online room you join — disclosed and consented at join, never stored server-side by us.
- **Controls:** view / replace / delete photo and clips at any time from the profile editor; per-profile reactions on/off; per-match reactions on/off.
- Play Data Safety: "no data collected/uploaded to us"; P2P sharing disclosed. No analytics SDKs in V1.

## 13. UX — screens (V1)

1. **Home:** Play (Pass & Play) · Play vs Bots · Play Online · Profiles · Settings · How to Play
2. **Profile editor:** name, photo (import/replace/delete), **character type & voice profile with preview**, R1 clip import + emotion tagging, reactions toggle
3. **Match setup:** local profiles/bots, **house-rules toggle (blockades)**; online: create room (code; host sets house rules) / join code
4. **In-match (adaptive portrait/landscape):** 3D board, active-player banner, dice, speech bubbles, reaction popups, event toasts, pause (resume/save/quit)
5. **Results:** standings + "best reactions" strip + rematch
6. **Settings:** sound/music, language, graphics tier (auto + manual), online profile sharing, colorblind mode

**Orientation (D12, resolved):** both orientations supported — responsive anchored layouts + safe-area handling on every screen from day 1; **portrait is the default** and the pass-and-play-preferred framing; the 3D camera has two authored framings (portrait tall / landscape wide); the system rotation lock is respected.

**Accessibility:** colorblind palettes + shape-coded tokens, scalable UI text, sound-off respect.

## 14. Art & audio pipeline (all free)

Blender (models) · Kenney/Quaternius/Poly Pizza CC0 (placeholders & reference) · original models for hero assets (board, tokens, dice) · Audacity + CC0 packs (SFX) · **localization (D8, resolved): English + Hindi (Devanagari) UI in V1, string tables from day one; Hinglish comedy lines in Latin script; regional-language dialogue packs (Marwadi, Bhojpuri, …) are pure data-only adds for any later release**. Engine: **Unity 6 + URP** confirmed (owner requirement 10; no technical reason to change).

## 15. Roadmap (each milestone = playable/testable build)

| # | Milestone | Scope | Effort (solo, full-time) |
|---|---|---|---|
| M0 | **Rules engine** | Pure C# `Rules` lib, unit/property tests, headless CLI runner, deterministic | 1–2 wk |
| M1 | **Gray-box 3D + look-dev slice** | Board/tokens/dice, turn flow, pass-and-play, camera rotation **+ one fully polished visual slice (one corner, one token, one capture) to lock the premium look on a real T1 device** | 3–4 wk |
| M2 | **Identity** | Photo import (picker), crop/mask, photo-on-token, profiles, replace/delete, skip path | 1–2 wk |
| M3 | **Comedy & game feel** | R1 clip import (picker) + trim/tag + playback, **ReactionDirector + starter line library (male/female/child pools)**, token interactions & capture choreography, VFX, sound, menus, save/resume | 4–5 wk |
| M4 | **Bots, onboarding & voice** | 3 bot tiers, tutorial, dialogue library expansion (150+ across male/female/child), **self-recorded VO sessions (male/female/child voice profiles), processing (Audacity), integration**, TTS fallback evaluation | 2–4 wk |
| M5 | **Networking foundation** | `NetTransport` command/event layer, **LAN play** (same Wi-Fi, $0) — validates architecture; **EOS-vs-Nakama evaluation checklist executed (§9.2); D6 finalized here** | 2–3 wk |
| M6 | **Internet play** | EOS (or chosen provider) room codes, commit–reveal, reconnect, **profile & R1-clip sync to room participants** | 3–5 wk |
| M7 | **Hardening & launch** | Device-lab pass (real T1!), thermal/battery, AAB size, compliance, closed testing → soft launch | 2–3 wk |

**Total ≈ 18–28 solo full-time weeks — a planning estimate, not a commitment** (D11). M0–M4 are 100% offline and free; M5 LAN tests networking at $0; M6 uses the chosen free online service.

**Schedule governance (D11, resolved):** development is AI-assisted solo development with a flexible, milestone-based schedule — progress is measured by completed and validated milestones, never hours or dates. Quality is never sacrificed for schedule: if any feature becomes a major schedule or technical risk, it is escalated to the owner, who explicitly decides simplify-vs-hold. Priority order when trade-offs arise: premium visuals · real-face reactions · spoken desi comedy · offline play · online multiplayer — all above an artificially short schedule.

## 16. Risks & contradictions register — NEW in v0.2

| # | Risk / tension | Mitigation |
|---|---|---|
| R1 | **Premium visuals vs $0 art budget** — premium 3D assets usually cost money | Premium look achieved via free tools (Blender), CC0 bases, and craft: lighting/materials/post. Cost is TIME, not money. Look-dev slice in M1 de-risks before mass asset production |
| R2 | **Same art direction on T1 vs premium quality** | Quality-scaling matrix (§8.2) designed in from M1; real ₹8k device tested every milestone |
| R3 | **Unity Relay metered free tier vs $0-forever constraint** | Resolved by D6 (provisional): EOS as working assumption, final verdict at M5; Relay excluded as default; `NetTransport` seam keeps the provider swappable |
| R4 | **"Authoritative" simulation vs no-game-server constraint** | Host-authoritative + commit–reveal dice + signed event log = verifiable fairness for friends-only; strict server-auth = Nakama upgrade path only |
| R5 | **Premium art vs AAB size** | Texture budgets, ASTC per-tier variants, alert at 120 MB, Play Asset Delivery (free) as escape hatch |
| R6 | **Comedy VO vs $0** (voice actors cost money) | Resolved by D14 + A2/A4: self-recorded original VO (owner/friends, $0) organized as **character-profile voice profiles (male/female/child)** — token color never determines voice; players' own voices via imported R1 clips; free system TTS only as coverage fallback, never replacing original assets |
| R7 | **Free cloud hosting idle-reclamation** (if self-hosting: Oracle Always Free reclaims idle 7-day instances) | Avoided by recommending EOS (no servers); if Nakama ever chosen: home server/PAYG-within-limits discussion with owner |
| R8 | **EOS has no contractual free-forever guarantee** (Epic states no intention to monetize) | Transport abstraction + maintained Nakama migration document; revisit if Epic policy changes |
| R9 | Camera + encode + 3D concurrent on T1 | **Post-V1 (A1):** camera reactions removed from V1; the risk moves to the future R2 reintroduction. V1 keeps decode-burst budgeting for R1 clip playback |
| R10 | Solo-dev schedule risk (18–27 wk) | Milestone-gated plan; scope discipline (§3); AI-assisted execution per D11 |
| R11 | **Spoken comedy depends on owner-recorded voice assets** (A4 makes voice primary) | Architecture ships voice-ready with bubble + sting fallback until recordings land — never TTS substitution, never a silent scope-cut of the voice system |
| R12 | **Unity Editor cannot run in the AI build environment** | Unity-side code/configs are authored + statically reviewed here; editor-level validation (play mode, scene baking, device builds) happens on the owner's Unity 6 install; every checkpoint item is labeled **authored** vs **validated** |

## 17. Decision log

| ID | Decision | Status |
|---|---|---|
| D1 | 2v2 team play | ✅ **RESOLVED: postponed** (owner req. 13) |
| D2 | Three consecutive 6s forfeit turn | ✅ **RESOLVED (2026-09-08): yes** — all three rolls void |
| D3 | Capture grants extra roll | ✅ **RESOLVED (2026-09-08): yes** |
| D4 | Blockades | ✅ **RESOLVED (2026-09-08): per-match toggle at setup** — default ON (playtest-tunable); host sets it in online rooms |
| D5 | Match end | ✅ **RESOLVED (2026-09-08): continue for placings** — finish order → tokens home; 2-player matches end at first winner |
| D6 | Online transport | 🔶 **PROVISIONAL (2026-09-08): EOS as working assumption; FINAL AT M5** after real-device evaluation vs self-hosted Nakama (checklist §9.2). Unity Relay excluded as default (metered). $0 apart from Play fee; no recurring paid dependency without owner approval; NetTransport stays provider-agnostic. Fairness locked: host-authoritative + commit–reveal + signed event logs + reconnect + no client-side authority over critical results |
| D7 | Premium art motif | ✅ **RESOLVED (2026-09-08): Festival base + truck-art comedy accents** — festive rangoli/diya/marigold stage; truck-art reserved for comedy surfaces (bubbles, props, UI). Concept refs in §8.1 |
| D8 | UI languages | ✅ **RESOLVED (2026-09-08): English + Hindi (Devanagari) UI in V1** — string tables from day 1; Hinglish dialogue in Latin script; regional dialogue packs = data-only adds later |
| D9 | App name | ✅ **RESOLVED (2026-09-09): "DesiLudo3D" as working title** — final Play Store + trademark check at M7; no blocking collision found; zero architecture impact |
| D10 | Monetization | ✅ **RESOLVED: none in V1** (owner req. 13) |
| D11 | Team & schedule model | ✅ **RESOLVED (2026-09-09): AI-assisted solo development — flexible milestone-based schedule.** Owner = final decision-maker (setup, testing, approvals, asset/voice decisions, device testing); Arena/AI performs as much implementation, architecture, documentation, testing assistance, debugging and iteration as technically possible — the owner does not need to hand-write code. M0–M7 is the technical sequence; calendar estimates are **planning estimates only, not commitments**; progress is measured by completed & validated milestones, not hours. Quality is never silently sacrificed: any major schedule/technical risk is escalated to the owner for an explicit simplify-vs-hold decision. Priority on trade-offs: premium visuals, real-face reactions, spoken desi comedy, offline play, online multiplayer — above an artificially short schedule |
| D12 | Orientation | ✅ **RESOLVED (2026-09-08): both, adaptive** — responsive layouts from day 1; portrait default (pass-and-play preferred); two camera framings; system rotation lock respected (≈+1 wk absorbed across M1–M3) |
| D13 | R2 reaction moments default | ✅ RESOLVED (2026-09-08): on by default — **AMENDED (A1, 2026-09-09): R2 postponed beyond V1; this decision is preserved for its future reintroduction** |
| D14 | Dialogue delivery | ✅ RESOLVED (2026-09-08): bubbles + self-recorded original VO — **AMENDED (A2/A4, 2026-09-09): voice is the PRIMARY comedy channel, bubbles secondary; voices are character-profile based (male/female/child), NEVER token color** (§6.3) |
| **A1** | **Owner amendment: camera reactions removed from V1** | ✅ 2026-09-09 — no camera permission flow, capture, encoding, sharing, cooldowns, or camera privacy UI in V1; R1 (picker-imported prerecorded clips) remains in V1; R3 stays post-V1; modular seam retained |
| **A2** | **Owner amendment: final voice model** | ✅ 2026-09-09 — voice selected by the character profile: male→male, female→female, child→child-appropriate; **token color never determines gender or voice**; multiple voice profiles supported cleanly |
| **A3** | **Owner amendment: female & child dialogue first-class** | ✅ 2026-09-09 — genuine female Hinglish pools (natively written, not male lines with word swaps) + child-appropriate pools; character-specific pools, contextual variants, distinct personality styles |
| **A4** | **Owner amendment: spoken comedy is primary** | ✅ 2026-09-09 — voice/audio first, optional text bubble for readability; V1 comedy is never designed text-only |
| **A5** | **Owner amendment: profile = source of identity** | ✅ 2026-09-09 — the player profile drives character identity, type, voice category, dialogue pool, and visual identity; the real-photo requirement is unchanged; no generic emoji substitution |
| — | Engine | ✅ **RESOLVED: Unity 6 + URP** (owner req. 10) |
| — | Generative AI | ✅ **RESOLVED: none in V1** (owner req. 8) |
| — | Cost policy | ✅ **RESOLVED: $0 services; $25 Play fee only at publish** (owner req. 9) |
| — | Development order | ✅ **RESOLVED: offline core → networking → online → harden** (owner req. 2/14) |

**All decisions D1–D14 and owner amendments A1–A5 are FINAL (2026-09-09). Specification LOCKED — implementation is authorized and in progress (start: M0 rules engine). Progress: `CHECKPOINT.md` · implementation decisions: `docs/IMPLEMENTATION-LOG.md`**

## 18. Audit appendix (pointers)

Full 15-point technical audit delivered in chat (feasible overall; hardest pillars = real-face reactions & internet play; Unity 6 recommended and confirmed; future-AI-ready via interfaces + event-sourced logs). Key external facts verified 2026-09-08: Unity 6 Personal free <$200k/yr no runtime fee; MediaPipe on-device free Apache-2.0; Relay free ≤~50 CCU then metered; EOS free/unmetered; Oracle free-tier idle reclamation; Play API 36 requirement post-Aug-2026.

## 19. Final validation checklist (owner acceptance gate — from the build directive)

**Product:** premium 3D Ludo experience · Indian/desi visual identity · festival + truck-art direction · actual player identity/face on tokens · original comedy · spoken comedy primary · male/female/child voice profiles matched to character profiles · genuinely-represented female dialogue pool · child-appropriate dialogue · **no voice determined by token color** · **camera reactions NOT in V1**.

**Gameplay:** full Ludo rules · D2 (three-sixes void) · D3 (capture extra roll) · D4 (blockade toggle) · D5 (placings) · offline pass-and-play · bots · save/resume.

**UI:** English UI · Hindi/Devanagari UI · portrait · landscape · responsive layouts.

**Online:** private rooms · room codes · 2–4 devices · synchronized gameplay · reconnect · fair dice/event handling · provider abstraction · no prohibited V1 social/matchmaking features.

**Engineering:** rules isolated from Unity presentation · provider abstractions (dice/bot/reaction/transport) · event log · future-AI seams · performance tiers · Android build · automated tests · documentation · checkpoints · git history.

**Cost & privacy:** no mandatory paid AI API · no mandatory paid backend · no generative-AI dependency · no unnecessary analytics SDK · camera capture absent from V1 · privacy documentation consistent with the actual implementation.

---

*End of specification v1.1 — LOCKED. Amendments A1–A5 applied and consistency-checked 2026-09-09.*
