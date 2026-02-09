# HMM 2-State Log-Likelihood

## Goal
Test whether a simple 2-state HMM provides a better fit to the trigram
sequence data than a unigram baseline.

## Method
- Train a 2-state HMM on all trigram sequences (Baum–Welch, 5 iterations).
- Deterministic initialization: state = trigram value mod 2.
- Compute average log-likelihood per token for:
  - HMM model
  - Unigram baseline

## Results
- HMM log-likelihood per token: **-26.2913**
- Unigram log-likelihood per token: **-4.3478**

## Interpretation
The trained 2-state HMM performs **worse** than the unigram baseline, which
indicates this simplistic HMM (with current initialization/training) is not
capturing useful structure and may be overfitting poorly. This suggests that
either a different initialization, more states, or a different model class
is required.

## Next Steps
- Retry with 3–4 states and stronger smoothing.
- Use supervised state hints (e.g., header vs body) to initialize emissions.
