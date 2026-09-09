# -*- coding: utf-8 -*-
"""Bot brains (M4) — tiered heuristic opponents.

Tiers:
- EASY   : uniform random choice (fun, beatable).
- MEDIUM : greedy on the core score (captures / finishes / releases / stars)
           with 25% random noise — no danger awareness.
- HARD   : full score including threat terms (avoid landing where an
           opponent can capture within one roll; reward escaping threats)
           with 5% noise.

Deterministic per seed. This module is the VALIDATED reference; the C#
Core HeuristicAgent is its mirror (same scoring constants).
"""

import random
from enum import Enum
from typing import List

from ludo.engine import LegalMove
from ludo.model import (
    PROG_BASE, PROG_FINISHED, PROG_LOOP_MAX, LOOP_CELLS, GameState,
    Seat, TokenState, global_cell, is_safe_cell,
)


class BotTier(Enum):
    EASY = 0
    MEDIUM = 1
    HARD = 2


def threat_count(state: GameState, seat: Seat, cell: int) -> int:
    """How many opponent tokens could capture a token on `cell` with a 1..6
    roll (loop proximity only — blockades/entrances ignored; a heuristic,
    not an oracle)."""
    if cell < 0:
        return 0
    count = 0
    for t in state.tokens:
        if t.seat == seat or t.progress < 0 or t.progress > PROG_LOOP_MAX:
            continue
        oc = global_cell(t.seat, t.progress)
        for k in range(1, 7):
            if (oc + k) % LOOP_CELLS == cell:
                count += 1
                break
    return count


def base_score(state: GameState, seat: Seat, move: LegalMove) -> float:
    """Core preferences shared by MEDIUM and HARD."""
    score = 0.0
    if move.captures:
        score += 100.0 * len(move.victims)
    if move.to == PROG_FINISHED:
        score += 90.0
    if move.is_release:
        score += 40.0
    if 0 <= move.to <= PROG_LOOP_MAX and is_safe_cell(global_cell(seat, move.to)):
        score += 25.0
    if move.to > PROG_LOOP_MAX and move.to != PROG_FINISHED:
        score += 15.0        # home column: immune
    score += 2.0 * move.to   # advance the leader
    return score


def full_score(state: GameState, seat: Seat, move: LegalMove) -> float:
    """base_score + threat awareness (HARD only)."""
    score = base_score(state, seat, move)
    if 0 <= move.to <= PROG_LOOP_MAX:
        to_cell = global_cell(seat, move.to)
        if not is_safe_cell(to_cell):
            score -= 20.0 * threat_count(state, seat, to_cell)      # landing in danger
    if 0 <= move.frm <= PROG_LOOP_MAX:
        frm_cell = global_cell(seat, move.frm)
        if not is_safe_cell(frm_cell):
            score += 10.0 * threat_count(state, seat, frm_cell)     # escaping danger
    return score


def make_chooser(tier: BotTier, seed: int = 0):
    """Chooser compatible with simulation.play_random_match."""
    rng = random.Random(seed * 7919 + 17)

    def chooser(r: random.Random, moves: List[LegalMove], state: GameState) -> int:
        if len(moves) == 1:
            return 0
        if tier == BotTier.EASY:
            return rng.randrange(len(moves))

        eps = 0.25 if tier == BotTier.MEDIUM else 0.05
        if rng.random() < eps:
            return rng.randrange(len(moves))

        seat = state.current_seat
        score_fn = base_score if tier == BotTier.MEDIUM else full_score
        best_i, best_s = 0, float("-inf")
        for i, m in enumerate(moves):
            s = score_fn(state, seat, m) + rng.random() * 0.01   # stable tiebreak
            if s > best_s:
                best_i, best_s = i, s
        return best_i

    return chooser


def tier_for_seat(seat: Seat) -> BotTier:
    """Demo mapping for tier-strength tests: seat 0 = HARD, others EASY."""
    return BotTier.HARD if int(seat) == 0 else BotTier.EASY
