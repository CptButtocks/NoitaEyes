# Body Column Per-Column Binning (Row-Pair 8)

## Goal
Test whether East/West column distributions align more strongly when each
column is binned by its **own** local frequency profile rather than global
frequencies.

## Method
- Collect row-pair 8 column sequences (base-25 values, 0..24) for all messages.
- For each column index, count local value frequencies across all messages.
- Sort values by frequency (desc, tie-break by value).
- Assign bins **per column**:
  - High = top 2 values
  - Mid = next 2 values
  - Low = remaining values
- Map each column value to its bin and recompute column value sets.
- Compare East vs West column sets (columns 0..14) using Jaccard overlap.
- Track average Jaccard and compare to raw-value Jaccard.

## Results
Binned Jaccard overlaps (East vs West, columns 0..14):
- 0: 0.667
- 1: 0.0
- 2: 0.667
- 3: 0.333
- 4: 0.667
- 5: 0.5
- 6: 0.667
- 7: 0.5
- 8: 0.667
- 9: 0.0
- 10: 0.0
- 11: 0.5
- 12: 0.667
- 13: 0.667
- 14: 0.667

Average overlaps:
- Raw values: 0.069
- Per-column bins: 0.478

Columns with full bin alignment: none  
Columns fully disjoint after binning: 1, 9, 10

## Interpretation
Per-column binning substantially raises East/West overlap relative to raw
values, but does **not** improve average overlap beyond global binning
(still 0.478). This suggests East/West messages share distribution-level
structure, while exact symbol choice remains side-specific. The persistence
of fully disjoint columns (1, 9, 10) indicates localized constraints that
remain strongly side-dependent even after coarse-graining.

## Next Steps
- Evaluate per-column **mode alignment** (top bin only) to identify columns
  with consistent coarse symbols.
- Increase bin sizes (e.g., high=3, mid=3) and see if overlap shifts or
  disjoint columns persist.
