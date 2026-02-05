# Motif-Segmented Grammar (Header Templates)

## Goal
Use high-frequency motifs as anchors to segment each message into a short
"header" and test whether the header follows a fixed template.

## Method
- Build canonical weave trigram sequences per message.
- Define two early motifs:
  - Motif B: [5, 49, 75, 54]
  - Motif C: [62, 13]
- For each message, pick the earliest occurrence of motif B or C.
- Define the header as indices 0..end of the chosen motif.
- Compare header length and header tail templates across messages.

## Results
All messages produce a header of length 6. The motif anchor start positions are:
- Motif B at index 2 for messages 3..8 (6 messages).
- Motif C at index 4 for messages 0..2 (3 messages).

Header tail templates (indices 1..5) are fixed by motif type:
- Motif B header tail: [66, 5, 49, 75, 54]
- Motif C header tail: [66, 5, 48, 62, 13]

The first trigram (index 0) varies by message, while the remaining five values
are fixed within each motif group. This yields two stable header templates with
a message-specific lead trigram.

## Interpretation
This supports a grammar-like generator with a fixed header structure:
the process chooses one of two header templates, then injects a variable
leading trigram. The shared tail suggests a strong structural "prefix" before
the body of the message begins.

## Next Steps
- Segment the remaining body (indices 6..end) and cluster by row-pair patterns.
- Test whether the variable lead trigram correlates with message-specific traits
  (sum, unique count, or East/West grouping).
- Search for additional mid-body anchors that might define secondary phrase
  boundaries.
