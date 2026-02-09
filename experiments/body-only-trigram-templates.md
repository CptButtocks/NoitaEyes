# Body-Only Trigram Templates (Length 4)

## Goal
Extract the most frequent length-4 trigram paths restricted to the body to
identify any recurring payload motifs.

## Method
- Remove the motif-defined header (length 6).
- Scan body-only trigram sequences for contiguous 4-grams.
- Rank by frequency and report coverage and average position.

## Results
Top body templates:
1. **2-60-29-40** (count **4**, coverage **4**, avgPos **0**)
2. **14-81-8-15** (count **3**, coverage **3**, avgPos **9**)
3. **15-59-18-68** (count **3**, coverage **3**, avgPos **7**)

## Interpretation
Body-only templates have low coverage and are concentrated early in the body,
which suggests weak global templating beyond the header boundary.

## Next Steps
- Extend to length-5 templates to see if any longer body paths persist.
- Compare body-only templates by East vs West subsets.
