# HMM Header/Body Initialization (3-4 States)

## Goal
Test whether more states with header/body initialization improve HMM fit over
the unigram baseline.

## Method
- Train 3-state and 4-state HMMs with header/body position initialization.
- Use 5 Baum–Welch iterations on full trigram sequences.
- Compare per-token log-likelihood against unigram baseline.

## Results
- 3-state HMM: **-25.9105** per token
- 4-state HMM: **-25.9335** per token
- Unigram baseline: **-4.3478** per token

## Interpretation
Both 3- and 4-state HMMs remain far worse than the unigram baseline, so
header/body initialization alone is insufficient. The model likely needs
stronger supervision or a different structure.

## Next Steps
- Try supervised initialization by row-pair phase or header motif class.
- Evaluate HMMs on body-only sequences to reduce header dominance.
