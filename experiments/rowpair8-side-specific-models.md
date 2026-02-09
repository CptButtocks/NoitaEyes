# Row-Pair 8 Side-Specific Models

## Goal
Test whether East-only and West-only row-pair 8 mappings are internally
consistent, and whether those mappings transfer across sides.

## Method
- Build run-position mappings with prevEast state.
- East-only: use ordered pairs among East messages with row-pair 8.
- West-only: use ordered pairs among West messages with row-pair 8.
- Measure mapping conflicts and leave-one-out decoding coverage/accuracy.
- Cross-side transfer: train within one side and evaluate on the other.

## Results
East-only:
- pairs: **12**
- conflicts: **54 / 93**
- covered: **158**, correct: **38**

West-only:
- pairs: **2**
- conflicts: **0 / 44**
- covered: **0**, correct: **0**

Cross-side transfer:
- east -> west: covered **0**, correct **0**
- west -> east: covered **0**, correct **0**

## Interpretation
East-only mappings show substantial internal conflict, while West-only data is
too sparse to evaluate (no coverage). Cross-side transfer fails entirely,
indicating that side-specific mappings do not generalize across sides under
this model.

## Next Steps
- Restrict to paired messages with row-pair 8 to reduce conflict noise.
- Explore side-specific bin constraints to increase overlap without exact
  value matching.
