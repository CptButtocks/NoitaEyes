# Row-Pair 8 Early/Late Alignment

## Goal
Check whether early columns carry more cross-message structure and whether a
rhythm-anchored alignment improves decoding.

## Method
- Run-position decoding with Needleman–Wunsch alignment.
- Split evaluation into early (index < 10) and late (index >= 10) columns.
- Rhythm-anchored alignment: compute row-pair 8 start index in body row-pair
  sequence, align by offset (no gaps), then decode.

## Results
Early vs late:
- early covered **54**, correct **16**
- late covered **60**, correct **8**

Rhythm-anchored alignment:
- pairs **8**, aligned **170**, covered **164**, correct **14**

## Interpretation
Early columns are materially more predictable than late columns. The rhythm
anchor alignment increases coverage but reduces accuracy relative to the
Needleman–Wunsch baseline, suggesting that the anchor offset aligns noise rather
than structure.

## Next Steps
- Focus decoding on early columns with digit-level constraints.
- Try alternate anchors (row-pair run boundaries or header offsets).
- Evaluate early-only decoding with conditional digit mappings.
