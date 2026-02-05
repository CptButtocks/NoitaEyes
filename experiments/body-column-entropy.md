# Body Column Entropy (Row-Pair 8/10)

## Goal
Measure per-column entropy of row-pair 8/10 bands to detect low-entropy (more
structured) positions in the variable payload region.

## Method
- Use motif-based header segmentation (header length = 6).
- For each row-pair 8/10 band, read column-ordered glyph pairs:
  value = top * 5 + bottom (0..24).
- Aggregate per-column distributions across messages that contain the band.
- Compute entropy and max probability per column.

## Row-pair 8 column profile
Counts reflect the number of messages with the band long enough to cover the
column index.

Selected highlights:
- Columns 0..14 appear in all 6 messages; entropy ~2.25-2.59 (high variance).
- Columns 15..20 appear in 5 messages; entropy ~1.92-2.32.
- Column 21 appears in 4 messages; entropy 2.0.
- Columns 22..23 appear in 3 messages; entropy 1.585.
- Column 24 appears in 2 messages; entropy 0 (identical).
- Columns 30..38 appear in only 1 message; entropy 0 (singletons).

## Row-pair 10 column profile
Only message 4 contains row-pair 10; columns 0..9 are all singletons
(entropy 0).

## Interpretation
Row-pair 8 columns show high entropy across the shared prefix, then rapidly
collapse to singletons in the tail where fewer messages have coverage. There is
no low-entropy "fixed" column across all row-pair 8 messages, reinforcing that
this band encodes variable payload.

## Next Steps
- Normalize by column coverage (e.g., analyze only columns 0..14) and test for
  weaker constraints (e.g., restricted glyph-pair sets per column).
- Compare column distributions between East and West subsets to see if the
  payload is side-specific.
