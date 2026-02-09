# Shared Order-2 Grammar

## Goal
Identify order-2 contexts that generalize across messages and measure their
predictive power and header/body enrichment.

## Method
- Build order-2 contexts (prev2, prev1) per message.
- Keep contexts appearing in >= 3 messages (shared contexts).
- Measure coverage rate across all positions.
- Train leave-one-out predictors restricted to shared contexts and evaluate
  accuracy.
- Compare header vs body prevalence of shared contexts.

## Results
- shared contexts: **47**
- coverage rate: **0.162** of all order-2 positions
- shared-context prediction: **132 / 165** correct
- header share (shared contexts): **0.164**
- header share (baseline): **0.035**
- enrichment ratio: **4.686**

## Interpretation
A small set of shared order-2 contexts generalizes well and is strongly
header-skewed. This supports the hypothesis that the header contains a stable
grammar, while body contexts remain message-specific.

## Next Steps
- Use shared contexts to refine header segmentation and templates.
- Evaluate shared contexts within body-only positions.
- Test different coverage thresholds (N=2, N=4) to tune tradeoffs.
