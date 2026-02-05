# Body Run-Length Grouping

## Goal
Compare row-pair 8/10 run lengths across East/West message groups and across
header motif groups (B vs C).

## Method
- Use motif-based header segmentation (header length = 6).
- Extract body run lengths for row-pairs 6/8/10.
- Aggregate run8/run10 totals and non-zero counts by group.

## Results
### East vs West
East ids: 0,2,4,6,8 (5 messages)
- run8 total: 65 (non-zero in 4/5)
- run10 total: 7 (non-zero in 1/5)

West ids: 1,3,5,7 (4 messages)
- run8 total: 36 (non-zero in 2/4)
- run10 total: 0 (non-zero in 0/4)

### Header motifs
Motif B: ids 3,4,5,6,7,8 (6 messages)
- run8 total: 87 (non-zero in 5/6)
- run10 total: 7 (non-zero in 1/6)

Motif C: ids 0,1,2 (3 messages)
- run8 total: 14 (non-zero in 1/3)
- run10 total: 0 (non-zero in 0/3)

## Interpretation
Row-pair 8 expansion is concentrated on the East side and in motif B messages.
Row-pair 10 appears only once (message 4), and only in motif B. This supports
the idea that the late body expansion is associated with a specific header
template rather than being evenly distributed.

## Next Steps
- Check whether the lone row-pair 10 message (id 4) has any other unique traits
  (unique trigram count, sum outlier, special glyph positions).
- Inspect the trigrams within the row-pair 8 and 10 blocks for repeated motifs.
