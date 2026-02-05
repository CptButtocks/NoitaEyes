## NoitaEyes Agent Instructions

### Workflow
- Experiments are done in the test project (`NoitaEyes.Tests`).
- Each experiment results in a test set on the engine that validates the idea.
- If a hypothesis is proven false, add tests that confirm the negative result (no failing tests allowed).

### Source of Truth
- The `sources/` folder contains the current progress on the puzzle and should be consulted when forming or evaluating hypotheses.

### Experiment Documentation
- When an experiment is complete, its findings must be placed in the `experiments/` folder.
- When planning a new experiment, inspect the `experiments/` folder first to avoid redoing work.
