# Markov Predictability (Order-1 / Order-2)

## Goal
Measure how predictable the next trigram is using simple Markov models.

## Method
- Use the canonical weave trigram stream per message (no cross-message transitions).
- Baseline: always predict the globally most common trigram.
- Order-1: predict next trigram by argmax of P(next | prev).
- Order-2: predict next trigram by argmax of P(next | prev2, prev1).
- Tie-breakers use the smallest trigram value.

## In-sample results (trained on all messages)
Most common trigram: **5** (count 26).

- Baseline accuracy: 26 / 1027 = **0.0253**
- Order-1 accuracy: 192 / 1027 = **0.1870**
- Order-2 accuracy: 949 / 1018 = **0.9322**

Context stats:
- Order-1 contexts: 83 (deterministic: 0, avg branching ~= 10.24)
- Order-2 contexts: 842 (deterministic: 783, avg branching ~= 1.08)

## Leave-one-out results (train on all but the target message)
- Order-1 accuracy (seen contexts): 96 / 1027 = **0.0935** (coverage 1.0000)
- Order-2 accuracy (seen contexts): 158 / 269 = **0.5874** (coverage 0.2642)

## Leave-one-out with smoothing/backoff
Interpolated n-gram with add-k smoothing (k = 0.5, weights: l2=0.6, l1=0.3, l0=0.1).

- Order-1 accuracy (all positions): 94 / 1027 = **0.0915**
- Order-2 accuracy (all positions): 174 / 1018 = **0.1709**

## Smoothed leave-one-out by subset
Same smoothed model (k = 0.5, weights: l2=0.6, l1=0.3, l0=0.1), trained and evaluated within subsets.

- East paired (E1..E4):
  - Order-1: 17 / 469 = **0.0362**
  - Order-2: 62 / 465 = **0.1333**
- West paired (W1..W4):
  - Order-1: 21 / 445 = **0.0472**
  - Order-2: 20 / 441 = **0.0454**
- East all (E1..E5):
  - Order-1: 33 / 582 = **0.0567**
  - Order-2: 97 / 577 = **0.1681**

## Interpretation
Order-2 is highly predictive in-sample, which indicates strong local structure. However, the
drop in leave-one-out accuracy and coverage suggests that many order-2 contexts are
message-specific rather than universal. The interpolated model does not improve generalization
when forced to score all positions, which points to weak cross-message consistency even with
smoothing/backoff.

Within subsets, East-paired messages show higher order-2 generalization than West-paired,
and adding the East-only message improves East-side order-2 performance further. This hints
that East messages share more consistent short-range structure than West messages.

## Next Steps
- Try alternative interpolation weights and smaller k (e.g., k=0.1) to check sensitivity.
- Split by East/West subsets to see if one side generalizes better than the other.
