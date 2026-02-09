# Header Lead Trigram Correlation

## Goal
Test whether the variable header lead trigram correlates with message-level
properties or East/West grouping.

## Method
- Extract the first trigram value (index 0) per message.
- Compute correlations with:
  - Message trigram sum
  - Unique trigram count
- Compare mean lead values for East vs West message groups.

## Results
- corr(lead, sum): **-0.258**
- corr(lead, unique): **-0.038**
- East mean lead: **41.8**
- West mean lead: **66.75**

## Interpretation
Lead trigram values have weak correlations with sums/uniques, but show a large
East/West mean split. This suggests the lead trigram may encode side-specific
information rather than overall message size or diversity.

## Next Steps
- Compare lead trigram by header motif group (B vs C).
- Test lead trigram against row-pair 8 length and payload bin composition.
