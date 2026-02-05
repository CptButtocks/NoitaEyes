# Body Segmentation: Row-Pair Profiles

## Goal
Segment each message into header/body using the motif anchors, then test whether
the body (indices 6..end) clusters by row-pair profiles or reveals shared
structure.

## Method
- Use the motif-segmented header rule (header length = 6).
- For each message, count row-pair occurrences in the body.
- Compute L1 distances between per-message row-pair count vectors.
- Inspect nearest-neighbor relationships and motif-group alignment.

## Body row-pair counts (per message)
Format: rowPair:count.

- 0: 0:20, 2:26, 4:26, 6:21
- 1: 0:20, 2:26, 4:26, 6:25
- 2: 0:20, 2:26, 4:26, 6:26, 8:14
- 3: 0:20, 2:26, 4:26, 6:24
- 4: 0:20, 2:26, 4:26, 6:26, 8:26, 10:7
- 5: 0:20, 2:26, 4:26, 6:26, 8:20
- 6: 0:20, 2:26, 4:26, 6:26, 8:15
- 7: 0:20, 2:26, 4:26, 6:26, 8:16
- 8: 0:20, 2:26, 4:26, 6:26, 8:10

Shared core:
- Row-pairs 0, 2, 4 are fixed across all bodies (20/26/26).
- Row-pair 6 varies (21..26).
- Row-pair 8 appears in 6 messages; row-pair 10 appears only in message 4.

## Nearest neighbors (L1 distance on row-pair counts)
- 0 -> 3
- 1 -> 3
- 2 -> 6
- 3 -> 1
- 4 -> 5
- 5 -> 7
- 6 -> 2
- 7 -> 6
- 8 -> 2

Only 3/9 nearest-neighbor pairs stay within the same header motif group,
so the body row-pair profile does not cleanly cluster by header type.

## Interpretation
The body has a strong shared backbone (row-pairs 0/2/4), with variability driven
by row-pairs 6/8/10. Motif-B messages show higher row-pair 8 counts overall, but
the nearest-neighbor structure cuts across motif groups, suggesting the body is
not determined solely by the header type.

## Next Steps
- Segment the body further by scanning for secondary anchors or repeated motifs.
- Build a row-pair "rhythm" model: compare the sequence of row-pair indices, not
  just aggregate counts.
- Check whether row-pair 8/10 emergence correlates with message length or other
  global message properties (sum, unique count).
