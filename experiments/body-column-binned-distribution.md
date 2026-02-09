# Body Column Coarse-Binned Distributions (Row-Pair 8)

## Goal
Test whether East/West row-pair 8 column distributions become more similar
after coarse-graining glyph-pair values into frequency bins.

## Method
- Collect row-pair 8 column sequences (base-25 values, 0..24) for all messages.
- Compute global value frequencies across all row-pair 8 columns.
- Sort values by frequency (desc, tie-break by value).
- Assign bins:
  - High = top 8 values
  - Mid = next 8 values
  - Low = remaining 9 values
- Map each column value to its bin and recompute column value sets.
- Compare East vs West column sets (columns 0..14) using Jaccard overlap.
- Compute average Jaccard across columns and compare to raw-value Jaccard.

## Results
Frequency bins:
- High: 0, 1, 2, 4, 5, 6, 10, 14
- Mid: 8, 11, 12, 13, 15, 17, 21, 22
- Low: 3, 7, 9, 16, 18, 19, 20, 23, 24

Binned Jaccard overlaps (East vs West, columns 0..14):
- 0: 0.333
- 1: 0.333
- 2: 0.667
- 3: 0.0
- 4: 0.333
- 5: 0.333
- 6: 0.5
- 7: 0.5
- 8: 0.667
- 9: 1.0
- 10: 0.0
- 11: 0.5
- 12: 1.0
- 13: 0.333
- 14: 0.667

Average overlaps:
- Raw values: 0.069
- Binned values: 0.478

## Interpretation
Coarse-graining by frequency increases East/West overlap substantially
(avg Jaccard 0.069 -> 0.478), indicating that the two sides share
distribution-level structure even when exact glyph-pair values diverge.
Some columns (9, 12) align completely at the bin level, while others
remain disjoint (3, 10), suggesting side-specific payload with weak
global constraints.

## Next Steps
- Compare per-column bin **mode** alignment (East vs West) to identify
  columns with consistent coarse symbols.
- Repeat binning using per-column frequencies instead of global counts
  to see if local structure tightens further.
