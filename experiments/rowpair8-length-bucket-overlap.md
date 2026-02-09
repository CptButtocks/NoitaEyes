# Row-Pair 8 Length Bucket Overlap

## Goal
Check whether short/medium/long row-pair 8 payloads show different overlap,
entropy, or bin alignment behavior.

## Method
- Extract row-pair 8 column sequences for messages with non-zero length.
- Sort by length and split into three buckets (short/medium/long).
- For each bucket, compute:
  - Mean length
  - Average column entropy
  - East/West raw Jaccard overlap (minCoverage=1)
  - East/West coarse-bin overlap (high=8, mid=8)
  - East/West bin-mode match rate (per-column bins, high=2, mid=2)

## Results
- Bucket 0 (short): n=2 (E=2, W=0)
  - meanLen **18**, entropy **0.619**
  - raw overlap **0**, binned overlap **0**, mode match **0**
- Bucket 1 (medium): n=2 (E=1, W=1)
  - meanLen **23**, entropy **0.875**
  - raw overlap **0.045**, binned overlap **0.364**, mode match **1.0**
- Bucket 2 (long): n=2 (E=1, W=1)
  - meanLen **34.5**, entropy **0.718**
  - raw overlap **0.067**, binned overlap **0.533**, mode match **1.0**

## Interpretation
The short bucket contains no West messages, so East/West overlap cannot be
assessed there. Medium/long buckets still show low raw overlap but noticeably
higher overlap after coarse binning. With only one East/West message each,
mode matches are trivially high, so this result is not yet conclusive.

## Next Steps
- Rebucket using only West+East pairs to avoid empty-side buckets.
- Repeat with additional messages or alternative length thresholds.
