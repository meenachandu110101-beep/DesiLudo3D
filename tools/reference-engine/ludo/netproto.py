# -*- coding: utf-8 -*-
"""Online play protocol — reference implementation (M5).

Host-authoritative, event-sourced, fair-by-construction:

- The HOST runs the engine (the only rules authority). Guests send INTENTS
  ("roll", "move N"); the host validates, applies, assigns a monotonic
  sequence number, and broadcasts the applied COMMAND (HMAC-signed). Guests
  replay every command locally (deterministic engine) to rebuild state,
  verify their hash against the host's broadcast hash, and animate events.
- FAIR DICE (IL-012, extended): every seat commits SHA-256(secret) before
  the match; after all commitments are visible, secrets are revealed; the
  combined seed = SHA-256(seed_0 ‖ seed_1 ‖ …) in seat order (first 8
  bytes). The die STREAM is splitmix64(combinedSeed): roll i =
  stream.next() % 6 + 1 — every guest regenerates the exact stream and
  verifies each roll command against it (full post-hoc verifiability).
- TAMPER DETECTION: every host→guest message is HMAC-SHA256-signed with a
  session key (derived from the room code); guests additionally verify
  game-state consistency via replay-hash comparison (defense in depth).
- RECONNECT/RESYNC: the host's command log is the source of truth; a guest
  reconnecting with its last sequence number receives exactly the missing
  commands (TCP-agnostic: works over any INetTransport).

Wire format: one line per message (mirrors the other line codecs — trivial
to parse in C#). C# mirror: unity/Assets/_Core/Core/Net/.

Camera rule (A1): this module moves game intents and comedy/identity data
only — no camera anything, ever.
"""

from __future__ import annotations

import hashlib
import hmac as hmac_mod
import secrets
from typing import Callable, Dict, List, Optional, Tuple

from ludo.dice import commitment, verify as cc_verify
from ludo.engine import LegalMove, apply, get_legal_moves, new_match
from ludo.model import (
    Command, GameState, MatchConfig, MoveTokenCommand, RollDiceCommand,
    Seat, TurnPhase, seats_for_player_count,
)

PROTOCOL_VERSION = 1
KEY_LEN_HEX = 64
SEQ_INIT = 0

ROOM_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"   # no I/O/0/1 — readable codes


def make_room_code(rng: Optional["Rng"] = None) -> str:
    r = rng or Rng(secrets.randbits(64))
    return "".join(ROOM_ALPHABET[r.next_int(len(ROOM_ALPHABET))] for _ in range(6))


class Rng:
    """splitmix64 — the SAME stream used by the comedy engine (mirrored
    bit-exactly in C# by Core/Comedy/ComedyDirector.Rng and Core/Net)."""

    __slots__ = ("s",)
    MASK = 0xFFFFFFFFFFFFFFFF

    def __init__(self, seed: int):
        self.s = seed & self.MASK

    def next(self) -> int:
        self.s = (self.s + 0x9E3779B97F4A7C15) & self.MASK
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & self.MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & self.MASK
        return z ^ (z >> 31)

    def next_int(self, n: int) -> int:
        return self.next() % n


def combine_stream_seed(seeds: List[bytes]) -> int:
    """SHA-256(concat seeds in seat order), first 8 bytes big-endian."""
    if len(seeds) < 2:
        raise ValueError("commit-reveal needs at least two seeds")
    h = hashlib.sha256()
    for s in seeds:
        h.update(s)
    return int.from_bytes(h.digest()[:8], "big")


def derive_session_key(room_code: str, nonce_hex: str) -> bytes:
    return hashlib.sha256(("desiludo-session:" + room_code + ":" + nonce_hex)
                          .encode("utf-8")).digest()


def sign(key: bytes, payload: str) -> str:
    return hmac_mod.new(key, payload.encode("utf-8"), hashlib.sha256).hexdigest()


def verify_sig(key: bytes, payload: str, sig: str) -> bool:
    return hmac_mod.compare_digest(sign(key, payload), sig.lower())


# =====================================================================
# host
# =====================================================================

class NetHost:
    """Authoritative session host. Pure (no sockets): the transport feeds
    lines via on_line() and receives via the send/broadcast callbacks."""

    def __init__(self, room_code: str, host_name: str, players: int,
                 blockades: bool = True,
                 on_send: Optional[Callable[[int, str], None]] = None,
                 on_broadcast: Optional[Callable[[str], None]] = None,
                 on_guest_left: Optional[Callable[[int], None]] = None):
        self.room_code = room_code
        self.host_name = host_name
        self.players = players
        self.blockades = blockades
        self.on_send = on_send or (lambda i, l: None)
        self.on_broadcast = on_broadcast or (lambda l: None)
        self.on_guest_left = on_guest_left or (lambda i: None)

        self.seats: List[Seat] = list(seats_for_player_count(players))
        self.guests: Dict[int, Tuple[str, str]] = {}   # guest_id -> (name, seat str)
        self.seat_owner: Dict[Seat, str] = {}          # seat -> 'host' | guest name
        self.next_guest_id = 1

        self.session_nonce = secrets.token_hex(8)
        self.session_key = derive_session_key(room_code, self.session_nonce)

        self.commitments: Dict[Seat, str] = {}
        self.reveals: Dict[Seat, bytes] = {}
        self.match_started = False
        self.state: Optional[GameState] = None
        self.seq = SEQ_INIT
        self.stream: Optional[Rng] = None
        self.acked: Dict[int, int] = {}                # guest_id -> last acked seq
        self.command_log: List[Tuple[int, str, str]] = []   # (seq, line, state hash)
        self.bye_reasons: List[str] = []

    # ---------------------------------------------------------- transport i/o

    def send(self, guest_id: int, line: str):
        self.on_send(guest_id, line)

    def send_signed(self, guest_id: int, kind: str, payload: str):
        sig = sign(self.session_key, kind + " " + payload)
        self.send(guest_id, f"{kind} {payload} {sig}")

    def broadcast(self, line: str):
        self.on_broadcast(line)

    def broadcast_signed(self, kind: str, payload: str):
        sig = sign(self.session_key, kind + " " + payload)
        self.broadcast(f"{kind} {payload} {sig}")

    # ---------------------------------------------------------- lobby

    def on_line(self, guest_id: int, line: str):
        parts = line.strip().split(" ")
        kind = parts[0]

        if kind == "hello":
            if len(parts) < 3:
                self.send(guest_id, "bye malformed")
                return
            code = parts[1]
            if code != self.room_code:
                self.send(guest_id, "bye bad-room-code")
                return
            if self.match_started:
                # mid-match rejoin: require proof (seat + session key) —
                # a fresh stranger cannot enter a running match
                # wire form: hello <code> <seat> <keyhex> <name...>
                # (seat and key at fixed positions; name may contain spaces)
                if len(parts) < 4:
                    self.send(guest_id, "bye room-full")
                    return
                seat_name, key_hex = parts[2], parts[3]
                if key_hex != self.session_key.hex():
                    self.send(guest_id, "bye bad-session-key")
                    return
                try:
                    seat = Seat[seat_name]
                except KeyError:
                    self.send(guest_id, "bye bad-seat")
                    return
                if seat not in self.seats or seat == self._host_seat():
                    self.send(guest_id, "bye bad-seat")
                    return
                # drop any stale binding for this guest id / this seat
                for gid in [g for g, (_, sn) in self.guests.items() if sn == seat.name]:
                    self._drop_guest(gid, "rebind")
                name = " ".join(parts[4:])[:24] or "Player"
                self.guests[guest_id] = (name, seat.name)
                self.seat_owner[seat] = self.guests[guest_id][0]
                self.send(guest_id, f"welcome {seat.value} {self.session_nonce} "
                                    f"{self.session_key.hex()}")
                # replay the match setup so the guest can rebuild its shadow
                commits = " ".join(self.commitments[s] for s in self.seats)
                self.send_signed(guest_id, "start",
                                 f"{self.config_line} {commits}")
                seeds = " ".join(self.reveals[s].hex() for s in self.seats)
                self.send_signed(guest_id, "seeds", seeds)
                return
            if len(self.guests) >= self.players - 1:
                self.send(guest_id, "bye room-full")
                return
            name = " ".join(parts[2:])[:24] or "Player"
            seat = self._free_seat()
            self.guests[guest_id] = (name, seat.name)
            self.seat_owner[seat] = name
            self.send(guest_id, f"welcome {seat.value} {self.session_nonce} "
                                f"{self.session_key.hex()}")
            self.broadcast(f"lobby {len(self.guests) + 1}/{self.players} "
                           + ",".join(n for n, _ in self.guests.values())
                           + ("," + self.host_name))
        elif kind == "commit":
            if guest_id not in self.guests:
                self.send(guest_id, "bye not-in-room")
                return
            seat = Seat[self.guests[guest_id][1]]
            self._register_commit(seat, parts[1])
        elif kind == "reveal":
            if guest_id not in self.guests:
                return
            seat = Seat[self.guests[guest_id][1]]
            self._register_reveal(seat, bytes.fromhex(parts[1]))
        elif kind == "cmd":
            if guest_id not in self.guests:
                self.send(guest_id, "err not-in-room")
                return
            if not self.match_started:
                self.send(guest_id, "err not-started")
                return
            self._on_guest_cmd(guest_id, parts)
        elif kind == "resume":
            if guest_id not in self.guests:
                self.send(guest_id, "bye not-in-room")
                return
            last = int(parts[1]) if len(parts) > 1 else 0
            self._resend_from(guest_id, last)
        elif kind == "ack":
            if len(parts) > 1:
                self.acked[guest_id] = int(parts[1])
        elif kind == "bye":
            self._drop_guest(guest_id, "guest-left")

    def _free_seat(self) -> Seat:
        taken = {s for _, s in self.guests.values()}
        for seat in self.seats:
            if seat.name not in taken and seat not in self.seat_owner:
                return seat
        # unreachable: guarded by room-full
        raise RuntimeError("no free seat")

    def _drop_guest(self, guest_id: int, reason: str):
        if guest_id not in self.guests:
            return
        name, seat_name = self.guests.pop(guest_id)
        seat = Seat[seat_name]
        self.seat_owner.pop(seat, None)
        self.acked.pop(guest_id, None)
        self.on_guest_left(guest_id)
        if not self.match_started:
            self.commitments.pop(seat, None)
            self.reveals.pop(seat, None)
            self.broadcast(f"lobby {len(self.guests) + 1}/{self.players}")

    # ---------------------------------------------------------- commit-reveal

    def host_commit(self) -> Tuple[str, bytes]:
        """Claim the host seat and register its secret (call BEFORE guests
        join, so commitments exist before any reveal can be seen)."""
        seat = self._host_seat()
        self.seat_owner[seat] = "__host__"      # claim before guests join
        seed = secrets.token_bytes(16)
        self._register_commit(seat, commitment(seed))
        self._register_reveal(seat, seed)
        return commitment(seed), seed

    def _host_seat(self) -> Seat:
        for seat in self.seats:
            if self.seat_owner.get(seat) == "__host__":
                return seat
        for seat in self.seats:
            if seat not in self.seat_owner:
                return seat
        return self.seats[0]

    def _register_commit(self, seat: Seat, commit_hex: str):
        if self.match_started or seat in self.commitments:
            return
        self.commitments[seat] = commit_hex
        if self._all_committed():
            self._start_match()

    def _register_reveal(self, seat: Seat, seed: bytes):
        if self.match_started or seat not in self.commitments:
            return
        if seat in self.reveals:
            return
        if commitment(seed) != self.commitments[seat]:
            gid = self._guest_of_seat(seat)
            if gid is not None:
                self.send(gid, f"bye reveal-mismatch {seat.name}")
                self._drop_guest(gid, "reveal-mismatch")
            self.reveals = {}
            self.commitments.pop(seat, None)
            return
        self.reveals[seat] = seed
        if self._all_revealed():
            self._begin_stream()

    def _all_committed(self) -> bool:
        return len(self.commitments) == len(self.seats)

    def _all_revealed(self) -> bool:
        return len(self.reveals) == len(self.seats)

    def _start_match(self):
        cfg = MatchConfig(tuple(self.seats), blockades=self.blockades)
        self.config_line = (f"{len(self.seats)} {int(self.blockades)}")
        commits = " ".join(self.commitments[s] for s in self.seats)
        self.broadcast_signed("start", f"{self.config_line} {commits}")

    def _begin_stream(self):
        seeds = [self.reveals[s] for s in self.seats]
        self.stream_seed = combine_stream_seed(seeds)
        self.stream = Rng(self.stream_seed)
        self.state = new_match(MatchConfig(tuple(self.seats),
                                           blockades=self.blockades))
        self.match_started = True
        self.broadcast_signed("seeds",
                              " ".join(s.hex() for s in seeds))

    # ---------------------------------------------------------- match

    def _seat_of(self, guest_id: int) -> Seat:
        return Seat[self.guests[guest_id][1]]

    def _on_guest_cmd(self, guest_id: int, parts: List[str]):
        seat = self._seat_of(guest_id)
        if parts[1] == "roll":
            self._apply(RollDiceCommand(seat, self.next_die()))
        elif parts[1] == "move":
            self._apply(MoveTokenCommand(seat, int(parts[2])))
        elif parts[1] == "leave":
            self._apply_from_host(_leave_command(seat))
        else:
            self.send(guest_id, "err unknown-cmd")

    def take_over_seat(self, seat: Seat):
        """M6 hardening: a departed guest's seat becomes a host-side bot so
        the match never stalls. The guest can rejoin with proof anytime and
        reclaim the seat."""
        if seat not in self.seats or seat == self._host_seat():
            return
        if seat in self.seat_owner and self.seat_owner[seat] not in ("__host__", "bot"):
            # unbind any live connection for that seat
            for gid in [g for g, (_, sn) in self.guests.items() if sn == seat.name]:
                self._drop_guest(gid, "takeover")
        self.seat_owner[seat] = "bot"

    def roll_for(self, seat: Seat):
        """Roll for ANY seat the host controls (host seat, or a bot seat
        after take_over_seat — M6 hardening). Die always from the fair
        stream."""
        self._apply(RollDiceCommand(seat, self.next_die()))

    def move_for(self, seat: Seat, token_index: int):
        """Move for any seat the host controls."""
        self._apply(MoveTokenCommand(seat, token_index))

    def host_roll(self):
        """Host-seat roll (host device input calls this)."""
        self.roll_for(self._host_seat_live())

    def host_move(self, token_index: int):
        self.move_for(self._host_seat_live(), token_index)

    def _host_seat_live(self) -> Seat:
        # host seat = the seat not owned by a guest (after match start the
        # owner map still tracks joins; on guest drop mid-match the host
        # keeps authority over the vacated seat as a bot — M6 hardening).
        for seat in self.seats:
            if seat not in self.seat_owner or self.seat_owner[seat] == "__host__":
                return seat
        return self.seats[0]

    def mark_host_seat(self):
        self.seat_owner[self._host_seat()] = "__host__"

    def next_die(self) -> int:
        return self.stream.next_int(6) + 1

    def _apply_from_host(self, command: Command):
        self._apply(command)

    def _apply(self, command: Command):
        if self.state is None or not self.match_started:
            return
        res = apply(self.state, command)
        if not res.ok:
            gid = self._guest_of_seat(command.seat) if hasattr(command, "seat") else None
            if gid is not None:
                self.send(gid, f"err illegal {res.error}")
            return
        self.state = res.new_state
        self.seq += 1
        self.command_log.append((self.seq, command.line(), self.state.state_hash()))
        self.broadcast_signed("cmd", f"{self.seq} {command.line()}")
        self.broadcast_signed("state", f"{self.seq} {self.state.state_hash()}")

    def _guest_of_seat(self, seat: Seat) -> Optional[int]:
        for gid, (_, seat_name) in self.guests.items():
            if seat_name == seat.name:
                return gid
        return None

    def _resend_from(self, guest_id: int, last_seq: int):
        """Reconnect resync: replay commands after last_seq from the log."""
        for seq, line, state_hash in self.command_log:
            if seq > last_seq:
                self.send_signed(guest_id, "cmd", f"{seq} {line}")
                self.send_signed(guest_id, "state", f"{seq} {state_hash}")
        self.send(guest_id, f"resynced {self.seq}")

    # The host also records (seq, command line, state hash) for resync:
    # wrapped _apply records into command_log (declared here, filled in
    # __init__ via self.command_log = [] — see below).


def _leave_command(seat: Seat):
    from ludo.model import LeaveMatchCommand
    return LeaveMatchCommand(seat)




# =====================================================================
# guest
# =====================================================================

class NetGuest:
    """Guest session. Pure state machine + SHADOW STATE: replays every
    verified command through the local engine and checks its hash against
    the host's broadcast hash (full consistency verification)."""

    def __init__(self, name: str, on_send: Callable[[str], None]):
        self.name = name
        self.on_send = on_send
        self.room_code = ""
        self.seat: Optional[Seat] = None
        self.session_key: Optional[bytes] = None
        self.config: Optional[MatchConfig] = None
        self.commitments: Dict[str, str] = {}
        self.revealed_seeds: List[bytes] = []
        self.stream: Optional[Rng] = None
        self.state: Optional[GameState] = None
        self.last_seq = SEQ_INIT
        self.command_log: List[Tuple[int, str]] = []
        self.verified_hashes = 0
        self.hash_mismatches: List[str] = []
        self.sig_failures = 0
        self.seq_gaps = 0
        self.errors: List[str] = []
        self.connected = True
        self.roll_count = 0

    # ---------------------------------------------------------- outgoing

    def join(self, room_code: str):
        self.room_code = room_code
        self._send(f"hello {room_code} {self.name}")

    def rejoin_with_proof(self, room_code: str, seat: Seat, session_key_hex: str):
        """Mid-match/app-restart reconnect: prove ownership of the seat via
        the session key received in the original welcome (persisted by the
        app). The host replays the match setup; then send resume 0."""
        self.room_code = room_code
        self._send(f"hello {room_code} {seat.name} {session_key_hex} {self.name}")

    def persisted_session(self) -> dict:
        """Everything needed to reconnect after an app restart."""
        return {
            "room_code": self.room_code,
            "seat": self.seat.name if self.seat else None,
            "session_key": self.session_key.hex() if self.session_key else None,
        }

    def send_commit(self, seed: bytes):
        self._seed = seed
        self._send(f"commit {commitment(seed)}")

    def send_reveal(self):
        self._send(f"reveal {self._seed.hex()}")

    def request_roll(self):
        self._send("cmd roll")

    def request_move(self, token_index: int):
        self._send(f"cmd move {token_index}")

    def request_leave(self):
        self._send("cmd leave")

    def resume(self):
        self._send(f"resume {self.last_seq}")

    def _send(self, line: str):
        if self.connected:
            self.on_send(line)

    # ---------------------------------------------------------- incoming

    def on_line(self, line: str):
        parts = line.strip().split(" ")
        kind = parts[0]

        if kind == "welcome":
            self.seat = Seat(int(parts[1]))
            self.session_key = bytes.fromhex(parts[3])
        elif kind == "lobby":
            pass   # UI update hook
        elif kind == "start":
            if not self._check_sig(parts, kind):
                return
            payload = parts[1:-1]
            players = int(payload[0])
            blockades = payload[1] == "1"
            self.config = MatchConfig(seats_for_player_count(players),
                                      blockades=blockades)
            self.commitments = dict(zip([s.name for s in self.config.seats],
                                        payload[2:2 + players]))
        elif kind == "seeds":
            if not self._check_sig(parts, kind):
                return
            self.revealed_seeds = [bytes.fromhex(h) for h in parts[1:-1]]
            if len(self.revealed_seeds) >= 2:
                self.stream = Rng(combine_stream_seed(self.revealed_seeds))
                self.state = new_match(self.config)
                self.roll_count = 0
        elif kind == "cmd":
            if not self._check_sig(parts, kind):
                return
            seq = int(parts[1])
            cmd_line = " ".join(parts[2:-1])
            self._on_cmd(seq, cmd_line)
        elif kind == "state":
            if not self._check_sig(parts, kind):
                return
            seq, host_hash = int(parts[1]), parts[2]
            if seq > self.last_seq:
                # a state hash for a command we never saw: missed messages —
                # this is the livelock guard (the next cmd may never come if
                # it is now our turn)
                self.seq_gaps += 1
                self.resume()
            elif seq == self.last_seq and self.state is not None:
                if self.state.state_hash() != host_hash:
                    self.hash_mismatches.append(f"seq {seq}")
                else:
                    self.verified_hashes += 1
        elif kind == "resynced":
            pass
        elif kind == "err":
            self.errors.append(" ".join(parts[1:]))
        elif kind == "bye":
            self.errors.append("bye " + " ".join(parts[1:]))
            self.connected = False

    def _check_sig(self, parts: List[str], kind: str) -> bool:
        if self.session_key is None:
            self.sig_failures += 1
            return False
        sig = parts[-1]
        payload = " ".join(parts[:-1])
        if not verify_sig(self.session_key, payload, sig):
            self.sig_failures += 1
            return False
        return True

    def _on_cmd(self, seq: int, cmd_line: str):
        if seq != self.last_seq + 1:
            self.seq_gaps += 1
            self.resume()   # ask the host for the missing range
            return
        command = _parse_command(cmd_line)
        res = apply(self.state, command)
        if not res.ok:
            self.errors.append("shadow-replay-failed " + cmd_line)
            return
        self.state = res.new_state
        self.last_seq = seq
        self.command_log.append((seq, cmd_line))
        if isinstance(command, RollDiceCommand):
            expected = self.stream.next_int(6) + 1
            self.roll_count += 1
            if command.die != expected:
                self.errors.append(
                    f"unfair-die seq {seq}: got {command.die}, stream says {expected}")
        self.on_send(f"ack {seq}")

    # ---------------------------------------------------------- helpers

    def legal_moves(self) -> List[LegalMove]:
        if self.state is None or self.state.phase != TurnPhase.AWAIT_MOVE:
            return []
        return get_legal_moves(self.state, self.state.current_seat,
                               self.state.last_die)

    @property
    def my_turn(self) -> bool:
        return (self.state is not None and self.seat is not None
                and self.state.current_seat == self.seat
                and self.state.phase != TurnPhase.MATCH_OVER)


def _parse_command(line: str) -> Command:
    from ludo.model import parse_command
    return parse_command(line)
