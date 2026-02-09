# Row-Pair 8 Digit Two-Stage Decode

## Goal
Test whether digit-level stability can be converted into full-value decoding by
combining top/bottom digit predictions and conditional digit mappings.

## Method
- Use row-pair 8 column sequences and Needleman–Wunsch alignment.
- Train run-position digit mappings (east -> west) for top and bottom digits.
- Train conditional digit mappings from west digits:
  - bottom | top (run-position)
  - top | bottom (run-position)
- Evaluate leave-one-out prediction for conditional digit mappings.
- Combine digit predictions into full values in three modes:
  - Independent top+bottom mappings
  - Top -> conditional bottom
  - Bottom -> conditional top

## Results
Conditional digit mapping:
- bottom | top: covered **98**, correct **53**
- top | bottom: covered **110**, correct **51**

Two-stage full-value decoding:
- independent: covered **114**, correct **20**
- top -> bottom: covered **116**, correct **21**
- bottom -> top: covered **117**, correct **27**

## Interpretation
Conditional digit mappings are moderately predictive, but combining digits into
full values remains weak. The bottom->top conditional variant is best, but still
below the baseline run-position decode (24 correct) and far below digit-only
prediction accuracy. Digit stability does not translate cleanly to stable full
value mappings.

## Next Steps
- Use digit posterior distributions (not argmax) when combining digits.
- Combine digit predictions with bin constraints on low-entropy columns.
- Restrict to early columns where digit accuracy is higher.
