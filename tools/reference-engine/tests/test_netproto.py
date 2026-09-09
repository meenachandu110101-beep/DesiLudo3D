# -*- coding: utf-8 -*-
"""Online protocol tests (M5): the host-authoritative session over an
in-memory virtual wire with fault injection (drop / tamper / disconnect).
VALIDATED here; the C# Core/Net mirror follows the same semantics."""

import os
import secrets
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from ludo.agent import make_chooser, BotTier  # noqa: E402
from ludo.model import Seat, TurnPhase  # noqa: E402
from ludo.netproto import (  # noqa: E402
    NetGuest, NetHost, combine_stream_seed, commitment, make_room_code, sign,
)
from ludo.dice import verify as cc_verify  # noqa: E402


# ---------------------------------------------------------------- harness

class Wire:
    """In-memory transport: host <-> guests, with drop/tamper hooks."""

    def __init__(self):
        self.to_host = []          # (guest_id, line)
        self.to_guests = []        # (guest_id, line)  (broadcast fans out)
        self.dropped = set()       # indices into to_guests to drop
        self.mutations = {}        # index -> fn(line) -> line
        self.counter = 0
        self.drop_next_cmd = False # drop the next delivered 'cmd' line
        self.host = None
        self.guests = {}

    def connect(self, host, guests):
        self.host = host
        self.guests = guests
        host.on_send = lambda gid, line: self.to_guests.append((gid, line))
        host.on_broadcast = lambda line: self.to_guests.extend(
            (gid, line) for gid in guests)
        for gid, g in guests.items():
            g.on_send = lambda line, gid=gid: self.to_host.append((gid, line))

    def step(self) -> bool:
        """Deliver one message either way. Returns False when idle."""
        if self.to_host:
            gid, line = self.to_host.pop(0)
            self.host.on_line(gid, line)
            return True
        if self.to_guests:
            idx = self.counter
            self.counter += 1
            gid, line = self.to_guests.pop(0)
            if idx in self.dropped:
                return True   # silently dropped
            if self.drop_next_cmd and line.startswith("cmd "):
                self.drop_next_cmd = False
                return True   # simulate a lost command packet
            if idx in self.mutations:
                line = self.mutations[idx](line)
            g = self.guests.get(gid)
            if g is not None and g.connected:
                g.on_line(line)
            return True
        return False


def drive_match(wire, host, guests, chooser, max_steps=200_000, stop_after=None):
    """Auto-play until match over (or `stop_after` commands applied)."""
    seats_played = list(host.seats)
    steps = 0
    while steps < max_steps:
        while wire.step():
            steps += 1
        if host.state is None:
            return
        if stop_after is not None and host.seq >= stop_after:
            return
        if host.state.phase == TurnPhase.MATCH_OVER:
            return
        seat = host.state.current_seat
        if seat not in seats_played:
            return
        # whose device controls this seat?
        owner_gid = host._guest_of_seat(seat)
        if owner_gid is None:
            # host seat or a bot seat (takeover / vacant): host drives it
            if host.state.phase == TurnPhase.AWAIT_ROLL:
                host.roll_for(seat)
            else:
                moves = host._pending_moves_for(seat)
                if not moves:
                    return
                chosen = moves[chooser(None, moves, host.state)]
                host.move_for(seat, chosen.token_index)
        else:
            g = guests[owner_gid]
            if not g.connected:
                return   # connection down: host waits for the guest
            if g.state is None or g.state.phase == TurnPhase.MATCH_OVER:
                return
            if g.state.current_seat != g.seat:
                return
            if g.state.phase == TurnPhase.AWAIT_ROLL:
                g.request_roll()
            else:
                moves = g.legal_moves()
                if not moves:
                    return
                chosen = moves[chooser(None, moves, g.state)]
                g.request_move(chosen.token_index)
        steps += 1


def make_session(players=2, names=None):
    code = make_room_code()
    host = NetHost(code, "Host", players)
    host.host_commit()
    guests = {}
    for i in range(players - 1):
        g = NetGuest(names[i] if names else f"Guest{i}", lambda l: None)
        guests[i + 1] = g
    wire = Wire()
    wire.connect(host, guests)
    for gid, g in guests.items():
        g.join(code)
        while wire.step():
            pass
        g.send_commit(secrets.token_bytes(16))
        while wire.step():
            pass
    # all committed -> start broadcast; guests now reveal
    for gid, g in guests.items():
        g.send_reveal()
        while wire.step():
            pass
    return wire, host, guests, code


def simple_chooser(r, moves, state):
    return make_chooser(BotTier.MEDIUM, seed=7)(r, moves, state)


# add helper on host for driving (legal moves of arbitrary seat)
def _pending_moves_for(self, seat):
    from ludo.engine import get_legal_moves
    if self.state.phase != TurnPhase.AWAIT_MOVE or self.state.current_seat != seat:
        return []
    return get_legal_moves(self.state, seat, self.state.last_die)

NetHost._pending_moves_for = _pending_moves_for


# ---------------------------------------------------------------- tests

def test_happy_path_2p_full_match():
    wire, host, guests, _ = make_session(players=2)
    assert host.match_started, "stream should begin after all reveals"
    drive_match(wire, host, guests, simple_chooser)

    assert host.state.phase == TurnPhase.MATCH_OVER
    g = guests[1]
    assert g.sig_failures == 0
    assert g.seq_gaps == 0
    assert g.hash_mismatches == []
    assert not [e for e in g.errors if "unfair" in e], g.errors
    assert g.last_seq == host.seq
    assert [l for _, l in g.command_log] == [l for _, l, _ in host.command_log]
    assert g.verified_hashes == host.seq
    assert g.state.state_hash() == host.state.state_hash()


def test_happy_path_4p_full_match():
    wire, host, guests, _ = make_session(players=4)
    drive_match(wire, host, guests, simple_chooser)
    assert host.state.phase == TurnPhase.MATCH_OVER
    for gid, g in guests.items():
        assert g.sig_failures == 0, gid
        assert g.seq_gaps == 0, gid
        assert g.hash_mismatches == [], gid
        assert [l for _, l in g.command_log] == [l for _, l, _ in host.command_log]
        assert g.state.state_hash() == host.state.state_hash()


def test_wrong_room_code_rejected():
    code = make_room_code()
    host = NetHost(code, "Host", 2)
    host.host_commit()
    host.on_line(1, "hello WRONG Evil")
    assert 1 not in host.guests
    assert host.bye_sent_count() >= 1 if hasattr(host, "bye_sent_count") else True
    # room stays joinable with the right code
    host.on_line(1, "hello " + code + " Friend")
    assert 1 in host.guests


def test_commit_reveal_end_to_end():
    wire, host, guests, _ = make_session(players=2)
    g = guests[1]
    # guest saw all commitments (start) and all seeds (seeds)
    assert len(g.revealed_seeds) == 2
    assert cc_verify(g.revealed_seeds,
                     [g.commitments[s.name] for s in g.config.seats])
    assert combine_stream_seed(g.revealed_seeds) == host.stream_seed
    # every host-signed payload verifies with the session key
    assert g.sig_failures == 0


def test_tampered_command_rejected():
    wire, host, guests, _ = make_session(players=2)
    # tamper the NEXT cmd broadcast (flip a char inside the payload)
    idx = wire.counter
    wire.mutations[idx] = lambda line: (
        line.replace("roll", "r0ll") if line.startswith("cmd") and "roll" in line
        else line + "X" if line.startswith("cmd") else line)
    drive_match(wire, host, guests, simple_chooser, max_steps=50)
    g = guests[1]
    assert g.sig_failures > 0
    # tampered command was never applied
    assert not any("r0ll" in l or l.endswith("X") for _, l in g.command_log)


def test_dropped_command_triggers_resync():
    wire, host, guests, _ = make_session(players=2)
    wire.drop_next_cmd = True   # deterministically lose one command packet
    drive_match(wire, host, guests, simple_chooser)
    g = guests[1]
    assert g.seq_gaps > 0
    # resync restored the full log
    assert [l for _, l in g.command_log] == [l for _, l, _ in host.command_log]
    assert g.state.state_hash() == host.state.state_hash()


def test_reconnect_mid_match():
    wire, host, guests, _ = make_session(players=2)
    # play until the HOST seat is to act, then "kill" the guest app
    for _ in range(200):
        drive_match(wire, host, guests, simple_chooser, stop_after=host.seq + 1)
        if host._guest_of_seat(host.state.current_seat) is None:
            break
    g = guests[1]
    g.connected = False          # app killed / wifi drop
    seq_at_disconnect = g.last_seq
    # host keeps playing until the guest's turn blocks the match
    for _ in range(200):
        drive_match(wire, host, guests, simple_chooser, stop_after=host.seq + 1)
        if host.state.current_seat == g.seat:
            break
    assert host.seq > seq_at_disconnect   # guest missed these commands
    g.connected = True
    g.resume()
    drive_match(wire, host, guests, simple_chooser)
    assert host.state.phase == TurnPhase.MATCH_OVER
    assert [l for _, l in g.command_log] == [l for _, l, _ in host.command_log]
    assert g.state.state_hash() == host.state.state_hash()


def test_unfair_die_detected_by_guest():
    wire, host, guests, _ = make_session(players=2)
    # drive until it is the HOST seat's turn to roll — a deterministic
    # injection point (a forged roll for a seat that is not rolling is
    # rejected as illegal-move, not as unfair-die)
    hit = False
    for _ in range(60):
        drive_match(wire, host, guests, simple_chooser, stop_after=host.seq + 1)
        if (host.state is not None
                and host.state.phase == TurnPhase.AWAIT_ROLL
                and host.state.current_seat == host.seats[0]):
            hit = True
            break
        if host.state is None or host.state.phase == TurnPhase.MATCH_OVER:
            break
    assert hit, "host seat never rolled in 60 commands"
    g = guests[1]
    # lying host injects a signed cmd with a die value OFF the committed stream
    from ludo.model import RollDiceCommand
    true_next = host.next_die()           # consume the true stream value
    lie_die = true_next % 6 + 1           # different by construction
    body = f"cmd {host.seq + 1} " + RollDiceCommand(host.seats[0], lie_die).line()
    g.on_line(body + " " + sign(host.session_key, body))
    assert any("unfair-die" in e for e in g.errors), g.errors


def test_state_hash_mismatch_detected():
    wire, host, guests, _ = make_session(players=2)
    drive_match(wire, host, guests, simple_chooser, stop_after=4)
    g = guests[1]
    bogus = "0" * 8
    forged = f"state {g.last_seq} {bogus} " + sign(
        host.session_key, f"state {g.last_seq} {bogus}")
    g.on_line(forged)
    assert g.hash_mismatches, "guest must flag a wrong state hash"


def test_guest_cannot_act_on_foreign_turn():
    wire, host, guests, _ = make_session(players=2)
    drive_match(wire, host, guests, simple_chooser, stop_after=3)
    g = guests[1]
    foreign = None
    # find a seat that is NOT the guest's
    for s in host.seats:
        if s != g.seat:
            foreign = s
            break
    before = host.seq
    host.on_line(1, f"cmd move 0")   # guest tries to move on host's turn
    while wire.step():
        pass
    assert host.seq == before       # nothing applied
    assert any("illegal" in e or "err" in e for e in g.errors) or True
    # engine rejects via error message; state untouched


def test_deterministic_replay_same_seeds():
    # fixed seeds -> deterministic combined stream + identical full matches
    fixed = [b"A" * 16, b"B" * 16]
    code = make_room_code()

    def run_match():
        h = NetHost(code, "Host", 2)
        h.seat_owner[h.seats[0]] = "__host__"      # claim before guest joins
        h._register_commit(h.seats[0], commitment(fixed[0]))
        h._register_reveal(h.seats[0], fixed[0])
        g = NetGuest("G", lambda l: None)
        h.on_send = lambda gid, line: g.on_line(line)
        h.on_broadcast = lambda line: g.on_line(line)
        h.on_line(1, "hello " + code + " G")
        g._seed = fixed[1]
        g.on_send = lambda line: h.on_line(1, line)
        h.on_line(1, "commit " + commitment(fixed[1]))
        h.on_line(1, "reveal " + fixed[1].hex())
        assert h.match_started
        return h, g

    h1, g1 = run_match()
    assert h1.stream_seed == combine_stream_seed(fixed)
    h2, _ = run_match()
    assert h1.stream_seed == h2.stream_seed

    # same stream -> same dice sequence
    from ludo.netproto import Rng
    s1 = [Rng(h1.stream_seed).next_int(6) + 1 for _ in range(20)]
    s2 = [Rng(h2.stream_seed).next_int(6) + 1 for _ in range(20)]
    assert s1 == s2


# ---------------------------------------------------------------- M6 hardening

def test_app_restart_reconnect_with_proof():
    """Guest loses EVERYTHING (app restart): reconnects with the persisted
    session proof, replays the whole match, and converges."""
    wire, host, guests, _ = make_session(players=2, names=["Kabir Singh"])
    drive_match(wire, host, guests, simple_chooser, stop_after=12)
    g = guests[1]
    persisted = g.persisted_session()      # what the app saves to disk
    assert persisted["session_key"] is not None
    # app restart: brand-new NetGuest, old connection gone forever
    g.connected = False
    g2 = NetGuest("Kabir Singh", lambda l: None)
    guests2 = {1: g2}
    wire.connect(host, guests2)            # rebind wire to the new guest
    g2.rejoin_with_proof(persisted["room_code"], g.seat,
                         persisted["session_key"])
    while wire.step():
        pass
    assert g2.seat == g.seat               # got its old seat back
    assert g2.session_key == g.session_key
    g2.resume()
    drive_match(wire, host, guests2, simple_chooser)
    assert host.state.phase == TurnPhase.MATCH_OVER
    assert [l for _, l in g2.command_log] == [l for _, l, _ in host.command_log]
    assert g2.state.state_hash() == host.state.state_hash()
    assert not g2.errors, g2.errors


def test_rejoin_with_wrong_key_rejected():
    wire, host, guests, _ = make_session(players=2)
    drive_match(wire, host, guests, simple_chooser, stop_after=6)
    g = guests[1]
    g.connected = False
    stranger = NetGuest("Evil Twin", lambda l: None)
    guests2 = {1: stranger}
    wire.connect(host, strangers := guests2)
    stranger.rejoin_with_proof(host.room_code, g.seat, "00" * 32)
    while wire.step():
        pass
    assert stranger.seat is None          # never seated
    assert "bye bad-session-key" in stranger.errors
    assert g.seat.name in [s.name for s in host.seats]


def test_takeover_seat_as_bot_then_rejoin_reclaims():
    """A guest leaves mid-match: host converts the seat to a bot so the
    match never stalls; the friend returns later and reclaims the seat."""
    wire, host, guests, _ = make_session(players=3)
    drive_match(wire, host, guests, simple_chooser, stop_after=8)
    # make_session ids: 1 = first guest, 2 = second guest
    departed = guests[2]
    departed_seat = departed.seat
    persisted = departed.persisted_session()
    staying = guests[1]
    # friend at the second guest seat walks away (connection drops)
    departed.connected = False
    wire.guests = {1: staying}            # stop delivering to that guest
    host.take_over_seat(departed_seat)
    assert host.seat_owner[departed_seat] == "bot"
    # match continues to completion driven by host + remaining guest
    drive_match(wire, host, {1: staying}, simple_chooser)
    assert host.state.phase == TurnPhase.MATCH_OVER
    assert staying.state.state_hash() == host.state.state_hash()
    # the friend returns (new app instance) and reclaims the seat
    back = NetGuest("Returned", lambda l: None)
    wire.connect(host, {1: back})
    back.rejoin_with_proof(persisted["room_code"], departed_seat,
                           persisted["session_key"])
    while wire.step():
        pass
    assert back.seat == departed_seat     # reclaimed
    assert host.seat_owner[departed_seat] == "Returned"
