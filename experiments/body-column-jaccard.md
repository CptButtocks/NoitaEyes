# Row-Pair 8 Column Jaccard Overlap (East vs West)

## Goal
Quantify how different East vs West row-pair 8 column value sets are using
Jaccard overlap per column (intersection / union).

## Method
- Use row-pair 8 column sets with full coverage within each group.
- East: coverage = 4 (messages 2,4,6,8).
- West: coverage = 2 (messages 5,7).
- Compute Jaccard overlap for columns 0..14.

## Results
Jaccard overlaps (East vs West):

- 0: 0.000
- 1: 0.000
- 2: 0.000
- 3: 0.000
- 4: 0.200
- 5: 0.250
- 6: 0.000
- 7: 0.250
- 8: 0.000
- 9: 0.000
- 10: 0.000
- 11: 0.333
- 12: 0.000
- 13: 0.000
- 14: 0.000

Only 4/15 columns have non-zero overlap, and the overlaps are small (<= 0.333).

## Interpretation
East and West column sets for row-pair 8 are strongly disjoint. This implies
that the payload band is not just variable but **side-specific**, supporting a
branching encoding rule based on message group.

## Next Steps
- Use these low-overlap columns as discriminators to predict side/motif from
  payload alone (test a simple classifier).
- Explore whether column sets align with the lead trigram or other header cues.
