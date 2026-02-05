# Transition Graph Analysis

Date: 2026-02-04

## Goal
Build the trigram transition graph and quantify graph structure (sources, sinks, hubs, SCCs) to evaluate grammar-like behavior.

## Method
- Use canonical weave trigrams across all messages.
- Build a directed weighted transition graph (edge = observed trigram transition).
- Compute in/out degree, identify hubs (out-degree >= 10), sources (in-degree 0), and sinks (out-degree 0).
- Compute strongly connected components (SCCs) via DFS on the reversed graph.

## Results
- Nodes: 83
- Unique edges: 858
- Total transitions: 1035
- Sources: 0
- Sinks: 0
- SCCs: 1 (largest size 83)
- Hub count (out-degree >= 10): 49
- Top hubs (by out-degree): 26, 60, 54, 64, 5, 13, 21, 25, 11, 47

## Interpretation
- The graph is fully strongly connected (single SCC), suggesting rich mutual reachability between trigrams.
- No sources or sinks indicates trigrams participate in transitions both entering and leaving.
- Many hubs point to grammar-like structure where some trigrams act as frequent connectors/particles.

## Implementation
- Engine: `NoitaEyes.Engine/TrigramAnalysis.cs` (AnalyzeTransitionGraph)
- Tests: `NoitaEyes.Tests/HypothesisTests.cs` (transition graph metrics + hub list)
