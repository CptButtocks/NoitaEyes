# Trigram Sum and GCD Properties

Date: 2026-02-04

## Goal
Validate numeric properties reported in progress notes for the canonical trigram weave.

## Method
- Use canonical weave trigrams from the engine.
- Compute per-message trigram sums.
- Check for two-digit prime factors of sums.
- Compute GCD of the first two trigrams in each message.

## Results
- Per-message sums: 4040, 4124, 4754, 4295, 5656, 4748, 5385, 4936, 4545 (message order 0..8).
- E1/E3/E5 sums follow ABAB pattern: 4040, 5656, 4545.
- No sum has a two-digit prime factor (11–97).
- First two trigrams in every message have GCD > 1.

## Implementation
- Tests: `NoitaEyes.Tests/HypothesisTests.cs` (sum validation, ABAB pattern, prime-factor exclusion, GCD > 1).
