# Body Column Top-2 Bin Alignment (Row-Pair 8)

## Goal
Measure whether East/West columns share **similar top-2 bin preferences**
after per-column binning (high=2, mid=2).

## Method
- Use per-column binning (high = top 2 values, mid = next 2).
- Map row-pair 8 column values to bins.
- For each column (coverage East >= 4, West >= 2):
  - Identify the top-2 bins by frequency.
  - Compare East vs West top-2 bin sets.
  - Compute Jaccard overlap of the top-2 sets.
- Track average Jaccard and columns with full alignment.

## Results
Average top-2 Jaccard: **0.555**

Columns with full top-2 alignment: 4, 5, 7, 9, 11  
Columns with disjoint top-2 sets: none  

Per-column top-2 bins (East-West):
- 0: 2/1 - 1/0
- 1: 2/0 - 1/2
- 2: 1/2 - 2/0
- 3: 2/1 - 1/0
- 4: 2/1 - 2/1
- 5: 2/1 - 2/1
- 6: 0/2 - 2/1
- 7: 2/1 - 2/1
- 8: 2/1 - 1/0
- 9: 2/1 - 1/2
- 10: 2/1 - 0/2
- 11: 2/1 - 2/1
- 12: 0/2 - 2/1
- 13: 0/2 - 2/1
- 14: 1/2 - 2/0

## Interpretation
Top-2 bin alignment is modest (avg 0.555) and only 5 columns fully agree.
This suggests East/West columns share some coarse preferences, but diverge
in their primary or secondary bin choices for most columns.

## Next Steps
- Repeat with larger bins (high=3, mid=3) to check sensitivity.
- Compare alignment of **top-2 vs full set** overlap to see how much signal
  is concentrated in the most frequent bins.
