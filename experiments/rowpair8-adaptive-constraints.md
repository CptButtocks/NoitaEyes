# Row-Pair 8 Adaptive Constraints

## Goal
Test whether adaptive bin constraints improve decoding without collapsing
coverage.

## Method
- Use run-position decoding with leave-one-out training.
- Adaptive scoring: keep all candidates but add a bin bonus if the predicted
  value falls in allowed West bins (binBonus = 1.0).
- Entropy-gated constraints: apply bin constraints only on columns with
  entropy <= 1.5 and East/West overlap >= 0.2.

## Results
Adaptive scoring:
- pairs **8**, aligned **147**, covered **114**, correct **24**

Entropy-gated bin constraints:
- pairs **8**, aligned **147**, covered **112**, correct **24**

## Interpretation
Adaptive scoring yields identical performance to the baseline run-position
decoder, and entropy-gated constraints reduce coverage without improving
accuracy. The bin signal is too weak to help unless combined with stronger
features.

## Next Steps
- Increase bin bonus and use candidate counts instead of argmax only.
- Combine digit predictions with bin membership on low-entropy columns.
- Test adaptive constraints only on early columns.
