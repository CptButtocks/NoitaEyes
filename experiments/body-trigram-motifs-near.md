# Body Trigram Motifs (Approximate, Post-Header)

## Goal
Assess whether short body motifs become more shared when allowing one
substitution (edit distance 1).

## Method
- Same body segmentation as the exact motif scan.
- Count motifs that have a near-match (Hamming distance <= 1) in another
  message.
- Motif lengths: 2 and 3.

## Results
Near-shared motifs:
- Length-2: **835**
- Length-3: **287**

## Interpretation
Allowing one edit dramatically increases the number of near-shared motifs.
This indicates that body motifs are highly variable at the symbol level,
but may still follow weak structural patterns.

## Next Steps
- Combine near-matches with column/row-pair context to filter noise.
- Use anchor-based segmentation rather than purely motif-based grouping.
