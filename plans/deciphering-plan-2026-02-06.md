# Deciphering Plan (2026-02-06)

## Objective
Convert the strongest new signals into testable decoding hypotheses, with a
focus on side-specific control variables (header lead trigram) and payload
complexity (row-pair 8 length).

## Current Signals Worth Chasing
- **Lead trigram shows a strong East/West mean split** with weak correlation to
  sum/unique counts.
- **Row-pair 8 length strongly correlates** with content diversity and bin
  composition.
- **Coarse binning reveals shared structure**, but exact symbols remain
  side-specific.
- **Stateful decoding** improves modestly via bucketed keys but stalls in late
  columns.

## Phase 1: Lead Trigram as Control Variable
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/header-lead-vs-motif-group.md`
- `experiments/header-lead-vs-rowpair8-length.md`
- `experiments/header-lead-vs-payload-bin-composition.md`

Summary:
- Motif B vs C lead means are close (51.667 vs 55.333) with small sample size.
- Lead trigram weakly correlates with row-pair 8 length (-0.335) and body
  length (-0.259).
- Lead buckets show only modest bin-composition differences (avgBin ~1.33-1.42,
  highFrac ~0.48-0.55).

1. **Lead trigram vs motif group**
   - Compare lead trigram distributions between motif B vs C headers.
   - Test if lead trigram predicts motif group or side.
2. **Lead trigram vs payload length**
   - Correlate lead trigram with row-pair 8 length and body length.
3. **Lead trigram vs bin composition**
   - Compare payload bin distributions conditioned on lead trigram buckets
     (e.g., high/med/low lead values).

## Phase 2: Row-Pair 8 Length as a Complexity Axis
**Status: Completed (2026-02-06)**

Key outputs:
- `experiments/rowpair8-length-bucket-overlap.md`
- `experiments/rowpair8-length-bucket-decoding.md`
- `experiments/rowpair8-length-normalized-decoding.md`

Summary:
- Length buckets (n=2 each) leave the short bucket with no West messages.
- Medium/long buckets show low raw overlap (0.045-0.067) but higher binned
  overlap (0.364-0.533).
- Run-position decoding accuracy is identical for medium and long buckets
  (acc 0.211).
- Normalized buckets slightly increase coverage/accuracy for the medium bucket
  (acc 0.246) with minor effect on long.

1. **Subset analysis by length**
   - Split messages into short/medium/long row-pair 8 length bins.
   - Recompute column overlap, entropy, and bin alignment per subset.
2. **Length vs decoding accuracy**
   - Evaluate run-position decoding accuracy by length bucket.
3. **Length-normalized alignment**
   - Test if normalizing or rescaling column index improves alignment or
     decoding for long payloads.

## Phase 3: Side-Specific Modeling
1. **Separate East/West models**
   - Train stateful mappings only within East or West subsets and compare
     internal consistency.
2. **Cross-side transfer**
   - Train on East and evaluate on West (and vice versa) to quantify
     transferability of constraints.
3. **Side-conditional bin constraints**
   - Build East-only and West-only bin sets, and test if they explain
     the divergence better than global bins.

## Phase 4: Alignment Refinement
1. **Anchor-based alignment**
   - Use fixed header motifs as anchors and align body segments relative
     to anchor positions.
2. **Segmented alignment**
   - Align early body and late body separately to reduce drift.
3. **Hybrid alignment scoring**
   - Combine match score with bin similarity rather than exact symbol matches.

## Phase 5: Grammar/Sequence Modeling (Next Iteration)
1. **HMM with more states or supervised initialization**
   - Try 3-4 states and initialize by header/body or side labels.
2. **Body-only templates**
   - Extract top n-grams restricted to body indices to isolate payload patterns.
3. **Transition-graph templates by segment**
   - Build transition graphs for header vs body and compare hubs.

## Deliverables (per step)
- Add hypothesis to `NoitaEyes.Tests/HypothesisTests.cs`.
- Add a new experiment write-up in `experiments/` with:
  - Goal, Method, Results, Interpretation, Next Steps.
- Update this plan file after completing each phase or major step.

## Immediate Next Actions
1. Implement side-specific models (Phase 3.1).
2. Measure cross-side transfer (Phase 3.2).
3. Build side-conditional bin constraints (Phase 3.3).
