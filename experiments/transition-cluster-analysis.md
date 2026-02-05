# Transition Cluster Analysis

## Goal
Identify cluster structure in the trigram transition graph by filtering to
stronger (more frequent) transitions. This tests the "grammar-like" idea
from the research notes by looking for a dense core vs. sparse periphery.

## Method
- Build the transition graph from the canonical weave trigram stream.
- Keep only edges with weight >= N.
- Treat the filtered graph as undirected and compute connected components.
- Record cluster counts, largest cluster size, and singletons.

Engine helper:
- `NoitaEyes.Engine/TrigramAnalysis.cs` -> `AnalyzeTransitionClusters`

## Results
Edge weight >= 2:
- clusters: 23
- largest cluster: 59
- singletons: 20
- cluster sizes: 59,2,2,1 x20

Edge weight >= 3:
- clusters: 48
- largest cluster: 34
- singletons: 45
- cluster sizes: 34,2,2,1 x45

Edge weight >= 4 (for reference):
- clusters: 73
- largest cluster: 11
- singletons: 72

### Largest cluster membership
Edge weight >= 2:
- largest cluster members (59): 0,1,2,3,5,8,9,11,13,14,15,16,17,18,19,20,21,24,25,26,28,29,30,31,32,34,36,40,41,42,43,44,46,47,48,49,52,54,55,57,59,60,61,62,63,64,65,66,67,68,70,71,73,75,76,78,79,80,81

Edge weight >= 3:
- largest cluster members (34): 1,2,3,5,8,9,13,14,15,17,18,19,21,24,29,32,36,40,42,47,48,49,54,55,59,60,61,62,66,68,70,75,78,81

Edge weight >= 4:
- largest cluster members (11): 2,5,13,29,40,49,54,60,62,66,75

### East-only vs West-paired subsets
West-paired messages (W1..W4) at weight >= 2:
- clusters: 62
- largest cluster: 20
- singletons: 59
- largest members: 0,5,9,13,17,19,25,31,34,43,46,47,48,49,54,57,62,66,67,75

East-only message (E5) at weight >= 2:
- clusters: 61
- largest cluster: 2
- singletons: 59
- largest members: 5,49

West-paired messages (W1..W4) at weight >= 3:
- clusters: 78
- largest cluster: 5
- singletons: 76
- largest members: 5,49,54,66,75

East-only message (E5) at weight >= 3:
- clusters: 63
- largest cluster: 1
- singletons: 63

### East-paired vs West-paired subsets
East-paired messages (E1..E4) at weight >= 2:
- clusters: 52
- largest cluster: 27
- singletons: 47
- largest members: 1,2,3,5,8,13,14,15,24,26,29,32,40,42,48,49,54,60,61,62,64,65,66,70,75,78,81

West-paired messages (W1..W4) at weight >= 2:
- clusters: 62
- largest cluster: 20
- singletons: 59
- largest members: 0,5,9,13,17,19,25,31,34,43,46,47,48,49,54,57,62,66,67,75

East-paired messages (E1..E4) at weight >= 3:
- clusters: 82
- largest cluster: 2
- singletons: 81
- largest members: 5,66

West-paired messages (W1..W4) at weight >= 3:
- clusters: 78
- largest cluster: 5
- singletons: 76
- largest members: 5,49,54,66,75

## Interpretation
Filtering to stronger transitions reveals a dense core that stays connected,
with many tokens dropping into isolated singletons as the threshold rises.
This aligns with a grammar-like structure that has a tightly connected
backbone plus weaker, more specialized transitions.

## Next Steps
- Compare East-paired (E1..E4) vs West-paired (W1..W4) cluster membership.
- Map largest-cluster trigrams to their positions/orientations inside messages.
