namespace NoitaEyes.Engine;

public sealed record EyePuzzleEngineOptions(
    double ColumnSpacing = 1.0,
    double RowSpacing = 1.0,
    double RowOffset = 0.5,
    bool BuildTrigrams = true
);
