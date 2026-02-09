# Deciphering Plan (2026-02-05)

## Objective
Advance from structural validation to actionable decoding hypotheses by
systematically testing payload constraints, segmentation boundaries, and
stateful East/West relationships in reproducible experiments.

## Current State (from experiments)
- Canonical trigram extraction and counts are validated; 83 unique values
  with stable counts across messages.
- Two fixed header templates exist (motif B and motif C), each 6 trigrams,
  with a variable leading trigram and fixed tail.
- Body layout follows a deterministic row-pair sequence (0->2->4->6->8->10),
  with variability concentrated in late row-pairs (8/10).
- East/West relations are not explained by a static substitution; conflicts
  remain even after alignment and periodic offsets.
- Row-pair 8/10 column motifs are highly variable; no shared 3–6-grams even
  with edit distance 1. Coarse digit motifs show only short (3/4) overlap.
- Coarse binning increases East/West overlap substantially (distribution-level
  structure), but per-column binning and mode alignment remain side-specific
  for most columns.
- Stateful mapping (prevEast) removes conflicts for aligned pairs, but
  leave-one-out decoding has low accuracy/coverage.

## Guiding Principles
- Keep experiments in `NoitaEyes.Tests` and document results in `experiments/`.
- Prefer tests that quantify: coverage, accuracy, overlap, conflicts, and
  baseline comparisons.
- Focus on segmentation and constraints before attempting full decoding.
- Use multiple baselines (shuffled, random, and marginal) to avoid false
  positives.

## Phase 1: Strengthen Payload Constraints (Row-Pair 8/10)
**Status: Completed (2026-02-05)**

Key outputs:
- `experiments/body-column-bin-sweep.md`
- `experiments/body-column-bin-top2-alignment.md`
- `experiments/body-column-entropy-overlap.md`
- `experiments/body-column-early-late-overlap.md`

Summary:
- Coarse binning overlap varies by bin size (best around 3/3 and 5/5).
- Top-2 bin alignment is modest (avg 0.555) with 5 fully aligned columns.
- Entropy strongly anti-correlates with raw overlap (r=-0.833), but not with
  binned overlap (r≈0).
- Early/late columns show similarly low raw overlap; binned overlap slightly
  higher late.

1. **Bin size sweep**
   - Re-run coarse binning with (high,mid) = (3,3), (4,4), and (5,5).
   - Track average Jaccard, per-column overlap, and disjoint columns.
   - Goal: identify whether overlap stabilizes or diverges with bin granularity.
2. **Second-mode alignment**
   - Compare East/West top-2 bin distributions per column.
   - Measure overlap on sets of top-2 bins (not just the mode).
3. **Per-column entropy vs overlap**
   - Correlate per-column entropy (row-pair 8) with bin overlap and mode match.
   - Goal: see if high entropy columns are consistently the divergent ones.
4. **Column position influence**
   - Compare early vs late columns using identical metrics to confirm
     where constraints break down.

## Phase 2: Anchor Discovery and Secondary Segmentation
**Status: Completed (2026-02-05)**

Key outputs:
- `experiments/body-trigram-motifs.md`
- `experiments/body-trigram-motifs-near.md`
- `experiments/body-anchor-segmentation.md`
- `experiments/body-column-band-clustering.md`

Summary:
- Body motifs of length 2/3 are shared but low-coverage (top coverage=4).
- Approximate matching yields many near-shared motifs (high variability).
- Top anchor motif appears only at the body start; no mid-body boundary found.
- Column clustering shows minimal grouping at Jaccard >= 0.5.

1. **Mid-body motif scan**
   - Search for shorter anchors (length 2–3) that recur across messages in
     the body (indices 6..end).
   - Use approximate matches (edit distance 1) to catch weak anchors.
2. **Segment-by-anchors**
   - If anchors exist, split payload into segments and recompute
     row-pair stats, motif overlap, and East/West relations per segment.
3. **Column band clustering**
   - Cluster row-pair 8 columns by value-set similarity across messages
     to detect internal phases.

## Phase 3: East/West Relationship Models
**Status: Completed (2026-02-05)**

Key outputs:
- `experiments/rowpair8-stateful-prevn.md`
- `experiments/rowpair8-stateful-bucket-key.md`
- `experiments/rowpair8-stateful-constrained.md`
- `experiments/rowpair8-stateful-offset-alignment.md`

Summary:
- Prev2/Prev3 add no improvement over prev1 run-position mapping.
- Bucketed index key gives a small accuracy lift (+2 correct).
- Column constraints do not change coverage/accuracy.
- Offset alignment increases coverage but lowers accuracy.

1. **Stateful decoding refinement**
    - Extend prevEast mapping with limited context length (prev2, prev3)
      and evaluate accuracy/coverage by column region.
    - Add a small state classifier that switches mapping based on column bucket.
2. **Constraint-based mapping**
   - Use column value sets and bin constraints as a filter for candidate
     East->West mappings (reduce hypothesis space).
3. **Local alignment alternatives**
   - Compare Needleman–Wunsch to simple sliding-window alignment and
     anchor-based alignment to reduce alignment noise.

## Phase 4: Global Structure / Grammar Modeling
**Status: Completed (2026-02-05)**

Key outputs:
- `experiments/hmm-2state-loglik.md`
- `experiments/trigram-path-templates.md`

Summary:
- 2-state HMM underperforms unigram baseline (needs better initialization or more states).
- Top 4-gram templates are header-dominant, with limited body coverage.

1. **HMM / PCFG experiments**
   - Treat trigrams as symbols and learn a small HMM or PCFG to model
     the header and early body transitions.
   - Measure generalization to held-out messages.
2. **Graph-based templating**
   - Extract high-probability paths in the transition graph as candidate
     templates and check for consistent placement in the header/body.

## Phase 5: Metadata Correlation (Possible Control Variables)
**Status: Completed (2026-02-05)**

Key outputs:
- `experiments/header-lead-trigram-correlation.md`
- `experiments/rowpair8-length-vs-content.md`

Summary:
- Lead trigram weakly correlates with sums/uniques but differs strongly by side.
- Row-pair 8 length strongly correlates with content diversity and bin makeup.

1. **Header lead trigram correlation**
   - Test if the variable header lead trigram correlates with message
     sum, unique count, or East/West group.
2. **Row-pair 8 length vs content**
   - Analyze whether row-pair 8 run length predicts bin distributions
     or substitution success.

## Deliverables Checklist (per experiment)
- Hypothesis test in `NoitaEyes.Tests/HypothesisTests.cs`
- Experiment report in `experiments/`
- Include: goal, method, results, interpretation, next steps
- Record key metrics and baselines

## Immediate Next Actions
Plan complete. Next steps should be selected based on the strongest new signal
(currently: side-specific lead trigram and row-pair 8 length correlations).
