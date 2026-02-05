# Body Segmentation: Row-Pair Run Sequences

## Goal
Extract the exact row-pair sequence in the body (indices 6..end) and verify if
it follows a fixed run order with variable run lengths.

## Method
- Use motif-based header segmentation (header length = 6).
- Extract the row-pair sequence for the body.
- Run-length encode the sequence into (rowPair x length) runs.

## Run sequences (per message)
- 0: 0x20, 2x26, 4x26, 6x21
- 1: 0x20, 2x26, 4x26, 6x25
- 2: 0x20, 2x26, 4x26, 6x26, 8x14
- 3: 0x20, 2x26, 4x26, 6x24
- 4: 0x20, 2x26, 4x26, 6x26, 8x26, 10x7
- 5: 0x20, 2x26, 4x26, 6x26, 8x20
- 6: 0x20, 2x26, 4x26, 6x26, 8x15
- 7: 0x20, 2x26, 4x26, 6x26, 8x16
- 8: 0x20, 2x26, 4x26, 6x26, 8x10

## Interpretation
The body row-pair sequence is a strict progression:
0 -> 2 -> 4 -> 6 -> (8) -> (10). The first three runs are invariant, and all
variation is confined to the tail (row-pair 6 length and optional row-pairs 8
and 10). This strongly suggests a deterministic layout phase ordering rather
than free mixing of row-pairs.

## Next Steps
- Check whether row-pair 6/8/10 run lengths correlate with message length,
  trigram sum, or unique trigram count.
- Align body run lengths with glyph coordinates to see if row-pair blocks map to
  visual row bands or structural markers.
