# Row-Pair 8 Stateful Decode (Bucket Key)

## Goal
Test whether a **bucketed index key** improves decoding by sharing statistics
across nearby columns.

## Method
- Leave-one-out across 8 East/West pairs.
- Align with Needleman–Wunsch.
- Mapping key: `(bucket, east, prevEast)`, where:
  - Bucket 0: index 0–9
  - Bucket 1: index 10–19
  - Bucket 2: index 20+

## Results
- Pairs: **8**
- Aligned: **147**
- Covered: **114**
- Correct: **26**

Bucketed:
- Bucket 0: aligned **67**, covered **54**, correct **16**
- Bucket 1: aligned **64**, covered **58**, correct **10**
- Bucket 2: aligned **16**, covered **2**, correct **0**

## Interpretation
Bucketed keys improve correctness slightly (+2) relative to strict index keys,
especially in bucket 1. This suggests that some signal generalizes across
adjacent columns, but the late columns remain unresolved.

## Next Steps
- Compare bucketed keys with coarse-bin constraints.
- Try adaptive bucket sizes based on entropy rather than fixed index ranges.
