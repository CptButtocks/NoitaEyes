# Data Audit: Source vs Engine Messages

## Goal
Confirm that engine-embedded messages match the canonical source document.

## Method
- Parse `sources/Noita Eye Glyph Messages.md` (embedded resource) for message
  blocks.
- Accept only digit-only lines and strip line-break marker `5`.
- Compare per-line strings against `EyeMessage` data from the engine.

## Results
- sourceCount **9**
- engineCount **9**
- mismatchCount **0**

## Interpretation
The embedded engine data matches the canonical source document once line-break
markers are removed. The dataset is consistent and complete for the 9 messages
in the source.

## Next Steps
- If new sources are discovered, integrate and re-run this audit.
