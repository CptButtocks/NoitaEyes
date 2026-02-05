# Core Cluster Position Mapping (Weight >= 2)

## Goal
Map where the core transition cluster (largest cluster at weight >= 2) appears
inside each message, including orientation and row-pair distribution.

## Method
- Identify the core cluster via `AnalyzeTransitionClusters(..., minEdgeWeight: 2)`.
- Use `TrigramWeaver.BuildWeaveOccurrences` for row-pair/orientation counts.
- Use `TrigramWeaver.BuildWeavePlacements` for exact glyph coordinates.
- Filter occurrences by the core cluster set and aggregate by message, orientation,
  and row-pair (row-pair = top row index in each 2-row weave block).

## Results
Overall:
- total core occurrences: 834
- down: 421
- up: 413

Per-message counts (message id -> total, down, up):
- id 0: 85 (41 down, 44 up)
- id 1: 93 (48 down, 45 up)
- id 2: 95 (48 down, 47 up)
- id 3: 83 (42 down, 41 up)
- id 4: 107 (53 down, 54 up)
- id 5: 96 (46 down, 50 up)
- id 6: 92 (48 down, 44 up)
- id 7: 93 (47 down, 46 up)
- id 8: 90 (48 down, 42 up)

Row-pair distribution (row pair -> count):
- id 0: 0:25, 2:23, 4:19, 6:18
- id 1: 0:26, 2:26, 4:21, 6:20
- id 2: 0:25, 2:23, 4:17, 6:20, 8:10
- id 3: 0:22, 2:21, 4:23, 6:17
- id 4: 0:23, 2:21, 4:18, 6:21, 8:18, 10:6
- id 5: 0:21, 2:21, 4:18, 6:22, 8:14
- id 6: 0:23, 2:23, 4:16, 6:20, 8:10
- id 7: 0:24, 2:18, 4:22, 6:16, 8:13
- id 8: 0:24, 2:18, 4:21, 6:18, 8:9

Unique core glyph cells per message (unique coordinates):
- id 0: 255
- id 1: 279
- id 2: 285
- id 3: 249
- id 4: 321
- id 5: 288
- id 6: 276
- id 7: 279
- id 8: 270

Row distribution for unique core glyph cells (row -> count):
- id 0: 0:37, 1:38, 2:33, 3:36, 4:28, 5:29, 6:28, 7:26
- id 1: 0:39, 1:39, 2:39, 3:39, 4:32, 5:31, 6:31, 7:29
- id 2: 0:38, 1:37, 2:34, 3:35, 4:27, 5:24, 6:29, 7:31, 8:15, 9:15
- id 3: 0:33, 1:33, 2:32, 3:31, 4:35, 5:34, 6:25, 7:26
- id 4: 0:35, 1:34, 2:33, 3:30, 4:26, 5:28, 6:31, 7:32, 8:26, 9:28, 10:9, 11:9
- id 5: 0:31, 1:32, 2:33, 3:30, 4:26, 5:28, 6:31, 7:35, 8:21, 9:21
- id 6: 0:35, 1:34, 2:33, 3:36, 4:25, 5:23, 6:30, 7:30, 8:17, 9:13
- id 7: 0:36, 1:36, 2:27, 3:27, 4:34, 5:32, 6:24, 7:24, 8:19, 9:20
- id 8: 0:36, 1:36, 2:28, 3:26, 4:31, 5:32, 6:29, 7:25, 8:14, 9:13

East-paired vs West-paired (row pair -> count):
- East-paired (E1..E4): 0:96, 2:90, 4:70, 6:79, 8:38, 10:6 (total 379)
- West-paired (W1..W4): 0:93, 2:86, 4:84, 6:75, 8:27 (total 365)

## Interpretation
Core-cluster trigrams are distributed across all row pairs in each message, with
nearly balanced down/up orientations. The counts per row pair vary by message,
but each message maintains multiple row-pair layers of core transitions, which
supports a structured, woven grammar backbone rather than isolated patterns.

## Next Steps
- Compare row-pair distributions between East-paired vs West-paired groups.
- Map core occurrences to exact row/column glyph positions for visual overlays.
