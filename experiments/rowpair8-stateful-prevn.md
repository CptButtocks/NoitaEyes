# Row-Pair 8 Stateful Decode (Prev2 / Prev3)

## Goal
Test whether extending the state (prev2, prev3) improves row-pair 8 decoding
when using run-position mapping.

## Method
- Use leave-one-out training on all East/West pairs (8 total).
- Align sequences with Needleman–Wunsch.
- Mapping keys:
  - Prev2: `(index, east, prev1, prev2)`
  - Prev3: `(index, east, prev1, prev2, prev3)`
- Report overall coverage/accuracy and bucketed stats by index:
  - Bucket 0: 0–9
  - Bucket 1: 10–19
  - Bucket 2: 20+

## Results
Both Prev2 and Prev3 produce identical results:
- Pairs: **8**
- Aligned: **147**
- Covered: **114**
- Correct: **24**

Bucketed:
- Bucket 0: aligned **67**, covered **54**, correct **16**
- Bucket 1: aligned **64**, covered **58**, correct **8**
- Bucket 2: aligned **16**, covered **2**, correct **0**

## Interpretation
Adding more history (prev2 or prev3) does **not** improve accuracy or coverage.
The signal remains concentrated in early columns, and deeper state does not
resolve late-column ambiguity.

## Next Steps
- Try bucketed state models that generalize across nearby columns.
- Combine state with coarse bin constraints rather than exact values.
