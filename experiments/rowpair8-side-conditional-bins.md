# Row-Pair 8 Side-Conditional Bins

## Goal
Test whether side-specific frequency bins improve East/West overlap, and
whether bin constraints help decoding.

## Method
- Build global frequency bins separately for East and West sequences.
- Compute East/West column overlap using side-specific bins.
- Build per-column bins separately for East and West and compute overlap.
- Apply bin constraints during run-position decoding (accept only predictions
  whose bin appears in West training columns).

## Results
- Side-specific global bin overlap (avg Jaccard): **0.544**
- Side-specific per-column bin overlap (avg Jaccard): **0.567**
- Bin-constrained decoding: covered **86**, correct **19**

## Interpretation
Side-specific bins increase overlap beyond the global-bin baseline (0.478),
indicating better normalization of side-specific frequency profiles. However,
bin constraints reduce coverage and do not improve accuracy over the baseline
run-position decoder.

## Next Steps
- Combine bin constraints with digit-level constraints to reduce ambiguity
  while preserving coverage.
- Evaluate side-specific bins on aligned early vs late segments.
