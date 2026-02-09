# Header Lead vs Motif Group

## Goal
Determine whether header lead trigram values differ between motif B and motif C
header groups.

## Method
- Identify header motif (B vs C) using the header-end detection helper.
- For each message, extract the lead trigram (index 0).
- Compare mean lead values and counts for motif B vs motif C.

## Results
- motif B mean lead: **51.667** (n=6)
- motif C mean lead: **55.333** (n=3)

## Interpretation
The mean lead values for motif B and motif C are close, with a small sample
size for motif C. There is no strong separation between the two motif groups
based on the lead trigram alone.

## Next Steps
- Use lead trigram as a side-control variable rather than a motif predictor.
- Re-evaluate motif groups once more messages are available.
