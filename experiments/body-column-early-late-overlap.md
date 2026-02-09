# Body Column Early vs Late Overlap (Row-Pair 8)

## Goal
Compare East/West overlap between early columns (0–9) and late columns (10–14).

## Method
- Compute East/West column overlaps using:
  - Raw values (Jaccard)
  - Coarse global bins (high=8, mid=8)
- Average overlaps for early columns (0..9) vs late columns (10..14).

## Results
Average overlap:
- Raw early: **0.070**
- Raw late: **0.067**
- Binned early: **0.467**
- Binned late: **0.500**

## Interpretation
Raw overlaps are uniformly low in both early and late columns. Binned overlap
is slightly higher in late columns, suggesting coarse structure persists
throughout the payload band rather than being concentrated in the prefix.

## Next Steps
- Repeat using per-column binning to see if early/late divergence changes.
- Cross-check against column entropy to see if low-overlap columns are also
  the highest-entropy columns.
