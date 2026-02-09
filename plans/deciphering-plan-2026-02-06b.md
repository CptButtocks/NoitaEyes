# Deciphering Plan (2026-02-06, Rev 2)

## Objective
Convert the strongest structural signals into side-aware decoding constraints,
while controlling for payload length and alignment drift in row-pair 8.

## Evidence Recap
- Canonical weave is stable: 83 unique trigrams (0..82), fixed header tails,
  no adjacent repeats.
- Two header templates exist (motif B vs C) with a variable lead trigram.
- East vs West divergence is subtle at the cell level but strong at row-pair 8
  column values; coarse binning recovers shared structure.
- Row-pair 8 length drives body length and content diversity; row-pair 10 is
  rare (message 4 only).
- Stateful row-pair 8 decoding improves with run position and bucketed keys
  but still fails in late columns.

## Phase 1: Side-Specific Modeling (Highest Priority)
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-side-specific-models.md`
- `experiments/rowpair8-side-conditional-bins.md`

Summary:
- East-only mapping conflicts are high (54/93), West-only is too sparse to
  evaluate, and cross-side transfer yields zero coverage.
- Side-specific bins increase overlap (avg 0.544–0.567) but bin constraints
  reduce decoding coverage (86 covered, 19 correct).

## Phase 2: Length-Controlled Analysis
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-paired-length-buckets.md`
- `experiments/rowpair8-normalized-bucket-sweep.md`

Summary:
- Paired-only buckets are sparse; decoding on the paired subset yields zero
  coverage.
- Normalized bucket sweeps (3/4/5) show nearly identical accuracy (0.224–0.226).

## Phase 3: Alignment Refinement
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-alignment-refinement.md`

Summary:
- Direct, segmented, and bin-aware alignments increase coverage but reduce
  accuracy relative to the Needleman–Wunsch baseline.

## Phase 4: Feature Decomposition (Row-Pair 8)
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-digit-and-hybrid-constraints.md`

Summary:
- Digit overlap is higher than full-value overlap (top 0.328, bottom 0.360).
- Digit-level decoding is substantially stronger than full-value decoding.
- Hybrid digit+bin constraints improve precision but sharply reduce coverage.

## Phase 5: Grammar Modeling (Body-Focused)
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/hmm-headerbody-3-4state.md`
- `experiments/body-only-trigram-templates.md`

Summary:
- 3- and 4-state HMMs remain far worse than the unigram baseline.
- Body-only templates show low coverage and appear early in the body.

## Deliverables (per step)
- Add hypothesis to `NoitaEyes.Tests/HypothesisTests.cs`.
- Add a new experiment write-up in `experiments/` with:
  - Goal, Method, Results, Interpretation, Next Steps.
- Update plan status after completing each phase.

## Immediate Next Actions
1. Draft the next plan focusing on digit-level decoding and hybrid constraint
   strategies that preserve coverage.
2. Consider collecting or generating additional paired messages to reduce
   data sparsity in row-pair 8 analyses.
