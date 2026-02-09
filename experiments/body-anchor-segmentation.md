# Body Anchor Segmentation (Top Motif)

## Goal
Test whether the most common short body motif can act as a segmentation anchor.

## Method
- Use body trigram sequences (post-header).
- Identify the most widely shared motif among length-2/3 candidates.
- Use its earliest occurrence to split each body into pre/post segments.
- Report coverage and segment length averages.

## Results
Anchor motif: **2-60** (length 2, coverage 4 messages)

Segmentation stats:
- Found in **4 / 9** messages
- Average anchor position: **0**
- Average pre length: **0**
- Average post length: **114.5**

## Interpretation
The top shared motif sits at the **start of the body** for the few messages
where it appears, so it does not provide a useful mid-body boundary. This
suggests that reliable mid-body anchors are either absent or rarer than
expected at short lengths.

## Next Steps
- Search for anchors that occur **after** a minimum offset (e.g., index >= 5).
- Expand anchor length to 4+ and allow approximate matches.
