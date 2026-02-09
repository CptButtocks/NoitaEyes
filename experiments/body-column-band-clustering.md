# Body Column Band Clustering (Row-Pair 8)

## Goal
Detect internal phases in the row-pair 8 payload by clustering columns based
on value-set similarity.

## Method
- Build column value sets from all row-pair 8 sequences (min coverage 2).
- Compute pairwise Jaccard similarity between column value sets.
- Build an undirected graph with edges where Jaccard >= 0.5.
- Extract connected components (clusters).

## Results
- Clusters: **23**
- Largest cluster: **3 columns** → **[14, 23, 26]**
- Threshold: **0.5**

## Interpretation
Column value sets are mostly distinct; only a small 3-column cluster emerges
at Jaccard >= 0.5. This suggests weak grouping structure and minimal evidence
for large, repeated phases in the row-pair 8 payload.

## Next Steps
- Sweep thresholds (0.4, 0.3) to see if larger clusters emerge.
- Compare clusters for East-only and West-only subsets separately.
