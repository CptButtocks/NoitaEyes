# Row-Pair 8 Stateful Decode (Column Constraints)

## Goal
Check whether constraining predictions to observed West column value sets
improves decoding accuracy.

## Method
- Use run-position mapping `(index, east, prevEast)` with Needleman–Wunsch.
- Build West column value sets from **training pairs only**.
- During evaluation, only accept predictions that fall inside the allowed
  West column set for that index.

## Results
- Pairs: **8**
- Aligned: **147**
- Covered: **114**
- Correct: **24**

## Interpretation
Applying column constraints does **not** change accuracy or coverage relative
to the unconstrained run-position decoder, indicating that the mapping already
produces values consistent with observed West sets for the evaluated columns.

## Next Steps
- Combine constraints with bucketed keys to reduce late-column ambiguity.
- Use bin-level constraints instead of exact value sets.
