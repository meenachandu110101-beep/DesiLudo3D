# D6 — Final Transport Decision (M5)

Status: **DECIDED for V1** — self-hosted P2P (LAN / hotspot / direct IP)
behind the provider-agnostic `NetTransport` seam. EOS Relay and self-hosted
Nakama remain documented drop-ins for post-V1, not V1 dependencies.

## The locked evaluation criteria (Master Spec D6, unchanged)

1. **$0 mandatory** development AND service cost (no recurring paid backend).
2. Friends-only private rooms, 2–4 Android devices, no public matchmaking.
3. Host-authoritative rules, commit–reveal fair dice, signed event logs,
   reconnect — protocol-level guarantees, not service-level.
4. Transport must stay **provider-agnostic** (`INetTransport` seam; gameplay
   code never sees sockets or SDKs).
5. Unity Relay service was **excluded as the default** from the start.
6. EOS was the *provisional* assumption, to be finalized at M5 against
   self-hosted Nakama.

## Candidate evaluation

| Criterion | EOS (UGS Relay) | Self-hosted Nakama | Self-hosted P2P (chosen) |
|---|---|---|---|
| $0 today | Free tier, no card | OSS server, but needs a VM | **$0 forever** |
| $0 risk if terms change | **Real** — no free-forever contract; a pricing/policy change would break a shipped game | VM cost or Oracle Always Free (documented reclamation risk) | **None** (no third party exists) |
| Privacy (spec §12: no analytics) | Adds UGS SDK + cloud account surface | Game traffic transits our server | **No servers at all** — packets stay on the local network |
| Solo-dev ops burden | Low but account/project coupled | **High** (deploys, TLS, updates, DDoS) | **None** |
| Validatable in this environment | No (SDK needs editor/device) | No (needs a live server) | **Yes** — protocol fully validated (98/98 tests) |
| Covers the dominant use case | Yes, plus NAT'd internet | Yes, plus NAT'd internet | **Yes** — same Wi-Fi or one phone's **hotspot** (the classic desi-family setup); direct IP works whenever the host is reachable |
| Internet play across arbitrary NATs | Yes | Yes | Only when host reachable (port-forward / public IP) |

## Decision and rationale

**V1 ships self-hosted P2P.** It is the only option with zero third-party
risk under the $0 mandate, zero privacy surface, zero ops burden — and it is
the only option whose entire protocol could be *executed and validated* in
this environment. The friends-only 2–4 player scope is dominated by
same-room/hotspot play; room codes keep rooms private; the protocol itself
(VALIDATED in `tools/reference-engine/ludo/netproto.py` + tests: 98/98 total
green) delivers every required guarantee regardless of carrier:

- **Host-authoritative**: only the host runs the rules; guests send intents.
- **Fair dice**: per-match commit–reveal (SHA-256 commitments → revealed
  seeds → combined stream seed → splitmix64 stream); every guest regenerates
  and verifies each roll; a lying host is *detected by construction*.
- **Signed event logs**: HMAC-SHA256 session key on every host→guest message,
  plus independent shadow-replay state-hash verification (defense in depth).
- **Reconnect**: the host's command log is the resync source; a guest that
  reconnects with its last sequence number receives exactly what it missed.
- **Failure handling**: dropped commands are detected via sequence gaps AND
  state-hash gaps (the livelock guard found by the fault-injection tests);
  tampered/mismatched messages are rejected and flagged.

**EOS Relay (post-V1 option):** if real-device testing (owner checklist M5)
shows that friends need to play across arbitrary NATs (different cities over
the public internet), EOS Relay slots in behind `INetTransport` /
`NetHostSession.Send/Broadcast` — a new transport class only, zero gameplay
changes. That integration is deliberately NOT in V1: it adds a cloud
dependency the $0 mandate treats as risk, and it cannot be validated without
editor/device access.

**Nakama (rejected):** server hosting cost/risk and solo-dev ops burden with
no benefit over P2P for the friends-only scope.

## What this means for "online V1"

V1 is genuinely online: 2–4 Android devices, private code rooms, live
synchronized play, reconnect, and fairness verification — over LAN, one
hotspot, or direct IP. Not offline-only, and not dependent on any service
that could start charging.
