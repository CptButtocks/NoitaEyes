# Row-Pair 8 Stateful Decode (Offset Alignment)

## Goal
Test whether a simple best-offset alignment improves decoding compared to
Needleman–Wunsch.

## Method
- For each East/West pair, choose the offset in [-5, +5] that maximizes
  exact symbol matches in the overlapping region.
- Train run-position mapping `(index, east, prevEast)` using offset alignment.
- Evaluate in leave-one-out fashion.

## Results
- Pairs: **8**
- Aligned: **162**
- Covered: **144**
- Correct: **14**

## Interpretation
Offset alignment increases aligned/covered positions but **reduces accuracy**.
This suggests the flexible alignment is helping by preserving meaningful
matching structure, while simple offset matching overfits noisy local matches.

## Next Steps
- Use anchor-based alignment (e.g., fixed header motifs) rather than global
  offset alignment.
- Compare with constrained offsets derived from motif positions.
