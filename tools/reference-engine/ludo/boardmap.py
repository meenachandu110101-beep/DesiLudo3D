"""Board geometry map (15x15 classic Ludo grid) — presentation coordinate data.

This is the single canonical table for turning rules-space (seat, progress)
into grid coordinates. It is mirrored in C# (DesiLudo3D.Core.BoardGridMap);
this module validates the table's internal consistency so the C# mirror is
built on verified data, not on hope.

Grid: (x, y) with x = column 0..14 (left->right), y = row 0..14 (top->bottom).
World conversion happens in the Unity layer only.
"""
from __future__ import annotations

from typing import Dict, List, Tuple

from ludo.model import (
    LOOP_CELLS, PROG_BASE, PROG_COLUMN_FIRST, PROG_FINISHED,
    PROG_LOOP_MAX, SAFE_CELLS, Seat, start_cell,
)

GridPt = Tuple[int, int]

# 52 loop cells, clockwise, index = global cell id.
# Index 0 = Red's start cell (1,6); seat starts: Red=0, Green=13, Yellow=26, Blue=39.
LOOP: List[GridPt] = [
    (1, 6), (2, 6), (3, 6), (4, 6), (5, 6),          #  0.. 4  Red's outward run
    (6, 5), (6, 4), (6, 3), (6, 2), (6, 1),          #  5.. 9  up the left arm
    (6, 0),                                            # 10
    (7, 0),                                            # 11
    (8, 0),                                            # 12
    (8, 1), (8, 2), (8, 3), (8, 4), (8, 5),          # 13..17  Green's outward run
    (9, 6), (10, 6), (11, 6), (12, 6), (13, 6),      # 18..22
    (14, 6),                                           # 23
    (14, 7),                                           # 24
    (14, 8),                                           # 25
    (13, 8), (12, 8), (11, 8), (10, 8), (9, 8),      # 26..30  Yellow's outward run
    (8, 9), (8, 10), (8, 11), (8, 12), (8, 13),      # 31..35
    (8, 14),                                           # 36
    (7, 14),                                           # 37
    (6, 14),                                           # 38
    (6, 13), (6, 12), (6, 11), (6, 10), (6, 9),      # 39..43  Blue's outward run
    (5, 8), (4, 8), (3, 8), (2, 8), (1, 8),          # 44..48
    (0, 8),                                            # 49
    (0, 7),                                            # 50  Red's column entrance
    (0, 6),                                            # 51
]

# Home columns: 5 cells per seat, progress 51..55, leading to the centre.
HOME_COLUMNS: Dict[Seat, List[GridPt]] = {
    Seat.RED:    [(1, 7), (2, 7), (3, 7), (4, 7), (5, 7)],
    Seat.GREEN:  [(7, 1), (7, 2), (7, 3), (7, 4), (7, 5)],
    Seat.YELLOW: [(13, 7), (12, 7), (11, 7), (10, 7), (9, 7)],
    Seat.BLUE:   [(7, 13), (7, 12), (7, 11), (7, 10), (7, 9)],
}

# Base (yard) slot grid coordinates — 4 slots inside each 6x6 corner.
BASE_SLOTS: Dict[Seat, List[GridPt]] = {
    Seat.RED:    [(1, 1), (4, 1), (1, 4), (4, 4)],
    Seat.GREEN:  [(10, 1), (13, 1), (10, 4), (13, 4)],
    Seat.YELLOW: [(10, 10), (13, 10), (10, 13), (13, 13)],
    Seat.BLUE:   [(1, 10), (4, 10), (1, 13), (4, 13)],
}

# Board-corner camera azimuths (degrees) so each seat's corner faces them.
# Camera at angle a sits at (sin a * d, h, cos a * d) around the board centre.
SEAT_AZIMUTH_DEG: Dict[Seat, float] = {
    Seat.RED: 225.0, Seat.GREEN: 135.0, Seat.YELLOW: 45.0, Seat.BLUE: 315.0,
}

GRID_SIZE = 15


def position(seat: Seat, progress: int) -> GridPt:
    """Grid coordinate of a token. Raises for invalid progress."""
    if progress == PROG_BASE:
        raise ValueError("base tokens have no loop position; use BASE_SLOTS")
    if 0 <= progress <= PROG_LOOP_MAX:
        return LOOP[(start_cell(seat) + progress) % LOOP_CELLS]
    if PROG_COLUMN_FIRST <= progress < PROG_FINISHED:
        return HOME_COLUMNS[seat][progress - PROG_COLUMN_FIRST]
    if progress == PROG_FINISHED:
        return (7, 7)  # centre
    raise ValueError("invalid progress: %d" % progress)


def _adjacent(a: GridPt, b: GridPt) -> bool:
    return abs(a[0] - b[0]) + abs(a[1] - b[1]) == 1


def _diagonal(a: GridPt, b: GridPt) -> bool:
    return abs(a[0] - b[0]) == 1 and abs(a[1] - b[1]) == 1


# The classic 15x15 Ludo track makes exactly four DIAGONAL corner hops where
# the path turns around the centre block: e.g. (5,6) -> (6,5). This is the
# standard mapping artifact of the physical board (verified against the
# canonical layout: bases 6x6 at corners, runways row/col 7, centre 3x3,
# starts spaced 13 apart, entrances at start+50 adjacent to runways).
DIAGONAL_CORNER_STEPS = {4: 5, 17: 18, 30: 31, 43: 44}


def self_check() -> None:
    """Raise AssertionError if the tables are internally inconsistent."""
    assert len(LOOP) == LOOP_CELLS, "loop must have 52 cells"
    assert len(set(LOOP)) == LOOP_CELLS, "loop cells must be unique"
    for i in range(LOOP_CELLS):
        a, b = LOOP[i], LOOP[(i + 1) % LOOP_CELLS]
        if i in DIAGONAL_CORNER_STEPS:
            assert DIAGONAL_CORNER_STEPS[i] == (i + 1) % LOOP_CELLS
            assert _diagonal(a, b), \
                "loop corner %d->%d must be the diagonal hop: %r %r" % (i, (i + 1) % LOOP_CELLS, a, b)
        else:
            assert _adjacent(a, b), \
                "loop cells %d->%d not adjacent: %r %r" % (i, (i + 1) % LOOP_CELLS, a, b)
    # safe cells = the four start cells + four star cells (engine rule)
    for seat in Seat:
        assert start_cell(seat) in (0, 13, 26, 39)
    assert frozenset(start_cell(s) for s in Seat) | \
        frozenset((start_cell(s) + 8) % LOOP_CELLS for s in Seat) == SAFE_CELLS, \
        "map star/start cells must match engine SAFE_CELLS"
    # home columns: 5 cells each, adjacent chain, first cell adjacent to the
    # column entrance (start + 50); last cell adjacent to the centre block
    # edge (the 3x3 centre is (6..8)^2; finished tokens render inside it)
    centre_edge = {
        Seat.RED: (6, 7), Seat.GREEN: (7, 6), Seat.YELLOW: (8, 7), Seat.BLUE: (7, 8),
    }
    for seat in Seat:
        col = HOME_COLUMNS[seat]
        assert len(col) == 5
        for i in range(4):
            assert _adjacent(col[i], col[i + 1]), "%s column break at %d" % (seat, i)
        entrance = LOOP[(start_cell(seat) + 50) % LOOP_CELLS]
        assert _adjacent(entrance, col[0]), \
            "%s entrance %r not adjacent to column start %r" % (seat, entrance, col[0])
        assert _adjacent(col[4], centre_edge[seat]), \
            "%s column must reach the centre block edge" % seat
    # base slots inside each 6x6 corner quadrant
    quadrant_x = {Seat.RED: range(0, 6), Seat.GREEN: range(9, 15),
                  Seat.YELLOW: range(9, 15), Seat.BLUE: range(0, 6)}
    quadrant_y = {Seat.RED: range(0, 6), Seat.GREEN: range(0, 6),
                  Seat.YELLOW: range(9, 15), Seat.BLUE: range(9, 15)}
    for seat, slots in BASE_SLOTS.items():
        assert len(slots) == 4
        for (x, y) in slots:
            assert x in quadrant_x[seat] and y in quadrant_y[seat], \
                "%s base slot %r outside quadrant" % (seat, (x, y))
    # position() consistency for a full walk of every seat
    for seat in Seat:
        for p in range(0, PROG_LOOP_MAX + 1):
            x, y = position(seat, p)
            assert 0 <= x < GRID_SIZE and 0 <= y < GRID_SIZE
        for p in range(PROG_COLUMN_FIRST, PROG_FINISHED):
            assert position(seat, p) == HOME_COLUMNS[seat][p - PROG_COLUMN_FIRST]
        assert position(seat, PROG_FINISHED) == (7, 7)
