# Body Segmentation: Row-Pair Rhythm

## Goal
Analyze the sequence of row-pair indices in the body (indices 6..end) to see
whether there is a consistent "rhythm" across messages, beyond aggregate counts.

## Method
- Use motif-based header segmentation (header length = 6).
- Extract the row-pair sequence for the body.
- Count row-pair transitions (from -> to) within the body.
- Compare per-message transition profiles and global totals.

## Global transition totals
Top transitions across all bodies:
- 2->2: 225
- 4->4: 225
- 6->6: 217
- 0->0: 171
- 8->8: 95
- 0->2: 9
- 2->4: 9
- 4->6: 9
- 6->8: 6
- 10->10: 6
- 8->10: 1

## Per-message transition profiles (non-zero)
- 0: 2->2:25, 4->4:25, 6->6:20, 0->0:19, 0->2:1, 2->4:1, 4->6:1
- 1: 2->2:25, 4->4:25, 6->6:24, 0->0:19, 0->2:1, 2->4:1, 4->6:1
- 2: 2->2:25, 4->4:25, 6->6:25, 0->0:19, 8->8:13, 0->2:1, 2->4:1, 4->6:1, 6->8:1
- 3: 2->2:25, 4->4:25, 6->6:23, 0->0:19, 0->2:1, 2->4:1, 4->6:1
- 4: 2->2:25, 4->4:25, 6->6:25, 8->8:25, 0->0:19, 10->10:6, 0->2:1, 2->4:1, 4->6:1, 6->8:1, 8->10:1
- 5: 2->2:25, 4->4:25, 6->6:25, 0->0:19, 8->8:19, 0->2:1, 2->4:1, 4->6:1, 6->8:1
- 6: 2->2:25, 4->4:25, 6->6:25, 0->0:19, 8->8:14, 0->2:1, 2->4:1, 4->6:1, 6->8:1
- 7: 2->2:25, 4->4:25, 6->6:25, 0->0:19, 8->8:15, 0->2:1, 2->4:1, 4->6:1, 6->8:1
- 8: 2->2:25, 4->4:25, 6->6:25, 0->0:19, 8->8:9, 0->2:1, 2->4:1, 4->6:1, 6->8:1

## Interpretation
The body rhythm is highly regular:
- Most transitions are self-transitions, indicating long runs of the same row-pair.
- Every message contains exactly one 0->2, 2->4, and 4->6 transition.
- A single 6->8 transition appears only in messages where row-pair 8 is present.
- Message 4 is the only one to introduce row-pair 10, via a single 8->10 step and six 10->10 repeats.

This looks like a fixed row-pair progression with optional late-stage expansions
(row-pair 8, then 10), rather than free-form mixing.

## Next Steps
- Extract the exact row-pair sequences and see if their lengths correlate with
  message length or any global summary (sum, unique count).
- Search for row-pair rhythm "sub-phrases" around the first 6->8 transition.
- Build a small row-pair state machine that enforces the observed transitions
  and validate it against the body sequence.
