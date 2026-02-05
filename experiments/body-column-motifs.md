# Body Column Motifs (Row-Pair 8/10)

## Goal
Extract column-ordered glyph-pair sequences for row-pair 8/10 body bands and
search for shared length-3 or length-4 motifs across messages.

## Method
- Use motif-based header segmentation (header length = 6).
- Identify the row-pair 8/10 body block and its column band via placements.
- For each column in the band, read the (top,bottom) glyph pair and encode as
  base-5 pair value: value = top * 5 + bottom (0..24).
- Scan for length-3 and length-4 motifs across messages.

## Row-pair 8 column sequence lengths
- id 2: 21
- id 4: 39
- id 5: 30
- id 6: 22
- id 7: 24
- id 8: 15

## Row-pair 10 column sequence lengths
- id 4: 10

## Motif results
Shared motifs (appearing in 2+ messages):
- Row-pair 8: length-3 = 0, length-4 = 0
- Row-pair 10: length-3 = 0, length-4 = 0

## Interpretation
Even when reading row-pair 8/10 blocks as column-ordered glyph pairs, there are
no shared 3- or 4-gram motifs across messages. This reinforces that these
late-stage bands are highly variable and likely encode message-specific
payload rather than fixed grammar.

## Next Steps
- Try longer motifs (length 5+) or approximate matching (edit distance 1) to
  detect weakly conserved patterns.
- Compare column-pair distributions between East and West subsets.
