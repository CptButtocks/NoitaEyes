# Row-Pair 8 Normalized Bucket Sweep

## Goal
Check whether increasing normalized bucket granularity (3/4/5) improves
run-position decoding accuracy.

## Method
- Use normalized position buckets and prevEast mapping (leave-one-out).
- Evaluate for bucket counts 3, 4, and 5.
- Report coverage and accuracy.

## Results
- 3 buckets: covered **115**, aligned **147**, correct **26** (acc **0.226**)
- 4 buckets: covered **115**, aligned **147**, correct **26** (acc **0.226**)
- 5 buckets: covered **116**, aligned **147**, correct **26** (acc **0.224**)

## Interpretation
Bucket granularity does not materially improve decoding performance. The
normalized-bucket approach appears stable but still limited by late-column
ambiguity.

## Next Steps
- Combine normalized buckets with bin or digit-level constraints.
- Evaluate bucket granularity only on early columns where signal is stronger.
