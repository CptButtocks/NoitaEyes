# Row-Pair 8 Length vs Content

## Goal
Measure whether row-pair 8 length predicts content complexity or coarse bin
composition.

## Method
- For each message, compute row-pair 8 column length.
- Compute:
  - Unique value count in row-pair 8
  - Average bin value (global bins high=8, mid=8)
  - Fraction of high-bin values
- Correlate each metric with row-pair 8 length.

## Results
- corr(length, unique): **0.972**
- corr(length, avgBin): **0.909**
- corr(length, highFraction): **0.920**

## Interpretation
Row-pair 8 length strongly correlates with content complexity and the
proportion of high-frequency bins. Longer payloads are both more diverse
and more weighted toward globally frequent symbols.

## Next Steps
- Check whether these correlations hold within East-only and West-only subsets.
- Correlate length with decoding accuracy to test whether longer payloads are
  systematically harder.
