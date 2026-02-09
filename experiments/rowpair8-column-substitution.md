# Row-Pair 8 Column Substitution (Paired East/West)

## Goal
Test whether the row-pair 8 payload supports a consistent *position-specific*
East->West substitution when aligned by column index within paired messages.

## Method
- Use `GetRowPairColumnSequence` (column-ordered glyph-pair values, base-25).
- Restrict to paired East/West messages: (E1/W1), (E2/W2), (E3/W3), (E4/W4).
- Only include pairs where **both** messages contain a row-pair 8 block.
- For each aligned column index `i` in each usable pair, record a mapping
  `(i, eastValue) -> westValue`.
- Count mapping conflicts when a key maps to multiple West values.

## Results (Direct index alignment)
Usable pairs: **2** (E3/W3 and E4/W4).
Aligned positions across usable pairs: **52**.

- Mappings: **51**
- Conflicts: **1**

Conflict detail:
- index **11**, east **5** -> west **11** or **21**

## Results (Approximate alignment)
Same pairs, but align sequences with edit-distance (Needleman–Wunsch) before
mapping by alignment column.

- Aligned positions: **47**
- Mappings: **46**
- Conflicts: **1**

Conflict detail:
- alignment index **13**, east **5** -> west **5** or **11**

## Results (Stateful mapping: prevEast)
Add the previous East value to the mapping key:
`(index, eastValue, prevEast) -> westValue`.

- Aligned positions: **52**
- Mappings: **52**
- Conflicts: **0**

## Results (Stateful mapping: prevWest)
Add the previous West value to the mapping key:
`(index, eastValue, prevWest) -> westValue`.

- Aligned positions: **52**
- Mappings: **52**
- Conflicts: **0**

## Results (Stateful mapping: prevEast+prevWest)
Add both previous East and West values:
`(index, eastValue, prevEast, prevWest) -> westValue`.

- Aligned positions: **52**
- Mappings: **52**
- Conflicts: **0**

## Results (All East vs West pairs, aligned, prevEast)
Apply the prevEast stateful mapping to **all** East vs West pairs with
approximate alignment.

- Pairs: **8**
- Aligned positions: **147**
- Mappings: **147**
- Conflicts: **0**

## Results (Leave-one-out decode, aligned, prevEast)
Train the prevEast mapping on 7 pairs and predict the held-out pair.

- Pairs: **8**
- Aligned positions: **147**
- Covered positions: **26**
- Correct predictions: **6**

## Results (Leave-one-out decode, aligned, prevEast, motif-filtered)
Train only on pairs where the East message shares the **same header motif** as
the held-out pair.

- Pairs: **8**
- Aligned positions: **147**
- Covered positions: **24**
- Correct predictions: **6**

## Results (Leave-one-out decode, aligned, prevEast, run position)
Use the **original row-pair 8 column index** as the mapping index during
alignment.

- Pairs: **8**
- Aligned positions: **147**
- Covered positions: **114**
- Correct predictions: **24**

## Results (Run position buckets)
Bucket by original column index: 0..9, 10..19, 20+.

- Bucket 0: aligned **67**, covered **54**, correct **16**
- Bucket 1: aligned **64**, covered **58**, correct **8**
- Bucket 2: aligned **16**, covered **2**, correct **0**

## Results (Second-order state, early columns)
Add prev2 on columns 0..9:
`(index, east, prev1, prev2) -> west`.

- Aligned positions: **67**
- Covered positions: **54**
- Correct predictions: **16**

## Interpretation
A simple per-column mapping almost works but has a single conflict. Approximate
alignment shifts the conflict but does not remove it. Adding a one-step state
(prevEast or prevWest) resolves all conflicts for paired messages, and the
prevEast stateful rule remains conflict-free across all East/West pairs.

Leave-one-out decoding shows **low coverage and low accuracy**, but including
run position dramatically increases coverage (114 vs 26) and doubles the number
of correct predictions (24 vs 6). Bucketed results show that accuracy is
concentrated in early columns, while late columns (20+) have near-zero coverage
and accuracy. Adding a second-order state in early columns does **not** improve
accuracy beyond the first-order mapping.

## Next Steps
- Compare early-column accuracy to a shuffled baseline to confirm it is above
  chance.
- Test whether the early-column mapping predicts any stable glyph-pair
  constraints across East/West.
