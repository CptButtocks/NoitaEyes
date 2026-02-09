# Row-Pair 8 Length Bucket Decoding Accuracy

## Goal
Evaluate whether row-pair 8 decoding accuracy varies with payload length.

## Method
- Build a leave-one-out stateful mapping keyed by run position and prevEast.
- Align each East/West pair and evaluate prediction coverage and accuracy.
- Assign each pair to a length bucket based on the West row-pair 8 length
  (short/medium/long using the global length ordering).

## Results
Overall: pairs=**8**, aligned=**147**, covered=**114**, correct=**24**.
- Bucket 0 (short): covered **0**, aligned **0**, correct **0** (no West data)
- Bucket 1 (medium): covered **57**, aligned **71**, correct **12** (acc **0.211**)
- Bucket 2 (long): covered **57**, aligned **76**, correct **12** (acc **0.211**)

## Interpretation
Medium and long length buckets show nearly identical accuracy and coverage,
so length alone does not explain decoding difficulty in this model. The short
bucket has no West messages, so it cannot be evaluated.

## Next Steps
- Compare against length-normalized bucket keys to see if relative position
  improves decoding.
- Restrict to paired lengths or re-bucket to balance East/West coverage.
