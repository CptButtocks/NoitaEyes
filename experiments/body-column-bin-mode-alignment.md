# Body Column Bin Mode Alignment (Row-Pair 8)

## Goal
Check whether East and West columns share the **same dominant bin** after
per-column frequency binning (low/med/high). This isolates coarse column
preferences while ignoring exact glyph-pair values.

## Method
- Use per-column binning (high = top 2 values, mid = next 2, low = rest).
- Map row-pair 8 column values to bins for East and West messages.
- For each column with coverage (East >= 4, West >= 2), compute the mode bin.
- Tie-breaker: choose the **higher** bin (2 > 1 > 0).
- Compare East vs West mode bin per column.

## Results
Matched mode columns: 4, 5, 7, 11  
Mismatched mode columns: 0, 1, 2, 3, 6, 8, 9, 10, 12, 13, 14  

Match rate: **4 / 15 = 0.267**

Per-column mode bins (East-West):
- 0: 2-1
- 1: 2-1
- 2: 1-2
- 3: 2-1
- 4: 2-2
- 5: 2-2
- 6: 0-2
- 7: 2-2
- 8: 2-1
- 9: 2-1
- 10: 2-0
- 11: 2-2
- 12: 0-2
- 13: 0-2
- 14: 1-2

## Interpretation
Only 4 of 15 columns share the same dominant bin between East and West,
even after coarse-graining. This suggests the payload remains strongly
side-specific at the column level, with only a handful of columns showing
consistent coarse preferences.

## Next Steps
- Repeat the mode comparison with alternative bin sizes (e.g., high=3, mid=3).
- Evaluate column-wise **second-mode** alignment to see if East/West diverge
  only on the primary bin or on multiple coarse preferences.
