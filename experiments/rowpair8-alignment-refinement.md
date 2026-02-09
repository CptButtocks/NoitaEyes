# Row-Pair 8 Alignment Refinement

## Goal
Compare alternate alignment strategies to the Needleman–Wunsch baseline for
run-position decoding.

## Method
- Direct alignment: compare sequences by index without gaps.
- Segmented alignment: align columns 0–9 and 10+ separately.
- Bin-aware alignment: align sequences using coarse bins (high=8, mid=8) and
  evaluate decoding on original values.

## Results
- Direct alignment: aligned **170**, covered **164**, correct **14**
- Segmented alignment: aligned **148**, covered **118**, correct **14**
- Bin-aware alignment: aligned **153**, covered **126**, correct **12**

## Interpretation
All alternate alignments increase aligned/covered counts but reduce accuracy
relative to the Needleman–Wunsch run-position baseline (24 correct). This
suggests that flexible alignment is aligning noise rather than structure.

## Next Steps
- Constrain alignment using fixed header offsets or row-pair rhythm markers.
- Evaluate alignment quality only on early columns where signal is stronger.
