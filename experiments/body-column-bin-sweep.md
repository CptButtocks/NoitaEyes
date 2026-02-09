# Body Column Coarse Bin Sweep (Row-Pair 8)

## Goal
Check how East/West overlap changes when we vary **global** bin sizes for
row-pair 8 column values.

## Method
- Collect row-pair 8 column sequences (values 0..24).
- Compute global value frequencies across all row-pair 8 columns.
- Assign bins based on global ranks:
  - High = top `H` values
  - Mid = next `M` values
  - Low = remaining values
- Test bin sizes `(H,M)` = (3,3), (4,4), (5,5).
- For each binning:
  - Map values to bins.
  - Compute East/West column sets (columns 0..14).
  - Calculate average Jaccard overlap across columns.
- Compare to the raw-value average Jaccard.

## Results
Average Jaccard overlap:
- Raw values: **0.069**
- Bins (3,3): **0.533**
- Bins (4,4): **0.456**
- Bins (5,5): **0.511**

## Interpretation
Coarse binning consistently increases East/West overlap, confirming shared
distribution-level structure. The overlap does not monotonically increase
with larger bins, suggesting there is a “sweet spot” where coarse symbols
capture shared structure without collapsing too much.

## Next Steps
- Use per-column binning for a local view of overlap sensitivity.
- Compare these overlaps to top-2 bin alignment (not just set overlap).
