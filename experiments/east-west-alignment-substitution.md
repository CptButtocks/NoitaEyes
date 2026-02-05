# East–West Alignment & Substitution Test (Anchored)

## Goal
Test whether East/West pairs can be aligned (anchored on a shared motif) and
decoded via a single consistent substitution mapping.

## Method
- Build trigram sequences with the canonical weave scheme.
- Anchor alignment on the shared subsequence `[66, 5]`.
- Use global alignment (Needleman–Wunsch) for prefix/suffix.
- Extract mappings from aligned trigram pairs and count conflicts
  (a trigram mapping to multiple targets).

## Results
Anchor: `[66, 5]` (present in every message).

Per pair:
- E1/W1: conflicts=21, mappings=56, score=32, matches=46, gaps=10
- E2/W2: conflicts=24, mappings=57, score=-71, matches=19, gaps=36
- E3/W3: conflicts=29, mappings=64, score=-80, matches=23, gaps=37
- E4/W4: conflicts=32, mappings=61, score=-37, matches=30, gaps=15

Totals:
- total conflicts: 106
- total mappings: 238

## Interpretation
Anchored alignments produce many conflicting mappings, which strongly argues
against a single consistent substitution between East and West trigrams.
If a cipher exists, it likely depends on position/state rather than a static map.

## Next Steps
- Repeat with periodic substitution (mapping by index mod N).
- Test alternate anchors (e.g., chain `5->49->75->54`) to validate robustness.
