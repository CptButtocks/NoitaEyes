# High-Frequency Transition Motifs

## Goal
Identify top transitions and test whether they are stable across messages or
concentrated in specific early positions, suggesting fixed “function” tokens.

## Method
- Build per-message transition lists from canonical weave trigrams.
- Rank transitions by global count.
- Inspect per-message presence and exact index/row-pair positions.

## Top transitions (global counts)
- 66->5 (9)
- 75->54 (7)
- 5->49 (7)
- 49->75 (6)
- 62->13 (5)
- 60->29 (4)
- 2->60 (4)
- 54->2 (4)
- 66->62 (4)
- 29->40 (4)

## Motifs and placements
Universal transition:
- 66->5 appears **once in every message** at **index 1**, row-pair 0.

Early chain in messages 3–8:
- 5->49 at index 2 (row-pair 0)
- 49->75 at index 3 (row-pair 0)
- 75->54 at index 4 (row-pair 0)

Extra occurrences:
- Message 8 adds 5->49 at index 36 (row-pair 2) and 75->54 at index 67 (row-pair 4).

Early transition in messages 0–2:
- 62->13 at index 4 (row-pair 0).
- Message 3 shows 62->13 later at indices 35 (row-pair 2) and 66 (row-pair 4).

## Interpretation
The most frequent transition (66->5) is fixed across all messages at the same
early position, and a short chain (5->49->75->54) repeats at indices 2–4 in six
messages. These look like structural “header” patterns rather than random
coincidences, supporting the idea of fixed function-like tokens.

## Next Steps
- Check whether these motifs align with specific glyph coordinates (overlay).
- Test whether top transitions cluster around other fixed indices (e.g., line starts).
