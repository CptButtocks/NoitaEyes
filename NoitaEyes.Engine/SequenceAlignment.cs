using System.Collections.Generic;
using System.Linq;

namespace NoitaEyes.Engine;

public sealed record AlignmentStep(int? IndexA, int? IndexB, int? ValueA, int? ValueB);

public sealed record SequenceAlignmentResult(int Score, IReadOnlyList<AlignmentStep> Steps)
{
    public int MatchCount => Steps.Count(step =>
        step.ValueA.HasValue &&
        step.ValueB.HasValue &&
        step.ValueA.Value == step.ValueB.Value);

    public int AlignedCount => Steps.Count(step => step.ValueA.HasValue && step.ValueB.HasValue);

    public int GapCount => Steps.Count(step => !step.ValueA.HasValue || !step.ValueB.HasValue);

    public int MismatchCount => AlignedCount - MatchCount;
}

public static class SequenceAlignment
{
    public static SequenceAlignmentResult Align(
        IReadOnlyList<int> a,
        IReadOnlyList<int> b,
        int matchScore = 2,
        int mismatchScore = -1,
        int gapScore = -1)
    {
        if (a is null)
        {
            throw new ArgumentNullException(nameof(a));
        }

        if (b is null)
        {
            throw new ArgumentNullException(nameof(b));
        }

        var rows = a.Count + 1;
        var cols = b.Count + 1;
        var score = new int[rows, cols];
        var direction = new AlignmentDirection[rows, cols];

        for (var i = 1; i < rows; i++)
        {
            score[i, 0] = score[i - 1, 0] + gapScore;
            direction[i, 0] = AlignmentDirection.Up;
        }

        for (var j = 1; j < cols; j++)
        {
            score[0, j] = score[0, j - 1] + gapScore;
            direction[0, j] = AlignmentDirection.Left;
        }

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var match = a[i - 1] == b[j - 1] ? matchScore : mismatchScore;
                var diag = score[i - 1, j - 1] + match;
                var up = score[i - 1, j] + gapScore;
                var left = score[i, j - 1] + gapScore;

                var best = diag;
                var bestDir = AlignmentDirection.Diagonal;

                if (up > best)
                {
                    best = up;
                    bestDir = AlignmentDirection.Up;
                }

                if (left > best)
                {
                    best = left;
                    bestDir = AlignmentDirection.Left;
                }

                score[i, j] = best;
                direction[i, j] = bestDir;
            }
        }

        var steps = new List<AlignmentStep>();
        var row = a.Count;
        var col = b.Count;

        while (row > 0 || col > 0)
        {
            if (row > 0 && col > 0 && direction[row, col] == AlignmentDirection.Diagonal)
            {
                steps.Add(new AlignmentStep(row - 1, col - 1, a[row - 1], b[col - 1]));
                row--;
                col--;
            }
            else if (row > 0 && (col == 0 || direction[row, col] == AlignmentDirection.Up))
            {
                steps.Add(new AlignmentStep(row - 1, null, a[row - 1], null));
                row--;
            }
            else
            {
                steps.Add(new AlignmentStep(null, col - 1, null, b[col - 1]));
                col--;
            }
        }

        steps.Reverse();
        return new SequenceAlignmentResult(score[a.Count, b.Count], steps);
    }

    public static SequenceAlignmentResult AlignAnchored(
        IReadOnlyList<int> a,
        IReadOnlyList<int> b,
        int anchorIndexA,
        int anchorIndexB,
        int anchorLength,
        int matchScore = 2,
        int mismatchScore = -1,
        int gapScore = -1)
    {
        if (a is null)
        {
            throw new ArgumentNullException(nameof(a));
        }

        if (b is null)
        {
            throw new ArgumentNullException(nameof(b));
        }

        if (anchorLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(anchorLength), "Anchor length must be positive.");
        }

        if (anchorIndexA < 0 || anchorIndexA + anchorLength > a.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(anchorIndexA), "Anchor index is out of bounds for sequence A.");
        }

        if (anchorIndexB < 0 || anchorIndexB + anchorLength > b.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(anchorIndexB), "Anchor index is out of bounds for sequence B.");
        }

        var prefixA = a.Take(anchorIndexA).ToArray();
        var prefixB = b.Take(anchorIndexB).ToArray();
        var suffixA = a.Skip(anchorIndexA + anchorLength).ToArray();
        var suffixB = b.Skip(anchorIndexB + anchorLength).ToArray();

        var prefix = Align(prefixA, prefixB, matchScore, mismatchScore, gapScore);
        var suffix = Align(suffixA, suffixB, matchScore, mismatchScore, gapScore);

        var steps = new List<AlignmentStep>();
        steps.AddRange(OffsetSteps(prefix.Steps, 0, 0));

        var anchorScore = 0;
        for (var i = 0; i < anchorLength; i++)
        {
            var valueA = a[anchorIndexA + i];
            var valueB = b[anchorIndexB + i];
            var score = valueA == valueB ? matchScore : mismatchScore;
            anchorScore += score;
            steps.Add(new AlignmentStep(anchorIndexA + i, anchorIndexB + i, valueA, valueB));
        }

        steps.AddRange(OffsetSteps(suffix.Steps, anchorIndexA + anchorLength, anchorIndexB + anchorLength));

        var totalScore = prefix.Score + anchorScore + suffix.Score;
        return new SequenceAlignmentResult(totalScore, steps);
    }

    private static IEnumerable<AlignmentStep> OffsetSteps(
        IReadOnlyList<AlignmentStep> steps,
        int offsetA,
        int offsetB)
    {
        foreach (var step in steps)
        {
            var indexA = step.IndexA.HasValue ? step.IndexA + offsetA : null;
            var indexB = step.IndexB.HasValue ? step.IndexB + offsetB : null;
            yield return new AlignmentStep(indexA, indexB, step.ValueA, step.ValueB);
        }
    }

    private enum AlignmentDirection
    {
        Diagonal,
        Up,
        Left,
    }
}
