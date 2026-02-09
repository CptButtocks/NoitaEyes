# Body Trigram Motifs (Post-Header)

## Goal
Measure how many short trigram motifs are shared across messages within the
body (after the motif-defined header).

## Method
- Use canonical weave trigram sequences per message.
- Define body as indices `headerEnd+1 .. end` (header via motif B/C).
- Count shared motifs that appear in **2+ messages**.
- Motif lengths: 2 and 3.
- Record the most widely shared motif for each length.

## Results
Shared motifs:
- Length-2: **89**
- Length-3: **46**

Top coverage motifs:
- Length-2: **2-60** (coverage 4 messages)
- Length-3: **2-60-29** (coverage 4 messages)

## Interpretation
Short body motifs do recur across messages, but coverage is limited:
the top motifs appear in only 4 of 9 messages. This suggests local structure
exists, but no globally shared body template emerges at these lengths.

## Next Steps
- Allow approximate matching (edit distance 1) to detect weakly conserved
  motifs.
- Use the highest-coverage motif as a potential anchor for segmentation.
