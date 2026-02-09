# Header Lead vs Row-Pair 8 Length

## Goal
Test whether header lead trigram values correlate with row-pair 8 length or
overall body length.

## Method
- Extract lead trigram value (index 0) per message.
- Compute row-pair 8 column sequence length per message.
- Compute body length as total glyph count.
- Correlate lead trigram with row-pair 8 length and body length.

## Results
- corr(lead, row-pair 8 length): **-0.335**
- corr(lead, body length): **-0.259**

## Interpretation
Lead trigram values show weak negative correlations with payload length, which
suggests the lead trigram does not directly encode length or complexity.

## Next Steps
- Compare length-conditioned decoding accuracy to see if length drives
  alignment difficulty.
- Check whether East-only and West-only subsets show different correlations.
