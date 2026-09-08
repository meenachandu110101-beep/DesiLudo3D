"""Commit–reveal fair dice (spec §9.3 / IL-012).

Each participant generates a secret seed and publishes SHA-256(seed) as a
commitment BEFORE the roll. After all commitments are visible, seeds are
revealed and the die value is derived from SHA-256(seed_0 || seed_1 || ...)
in seat order. No participant can bias the outcome once any other commitment
is fixed, and every participant can verify the result afterwards.
"""
from __future__ import annotations

import hashlib
import secrets
from typing import Iterable, Sequence


def create_seed(nbytes: int = 16) -> bytes:
    return secrets.token_bytes(nbytes)


def commitment(seed: bytes) -> str:
    return hashlib.sha256(seed).hexdigest()


def combine_die_value(seeds: Sequence[bytes]) -> int:
    if len(seeds) < 2:
        raise ValueError("commit-reveal needs at least two seeds")
    h = hashlib.sha256()
    for s in seeds:
        h.update(s)
    value = int.from_bytes(h.digest()[:4], "big")
    return (value % 6) + 1


def verify(seeds: Sequence[bytes], commits: Sequence[str]) -> bool:
    """Verify that revealed seeds match the previously published commitments."""
    if len(seeds) != len(commits):
        return False
    return all(commitment(s) == c for s, c in zip(seeds, commits))
