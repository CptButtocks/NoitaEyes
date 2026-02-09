# Cell Entropy / Consensus Map

## Goal
Quantify per-cell variability across all messages to isolate any fixed scaffold
versus variable payload at the glyph-cell level.

## Method
- Use EyeMessage lines with line-break markers removed (digits 0-4 only).
- For each (row, col) cell, aggregate glyph values across all messages that
  contain that cell.
- Compute coverage (number of messages containing the cell), entropy (base-2),
  and max probability for each cell.
- Focus on the shared footprint (coverage >= 6), and the header region
  (rows 0/1, cols 0..8).
- Identify cells with entropy 0 (fixed across all messages that contain them).

## Results
### Coverage summary
- Total unique cells: 411
- Coverage distribution (messages per cell):
  - 9: 297
  - 8: 9
  - 7: 3
  - 6: 33
  - 5: 12
  - 4: 3
  - 3: 3
  - 2: 12
  - 1: 39

### Fixed cells (coverage = 9, entropy = 0)
Row,Col -> value
- (0,2) -> 1
- (0,3) -> 0
- (0,4) -> 1
- (1,1) -> 3
- (1,2) -> 2
- (1,3) -> 0
- (1,4) -> 4
- (1,5) -> 1

All fixed cells fall inside the header footprint (rows 0/1).

### Header (rows 0/1, cols 0..8, coverage >= 6)
- Cells: 18
- Fixed cells: 8
- Average entropy: 0.654
- High-entropy header cells (entropy > 1.5):
  - (0,0): counts 1:4, 2:2, 3:3
  - (0,1): counts 0:4, 1:3, 2:2
  - (1,0): counts 0:2, 1:2, 2:2, 3:2, 4:1

## Interpretation
Only a small scaffold is fixed across all messages, and it is concentrated in
rows 0/1 within the header. The leading header cells (0,0), (0,1), and (1,0)
are highly variable, matching the earlier observation that the first trigram
is message-specific. Beyond that, most of the grid remains variable, so the
payload is not confined solely to the late row-pair 8/10 bands.

## Next Steps
- Build an East vs West divergence map at the cell level to see which cells
  most strongly discriminate the two sides.
- Alternatively, apply a position-specific substitution test on the row-pair 8
  band (column-ordered pairs) to check for stateful East/West mappings.
