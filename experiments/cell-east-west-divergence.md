# Cell East/West Divergence Map

## Goal
Compare per-cell glyph value sets between East and West message groups to find
positions that strongly discriminate the two sides.

## Method
- Split messages into East ids {0,2,4,6,8} and West ids {1,3,5,7}.
- For each (row, col) cell, collect the set of glyph values seen in each group.
- Restrict to cells with full coverage on both sides (East=5, West=4).
- Compute Jaccard overlap per cell: |E n W| / |E ? W|.

## Results
### Full coverage
- Full-coverage cells: 297

### Jaccard overlap histogram (full coverage)
Jaccard -> count
- 0.000: 2
- 0.200: 17
- 0.250: 31
- 0.333: 10
- 0.400: 25
- 0.500: 75
- 0.600: 14
- 0.667: 51
- 0.750: 31
- 1.000: 41

### Zero-overlap cells (disjoint value sets)
- (7,1): East {2,3} vs West {1}
- (7,17): East {0,1} vs West {2,3}

### Header overlap (rows 0/1, cols 0..8)
Most header cells have Jaccard 1.0 (identical value sets).
The variable lead cells show partial overlap:
- (0,0): 0.333
- (0,1): 0.667
- (1,0): 0.600

## Interpretation
East and West messages share most per-cell value sets; divergence is subtle and
localized. Only two full-coverage cells are fully disjoint between sides, and
these lie in row 7 (row-pair 6), not in the header. The header scaffold is
largely shared, with variability concentrated in the lead cells, consistent
with the earlier header-template findings.

## Next Steps
- Use the most divergent cells as features in a simple classifier to predict
  East vs West from the glyph grid alone.
- Overlay the zero-overlap cells on the body band layout to see whether they
  align with row-pair 8 expansion boundaries or other structural markers.
