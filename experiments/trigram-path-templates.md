# Trigram Path Templates (Length 4)

## Goal
Identify the most frequent length-4 trigram paths and check their coverage
and typical positions.

## Method
- Scan all trigram sequences and count all contiguous 4-grams.
- Rank by total count (tie-break by motif).
- Report top 3 sequences with count, message coverage, and average position.

## Results
Top templates:
1. **5-49-75-54** (count 6, coverage 6, avgPos 2)
2. **66-5-49-75** (count 6, coverage 6, avgPos 1)
3. **2-60-29-40** (count 4, coverage 4, avgPos 6)

## Interpretation
The top two templates are header-adjacent and match the motif B header tail,
reinforcing that header structure dominates the most frequent 4-grams. The
third template appears later (avgPos 6) with lower coverage, hinting at a
secondary structure but not a global template.

## Next Steps
- Extend to length-5 paths to test whether header templates persist longer.
- Search for top paths constrained to body indices only.
