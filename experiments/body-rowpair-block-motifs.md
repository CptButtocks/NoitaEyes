# Body Row-Pair Block Motifs

## Goal
Inspect the trigram values inside the row-pair 8 and 10 body blocks to see
whether any local motifs or repeated transitions emerge.

## Method
- Use motif-based header segmentation (header length = 6).
- For each message, isolate the row-pair 8 and 10 blocks in the body.
- Collect trigram values and compute transition counts within each block.

## Row-pair 8 blocks
Total values: 101 (57 unique).

All row-pair 8 transitions are unique (95 total transitions, all count 1).
There are no self-transitions within row-pair 8 blocks.

## Row-pair 10 blocks
Only message 4 has a row-pair 10 block.

Values: 20, 71, 73, 25, 35, 8, 24 (7 unique).
Transitions are all unique (6 total transitions, all count 1).

## Interpretation
Row-pair 8/10 blocks do not reuse short trigram transitions across messages,
which suggests these blocks are either highly variable or driven by a different
mechanism than the structured header/body backbone. The lack of repeated
transitions indicates that if there is a grammar in these blocks, it is not
expressed through repeated local bigrams.

## Next Steps
- Explore longer motifs (length 3+) within row-pair 8 blocks to check for shared
  subsequences across messages.
- Check whether row-pair 8/10 block values correlate with specific glyph
  positions or orientations.
