# Trigram Weave Canonical Scheme

Date: 2026-02-04

## Goal
Validate the trigram extraction method described in the trigram grammar progress notes and the analytical overview.

## Method
- Remove line-break markers ("5") and treat each message as a grid of rows.
- Process rows in pairs (0+1, 2+3, ...).
- Weave trigrams left-to-right with alternating orientations:
  - Down: take two from the top row, one from the bottom row.
  - Up: take two from the bottom row, one from the top row.
- Apply per-orientation ordering (clockwise read) as a permutation:
  - Down permutation: 0,1,2 (no change).
  - Up permutation: 1,0,2 (swap the two bottom-row digits).

## Results
- Exactly 83 unique trigram values appear, covering the contiguous range 0-82.
- Trigram counts per message: 99, 103, 118, 102, 137, 124, 119, 120, 114.
- No trigram repeats immediately after itself in any message.
- All starting trigrams are > 26.

## Implementation
- Engine: `NoitaEyes.Engine/TrigramWeaver.cs`, `TrigramScheme.Canonical`.
- Tests: `NoitaEyes.Tests/HypothesisTests.cs` (hypotheses covering the above results).
