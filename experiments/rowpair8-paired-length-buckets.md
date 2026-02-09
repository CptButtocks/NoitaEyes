# Row-Pair 8 Paired-Length Buckets

## Goal
Re-evaluate length effects using only paired messages that actually contain
row-pair 8 (E3/W3 and E4/W4).

## Method
- Restrict to ids 4,5,6,7.
- Bucket by row-pair 8 length into 3 buckets.
- For each bucket, compute mean length, entropy, raw overlap, and binned overlap.
- Evaluate run-position decoding accuracy by bucket.

## Results
Bucket overlap:
- b0: n=1, len **22**, entropy **0**, raw **0**, binned **0**
- b1: n=1, len **24**, entropy **0**, raw **0**, binned **0**
- b2: n=2, len **34.5**, entropy **0.718**, raw **0.067**, binned **0.533**

Decoding (paired only):
- pairs **2**, aligned **47**, covered **0**, correct **0**
- b0: aligned **0**, covered **0**, correct **0**
- b1: aligned **20**, covered **0**, correct **0**
- b2: aligned **27**, covered **0**, correct **0**

## Interpretation
With only two paired messages containing row-pair 8, the buckets are too
sparse to support decoding. Overlap patterns match earlier results only in
the longest bucket, where both sides are represented.

## Next Steps
- Reduce bucket count (short/long) or pool additional data if available.
- Try non-parametric alignment or bin-based decoding for the paired subset.
