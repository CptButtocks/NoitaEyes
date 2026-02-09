# Header Lead vs Payload Bin Composition

## Goal
See if lead trigram values predict coarse bin composition in row-pair 8
payloads.

## Method
- Build global frequency bins for row-pair 8 values (high=8, mid=8).
- Sort messages by lead trigram and split into three buckets.
- For each bucket, compute:
  - Average bin value
  - Fraction of high-bin values

## Results
- bucket 0 (low leads): avgBin **1.328**, highFrac **0.478**
- bucket 1 (mid leads): avgBin **1.417**, highFrac **0.550**
- bucket 2 (high leads): avgBin **1.333**, highFrac **0.542**

## Interpretation
Bucketed bin composition varies only modestly with lead trigram values. The
middle-lead bucket is slightly more weighted toward high-frequency symbols,
but the difference is small.

## Next Steps
- Repeat bin composition analysis inside East-only and West-only subsets.
- Test if lead-trigram buckets better explain early vs late column differences.
