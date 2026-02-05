# Body Run-Length Correlations

## Goal
Correlate the body run lengths (row-pairs 6/8/10) with message-wide properties
like body length, trigram sum, and unique trigram counts.

## Method
- Use motif-based header segmentation (header length = 6).
- Extract body row-pair runs and record run lengths for row-pairs 6, 8, and 10.
- Record message trigram sum and unique trigram count (canonical weave).
- Compute Pearson correlations.

## Per-message metrics
Format: id, bodyLen, sum, unique, run6, run8, run10.

- 0: 93, 4040, 57, 21, 0, 0
- 1: 97, 4124, 57, 25, 0, 0
- 2: 112, 4754, 62, 26, 14, 0
- 3: 96, 4295, 61, 24, 0, 0
- 4: 131, 5656, 67, 26, 26, 7
- 5: 118, 4748, 65, 26, 20, 0
- 6: 113, 5385, 62, 26, 15, 0
- 7: 114, 4936, 68, 26, 16, 0
- 8: 108, 4545, 63, 26, 10, 0

## Correlations (Pearson r)
- run6 vs bodyLen: 0.7219
- run8 vs bodyLen: 0.9885
- run10 vs bodyLen: 0.6745
- run6 vs sum: 0.6548
- run8 vs sum: 0.8916
- run10 vs sum: 0.6412
- run6 vs unique: 0.6778
- run8 vs unique: 0.8538
- run10 vs unique: 0.4407

## Interpretation
Body length is almost entirely driven by the row-pair 8 run length (r=0.9885),
with row-pair 6 showing a weaker but consistent relationship. The presence of
row-pair 10 is rare (only message 4) and does not strongly track unique counts.
This supports the idea that the late-stage row-pair expansions (8, 10) encode
most of the body size variation.

## Next Steps
- Compare run8 and run10 lengths to East/West grouping and header motif type.
- Overlay row-pair 8/10 segments onto glyph coordinates to see if they map to
  distinct visual bands.
