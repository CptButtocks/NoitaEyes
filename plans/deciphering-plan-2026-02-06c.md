# Deciphering Plan (2026-02-06, Rev 3)

## Objective
Turn the strongest, most stable signals into a practical decoder by focusing on
row-pair 8 digit-level structure, adaptive constraints, and shared trigram
contexts that generalize across messages.

## Evidence Recap (Current State)
- Canonical trigram weave is confirmed (83 values, 0-82), no adjacent repeats,
  and starting trigrams > 26.
- Order-2 Markov predictability is very high in-sample but drops sharply in
  leave-one-out; many contexts are message-specific.
- East vs West divergence is weak at cell level but strong at row-pair 8
  values; binning improves overlap, digits improve even more.
- Row-pair 8 length drives body length and diversity; row-pair 10 is rare.
- Alignment variants increase coverage but reduce accuracy vs NW baseline.
- HMMs and body-only templates do not improve generalization.

## Phase 1: Digit-Level Modeling and Two-Stage Decode (Highest Priority)
**Goal:** Convert digit-level stability into a full-value decoder with useful
coverage.

**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-digit-two-stage.md`

Summary:
- Conditional digit mapping: bottom|top covered 98, correct 53; top|bottom
  covered 110, correct 51.
- Two-stage decoding: independent 20/114, top->bottom 21/116, bottom->top
  27/117 (still below baseline full-value decode).

Steps:
1. Build digit sequences for row-pair 8 (top and bottom) per message.
2. Train run-position mappings for top and bottom digits separately.
3. Train conditional digit models (bottom | top, runpos) and (top | bottom, runpos).
4. Combine digit predictions into full base-25 candidates, selecting by max
   likelihood or argmax of counts.
5. Evaluate coverage and accuracy vs current baseline.

Tests / Experiments:
- Add hypotheses for digit-level mapping accuracy (top, bottom) and conditional
  digit mapping accuracy.
- Add hypothesis for two-stage full-value decoding using digit predictions.
- Write `experiments/rowpair8-digit-two-stage.md` with goal, method, results,
  interpretation, next steps.

Exit criteria:
- If two-stage decoding improves accuracy over full-value baseline without
  collapsing coverage, proceed to Phase 2. Otherwise, refine digit constraints.

## Phase 2: Adaptive Hybrid Constraints (Preserve Coverage)
**Goal:** Use bin and column constraints only where they help, instead of
blanket restriction.

**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-adaptive-constraints.md`

Summary:
- Adaptive scoring matches the baseline (covered 114, correct 24).
- Entropy-gated bin constraints reduce coverage (covered 112) without a
  correctness gain.

Steps:
1. Compute per-column entropy and East/West overlap for digits and base-25.
2. Apply bin constraints only when overlap or entropy thresholds are met.
3. Score candidates by (digit likelihood + bin membership) instead of hard
   rejection; compare top-1 accuracy and coverage.

Tests / Experiments:
- Hypothesis for adaptive constraint decoding (coverage, correct, accuracy).
- Hypothesis for entropy-gated constraints (only low-entropy columns).
- Write `experiments/rowpair8-adaptive-constraints.md`.

Exit criteria:
- Show a measurable accuracy lift while keeping coverage within 80% of the
  digit-only decode.

## Phase 3: Early/Late Segmentation and Anchored Alignment
**Goal:** Isolate the segments that carry cross-message structure and avoid
aligning noise.

**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-early-late-alignment.md`

Summary:
- Early columns outperform late (16/54 vs 8/60 correct).
- Rhythm-anchored alignment increases coverage but reduces accuracy (14/164).

Steps:
1. Split row-pair 8 columns into early/late segments by position fraction and
   by overlap peaks.
2. Re-evaluate mapping accuracy on early segment only.
3. Use row-pair rhythm markers (from body row-pair sequence) as anchors for
   segmented alignment.

Tests / Experiments:
- Hypothesis comparing early vs late decoding accuracy.
- Hypothesis for rhythm-anchored alignment vs baseline.
- Write `experiments/rowpair8-early-late-alignment.md`.

Exit criteria:
- Identify a stable early segment with higher accuracy than full-sequence
  decoding, or confirm no improvement (negative result still recorded).

## Phase 4: Shared Trigram Grammar Extraction
**Goal:** Extract cross-message grammar contexts that generalize, then use them
for segmentation and prediction.

**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/shared-order2-grammar.md`

Summary:
- 47 shared order-2 contexts; coverage rate 0.162.
- Shared-context prediction 132/165; header enrichment ratio 4.686.

Steps:
1. Identify order-2 contexts appearing in >= N messages (start with N=3).
2. Build a shared-context transition graph and measure its coverage.
3. Evaluate whether shared contexts are enriched in header vs body indices.
4. Build a grammar skeleton of stable transitions and measure predictive gain
   vs generic order-2.

Tests / Experiments:
- Hypothesis for shared-context coverage and predictive accuracy.
- Hypothesis for header/body enrichment of shared contexts.
- Write `experiments/shared-order2-grammar.md`.

Exit criteria:
- Demonstrate a subset of contexts that generalizes better than the full
  order-2 model, or confirm that no stable grammar emerges.

## Phase 5: Data Audit and Expansion
**Goal:** Confirm dataset completeness and reduce sparsity if additional sources
exist.

**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/data-audit.md`

Summary:
- Source vs engine messages match after removing line-break marker `5`
  (sourceCount 9, engineCount 9, mismatchCount 0).

Steps:
1. Cross-check engine messages against `sources/` spreadsheets and notes.
2. Verify all listed messages are represented and correctly parsed.
3. If new messages exist, integrate them and update tests and experiments.

Tests / Experiments:
- Hypothesis validating dataset counts and message IDs vs source files.
- Write `experiments/data-audit.md` summarizing any differences.

Exit criteria:
- Verified completeness or documented additions with updated tests.

## Deliverables Per Phase
- Add hypothesis to `NoitaEyes.Tests/HypothesisTests.cs` with fixed metrics.
- Add experiment write-up in `experiments/` (Goal, Method, Results,
  Interpretation, Next Steps).
- Update the current plan file with status and summary after each phase.

## Notes
- If a hypothesis fails, record the negative result with passing tests.
- Prefer stable, cross-message signals over high in-sample accuracy.
