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
- Row-pair 8: length-3 = 0, length-4 = 0, length-5 = 0, length-6 = 0
- Row-pair 10: length-3 = 0, length-4 = 0, length-5 = 0, length-6 = 0

Approximate motifs (edit distance 1, appearing across 2+ messages):
- Row-pair 8: length-5 = 0, length-6 = 0
- Row-pair 10: length-5 = 0, length-6 = 0

Coarse motifs (top/bottom digit sequences, shared across 2+ messages):
- Row-pair 8 top digit: length-3 = 36, length-4 = 11, length-5 = 0, length-6 = 0
- Row-pair 8 bottom digit: length-3 = 33, length-4 = 11, length-5 = 0, length-6 = 0
- Row-pair 10 top digit: length-3 = 0, length-4 = 0, length-5 = 0, length-6 = 0
- Row-pair 10 bottom digit: length-3 = 0, length-4 = 0, length-5 = 0, length-6 = 0

## Interpretation
Even when reading row-pair 8/10 blocks as column-ordered glyph pairs, there are
no shared 3- to 6-gram motifs across messages. This reinforces that these
late-stage bands are highly variable and likely encode message-specific
payload rather than fixed grammar.

Allowing edit distance 1 for length-5/6 motifs still yields no cross-message
matches, so the variability isn't just minor symbol noise.

Coarse-graining to top/bottom digit sequences introduces shared 3- and 4-grams,
but no shared 5- or 6-grams. This suggests weak low-level structure (possibly
column rhythm) without longer conserved templates.

## Next Steps
- Compare column-pair distributions between East and West subsets.
