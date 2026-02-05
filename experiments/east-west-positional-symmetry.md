# East–West Positional Symmetry (Row-Pair Profiles)

## Goal
Test whether East-paired messages (E1..E4) are positionally closest to their
West-paired counterparts (W1..W4) using core-cluster row-pair distributions.

## Method
- Use the core cluster (largest cluster at weight >= 2).
- For each message, count core-trigram occurrences per row pair.
- Compute L1 distance between each East message and each West message.
- Compare nearest West for each East.

## Results
L1 distances (core row-pair counts):

```
     W1  W2  W3  W4
E1    8  10  25  24
E2   18  24  13  18
E3   36  34  13  24
E4   21  23  12  19
```

Nearest West per East:
- E1 -> W1 (paired)
- E2 -> W3
- E3 -> W3
- E4 -> W3

Only 2 of 4 East messages are closest to their paired West by this metric.

## Interpretation
Row-pair positional profiles of core-cluster trigrams do not align strongly
with the East/West pairing. This weakens the parallel-text hypothesis under
this specific positional metric, and suggests that any East–West relationship
is not simply captured by row-pair distribution of core tokens.

## Next Steps
- Repeat with alternative metrics (e.g., decile index histograms).
- Compare full-trigram row-pair profiles (not only core tokens).
