# Body Block Coordinate Overlay

## Goal
Map row-pair body blocks onto glyph coordinates to see whether each block
occupies a contiguous column band.

## Method
- Use motif-based header segmentation (header length = 6).
- Build `TrigramWeaver.BuildWeavePlacements` for each message.
- Split body placements into row-pair runs (blocks).
- For each block, collect glyph columns in the top/bottom rows and compute
  min/max ranges and contiguity.

## Results (column ranges per block)
Format: rowPair -> top[min..max] (count), bottom[min..max] (count).

Message 0:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..31] (32), bottom[0..30] (31)

Message 1:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..37] (38), bottom[0..36] (37)

Message 2:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 8 -> top[0..20] (21), bottom[0..20] (21)

Message 3:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..35] (36), bottom[0..35] (36)

Message 4:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 8 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 10 -> top[0..10] (11), bottom[0..9] (10)

Message 5:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 8 -> top[0..29] (30), bottom[0..29] (30)

Message 6:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 8 -> top[0..22] (23), bottom[0..21] (22)

Message 7:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 8 -> top[0..23] (24), bottom[0..23] (24)

Message 8:
- rowPair 0 -> top[9..38] (30), bottom[9..38] (30)
- rowPair 2 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 4 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 6 -> top[0..38] (39), bottom[0..38] (39)
- rowPair 8 -> top[0..14] (15), bottom[0..14] (15)

All top/bottom column ranges are contiguous for every block.

## Interpretation
Body blocks are literal contiguous column bands within each row pair. The
row-pair 0 body block always starts at column 9, implying the header occupies
columns 0..8 in rows 0/1. Row-pairs 2 and 4 always span the full row width,
showing the header does not touch those row pairs. Late expansions (row-pair 8
and 10) occupy leading columns 0..N, where N tracks the run length.

This strongly supports a spatial, banded layout: the grammar is not only
sequence-based but tied to contiguous glyph coordinates.

## Next Steps
- Overlay the row-pair 8/10 bands with actual glyph values to test if these
  bands align with visual markers or symbols.
- Compare the column ranges with message lengths to see if the header width
  (column 0..8) is constant across all messages.
