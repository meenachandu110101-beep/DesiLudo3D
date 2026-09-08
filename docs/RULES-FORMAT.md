# DesiLudo3D Rules — Cross-Implementation Format & Semantics Contract

> **Authoritative contract** binding the Python reference engine
> (`tools/reference-engine/`) and the canonical C# library
> (`src/DesiLudo3D.Rules/`). Both must read and write **byte-identical** text.
> The conformance vectors (`tests/conformance-vectors/*.vec`) enforce this:
> each records config + commands + exact event log + exact final state +
> state hash, and both implementations must reproduce all of them.

## 1. Board geometry

- 52 loop cells; seat start cells: Red=0, Green=13, Yellow=26, Blue=39.
- Safe cells (8): the four start cells {0,13,26,39} + four stars {8,21,34,47}.
- Token progress: `-1` base · `0..50` loop (seat-relative) · `51..55` private
  home column · `56` finished (centre).
- Global cell of (seat, progress) = `(start + progress) % 52` (loop only).
- Total distance base → home = 56 (exact roll required to finish).

## 2. State text format (`DESI-LUDO-STATE v1`)

```
DESI-LUDO-STATE v1
seats=Red,Yellow                      # config seats, in turn order
blockades=1                           # D4 toggle
threesixes=1                          # D2 toggle
capturex=1                            # D3 toggle
placings=1                            # D5 toggle
twopend=1                             # 2-player ends at first winner
current=Red                           # current seat
phase=AwaitRoll                       # AwaitRoll | AwaitMove | MatchOver
lastdie=0                             # die awaiting a move (0 = none)
consec=0                              # consecutive sixes this turn
rolls=0                               # rolls made this turn
turns=0                               # completed turn transitions
finished=                             # finish order (csv), place = index+1
left=                                 # seats that left (csv, sorted)
progress=Red:-1,-1,-1,-1;Green:...;Yellow:...;Blue:...   # ALL 16 tokens, seat-major
snap=-1,-1,...                        # turn-start snapshot (16 csv) — present only mid-turn
snapfin=                              # snapshot finished order (csv; may be empty)
standings=Red:1:4,Yellow:2:0          # present only when phase=MatchOver
```

Keys are always in this order; missing optional keys are simply absent.
The snapshot is part of logical state (IL-009) — a resumed mid-turn match
must still void correctly on a third consecutive six.

## 3. Command log format (`DESI-LUDO-CMDLOG v1`)

```
DESI-LUDO-CMDLOG v1
roll Red 6            # RollDiceCommand(seat, die) — die value comes from the
move Red 0            #   DiceProvider at the app layer; the engine records it.
leave Yellow          # LeaveMatchCommand(seat)
```

## 4. Event log format (`DESI-LUDO-EVENTLOG v1`)

```
dice <seat> <die> <rollIndex> <wasExtra01>
nomove <seat> <die>
move <seat>:<tokenIndex> <from> <to> <isRelease01>
capture <victimSeat>:<victimIndex> <attackerSeat>:<attackerIndex> <globalCell>
finish <seat>:<tokenIndex>
playerfinish <seat> <place>
voided <seat> <p0>,...,<p15> <finishedCsvOrDash>
turnend <nextSeat> <turnNumber>
left <seat>
end <seat>:<place>:<tokensHome>,...
```

Event order within one applied command is exactly: (roll) `dice` → [`voided` →
`turnend`] | [`nomove` → (`turnend`)] | [await move]; (move) `move` →
`capture`×N → [`finish` → `playerfinish`] → (`turnend` | `end`);
(leave) `left` → (`turnend` | `end`).

## 5. Conformance vector format (`# DESI-LUDO-VECTOR v1`)

```
# DESI-LUDO-VECTOR v1
name=<scenarioName>
players=<2|3|4>
blockades=<01> / threesixes= / capturex= / placings= / twopend=
cmd=<command line>              # repeated, in order
event=<event line>              # repeated — the EXACT expected event stream
state=<one state-format line>   # repeated — the EXACT expected final state
hash=<8-hex-char FNV-1a state hash>
```

## 6. State hash

FNV-1a 32-bit over the UTF-8 bytes of:

```
v1
<seats csv>                        # e.g. Red,Green,Yellow,Blue
<flags>                            # blockades,threeSixes,captureExtra,placings,twoPlayerEnd as 11111
<currentSeat>                      # Red
<phase>                            # AwaitRoll
<lastDie><consec><rolls><turns>    # one per line, decimal
<finished csv>                     # empty line if none
<left csv sorted by seat>
<progress csv (16)>
<snap csv or ->                    # IL-009: snapshot IS hashed
<snapFinished csv or ->
```

(13 newline separators, no trailing newline.)

## 7. Locked micro-semantics (with rationale in docs/IMPLEMENTATION-LOG.md)

- **D2** third consecutive six voids the *entire turn* (snapshot restore);
  toggle off ⇒ never voids. Counter resets on any non-six roll and at turn end.
- **D3** capture grants an extra roll (toggleable). Captures send every
  opponent token on the landing cell home (stacks are captured whole when
  blockades are off).
- **D4** blockade = 2+ tokens of one seat on any loop cell (safe cells
  included); blocks all other seats' passage AND landing; owner never blocked
  by own stacks; home columns can never be blockaded; toggle off ⇒ never blocks.
- **D5** match ends at first winner when placings off or 2-player; otherwise
  when ≤ 1 active player remains. Standings: finish order → tokens-home desc →
  non-leavers above leavers → config seat order (IL-011).
- A **6 always grants its extra roll**, even with no legal move (IL-004);
  finishing a token grants none (IL-005); a player finishing all tokens never
  rolls again.
- **Release** needs a 6 and is blocked if an opponent blockade occupies the
  start cell (blockades on).
- Turn passes automatically on a no-legal-move roll (non-six). With a legal
  move pending, the seat MUST move (no pass command exists).
- Commit–reveal dice (IL-012): value = `SHA256(seed₀‖seed₁‖…)`, first 4 bytes
  big-endian → uint32 → `v % 6 + 1`; seeds in seat order; ≥ 2 seeds required.
