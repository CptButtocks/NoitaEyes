# Row-Pair 8 Length-Normalized Decoding

## Goal
Test whether length-normalized position buckets improve decoding accuracy,
especially for longer payloads.

## Method
- Use a stateful mapping keyed by normalized position bucket (relative index
  scaled into 3 buckets) plus prevEast.
- Leave-one-out training over East/West pairs, evaluated on aligned sequences.
- Report accuracy by West length bucket (short/medium/long).

## Results
Overall: pairs=**8**, aligned=**147**, covered=**115**, correct=**26**.
- Bucket 0 (short): covered **0**, aligned **0**, correct **0**
- Bucket 1 (medium): covered **57**, aligned **71**, correct **14** (acc **0.246**)
- Bucket 2 (long): covered **58**, aligned **76**, correct **12** (acc **0.207**)

## Interpretation
Normalized buckets slightly improve coverage and accuracy in the medium
bucket, with little change for the long bucket. This suggests relative
position helps a bit but does not solve late-column ambiguity.

## Next Steps
- Increase bucket granularity (e.g., 4-5 buckets) or use continuous
  interpolation instead of discrete bins.
- Combine normalized buckets with bin constraints or side-specific models.
