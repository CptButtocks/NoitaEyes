# Body Column Entropy vs Overlap (Row-Pair 8)

## Goal
Test whether per-column entropy predicts East/West overlap at the column level.

## Method
- Compute per-column entropy from all row-pair 8 sequences.
- Compute East/West column overlap using:
  - Raw values (Jaccard)
  - Coarse global bins (high=8, mid=8)
- Correlate entropy with overlap across columns (0..14).

## Results
- Entropy vs raw overlap correlation: **-0.833**
- Entropy vs binned overlap correlation: **-0.013**
- Columns evaluated: **15**

## Interpretation
Higher entropy columns strongly correlate with **lower raw overlap**, but this
relationship vanishes once values are coarse-binned. That implies the raw
divergence is driven by specific symbol choices rather than distribution-level
structure, reinforcing the idea of side-specific payload with shared coarse
constraints.

## Next Steps
- Compute entropy within East and West separately to see if one side is
  systematically noisier.
- Compare entropy to mode alignment for identifying stable columns.
