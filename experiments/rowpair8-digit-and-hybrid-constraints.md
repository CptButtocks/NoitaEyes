# Row-Pair 8 Digit and Hybrid Constraints

## Goal
Test whether splitting glyph pairs into top/bottom digits improves overlap and
predictability, and whether hybrid digit+bin constraints help decoding.

## Method
- Compute East/West column overlap for top digits and bottom digits.
- Decode top and bottom digits separately with run-position mapping.
- Apply hybrid constraints (digit + bin) during decoding.

## Results
Digit overlap:
- top-digit avg Jaccard: **0.328**
- bottom-digit avg Jaccard: **0.360**

Digit decoding:
- top digits: covered **130**, correct **53**
- bottom digits: covered **127**, correct **69**

Hybrid constraints:
- covered **23**, correct **9**

## Interpretation
Top/bottom digits are more stable than full base-25 values, and digit-level
prediction is substantially higher than full-value decoding. Hybrid
constraints improve precision but cut coverage heavily.

## Next Steps
- Build a two-stage decoder: predict digits first, then refine with bin
  constraints to recover full values.
- Compare digit-level stability between early and late columns.
