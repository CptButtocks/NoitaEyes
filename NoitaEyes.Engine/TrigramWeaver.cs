using System.Collections.Generic;

namespace NoitaEyes.Engine;

public static class TrigramWeaver
{
    public static IReadOnlyList<EyeTrigramToken> BuildWeaveTrigrams(EyeMessage message, TrigramScheme? scheme = null)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        scheme ??= TrigramScheme.Canonical;
        var tokens = new List<EyeTrigramToken>();
        var index = 0;

        for (var row = 0; row < message.Lines.Count; row += 2)
        {
            if (row + 1 >= message.Lines.Count)
            {
                throw new InvalidOperationException($"Message {message.Id} has an odd number of rows ({message.Lines.Count}).");
            }

            var top = message.Lines[row];
            var bottom = message.Lines[row + 1];
            var topIndex = 0;
            var bottomIndex = 0;
            var orientation = scheme.StartOrientation;

            while (topIndex < top.Length || bottomIndex < bottom.Length)
            {
                if (orientation == TrigramOrientation.Down)
                {
                    if (topIndex + 1 >= top.Length || bottomIndex >= bottom.Length)
                    {
                        break;
                    }

                    var a = ParseGlyph(top[topIndex]);
                    var b = ParseGlyph(top[topIndex + 1]);
                    var c = ParseGlyph(bottom[bottomIndex]);
                    var (first, second, third) = scheme.DownPermutation.Apply(a, b, c);

                    tokens.Add(new EyeTrigramToken(index++, TrigramOrientation.Down, first, second, third));
                    topIndex += 2;
                    bottomIndex += 1;
                }
                else
                {
                    if (bottomIndex + 1 >= bottom.Length || topIndex >= top.Length)
                    {
                        break;
                    }

                    var a = ParseGlyph(bottom[bottomIndex]);
                    var b = ParseGlyph(bottom[bottomIndex + 1]);
                    var c = ParseGlyph(top[topIndex]);
                    var (first, second, third) = scheme.UpPermutation.Apply(a, b, c);

                    tokens.Add(new EyeTrigramToken(index++, TrigramOrientation.Up, first, second, third));
                    bottomIndex += 2;
                    topIndex += 1;
                }

                orientation = orientation == TrigramOrientation.Down
                    ? TrigramOrientation.Up
                    : TrigramOrientation.Down;
            }

            if (topIndex != top.Length || bottomIndex != bottom.Length)
            {
                throw new InvalidOperationException(
                    $"Weave did not consume all eyes for message {message.Id} row pair {row}/{row + 1}. " +
                    $"Consumed top {topIndex}/{top.Length}, bottom {bottomIndex}/{bottom.Length}.");
            }
        }

        return tokens;
    }

    public static IReadOnlyList<TrigramOccurrence> BuildWeaveOccurrences(EyeMessage message, TrigramScheme? scheme = null)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        scheme ??= TrigramScheme.Canonical;
        var occurrences = new List<TrigramOccurrence>();
        var index = 0;

        for (var row = 0; row < message.Lines.Count; row += 2)
        {
            if (row + 1 >= message.Lines.Count)
            {
                throw new InvalidOperationException($"Message {message.Id} has an odd number of rows ({message.Lines.Count}).");
            }

            var top = message.Lines[row];
            var bottom = message.Lines[row + 1];
            var topIndex = 0;
            var bottomIndex = 0;
            var orientation = scheme.StartOrientation;

            while (topIndex < top.Length || bottomIndex < bottom.Length)
            {
                if (orientation == TrigramOrientation.Down)
                {
                    if (topIndex + 1 >= top.Length || bottomIndex >= bottom.Length)
                    {
                        break;
                    }

                    var a = ParseGlyph(top[topIndex]);
                    var b = ParseGlyph(top[topIndex + 1]);
                    var c = ParseGlyph(bottom[bottomIndex]);
                    var (first, second, third) = scheme.DownPermutation.Apply(a, b, c);

                    occurrences.Add(new TrigramOccurrence(
                        message.Id,
                        index++,
                        TrigramOrientation.Down,
                        row,
                        topIndex,
                        bottomIndex,
                        first,
                        second,
                        third));

                    topIndex += 2;
                    bottomIndex += 1;
                }
                else
                {
                    if (bottomIndex + 1 >= bottom.Length || topIndex >= top.Length)
                    {
                        break;
                    }

                    var a = ParseGlyph(bottom[bottomIndex]);
                    var b = ParseGlyph(bottom[bottomIndex + 1]);
                    var c = ParseGlyph(top[topIndex]);
                    var (first, second, third) = scheme.UpPermutation.Apply(a, b, c);

                    occurrences.Add(new TrigramOccurrence(
                        message.Id,
                        index++,
                        TrigramOrientation.Up,
                        row,
                        topIndex,
                        bottomIndex,
                        first,
                        second,
                        third));

                    bottomIndex += 2;
                    topIndex += 1;
                }

                orientation = orientation == TrigramOrientation.Down
                    ? TrigramOrientation.Up
                    : TrigramOrientation.Down;
            }

            if (topIndex != top.Length || bottomIndex != bottom.Length)
            {
                throw new InvalidOperationException(
                    $"Weave did not consume all eyes for message {message.Id} row pair {row}/{row + 1}. " +
                    $"Consumed top {topIndex}/{top.Length}, bottom {bottomIndex}/{bottom.Length}.");
            }
        }

        return occurrences;
    }

    public static IReadOnlyList<TrigramPlacement> BuildWeavePlacements(EyeMessage message, TrigramScheme? scheme = null)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        scheme ??= TrigramScheme.Canonical;
        var placements = new List<TrigramPlacement>();
        var index = 0;

        for (var row = 0; row < message.Lines.Count; row += 2)
        {
            if (row + 1 >= message.Lines.Count)
            {
                throw new InvalidOperationException($"Message {message.Id} has an odd number of rows ({message.Lines.Count}).");
            }

            var top = message.Lines[row];
            var bottom = message.Lines[row + 1];
            var topIndex = 0;
            var bottomIndex = 0;
            var orientation = scheme.StartOrientation;

            while (topIndex < top.Length || bottomIndex < bottom.Length)
            {
                TrigramVertex vertexA;
                TrigramVertex vertexB;
                TrigramVertex vertexC;
                TrigramPermutation permutation;
                TrigramOrientation placementOrientation;

                if (orientation == TrigramOrientation.Down)
                {
                    if (topIndex + 1 >= top.Length || bottomIndex >= bottom.Length)
                    {
                        break;
                    }

                    var a = ParseGlyph(top[topIndex]);
                    var b = ParseGlyph(top[topIndex + 1]);
                    var c = ParseGlyph(bottom[bottomIndex]);
                    vertexA = new TrigramVertex(row, topIndex, a);
                    vertexB = new TrigramVertex(row, topIndex + 1, b);
                    vertexC = new TrigramVertex(row + 1, bottomIndex, c);
                    permutation = scheme.DownPermutation;
                    placementOrientation = TrigramOrientation.Down;

                    topIndex += 2;
                    bottomIndex += 1;
                }
                else
                {
                    if (bottomIndex + 1 >= bottom.Length || topIndex >= top.Length)
                    {
                        break;
                    }

                    var a = ParseGlyph(bottom[bottomIndex]);
                    var b = ParseGlyph(bottom[bottomIndex + 1]);
                    var c = ParseGlyph(top[topIndex]);
                    vertexA = new TrigramVertex(row + 1, bottomIndex, a);
                    vertexB = new TrigramVertex(row + 1, bottomIndex + 1, b);
                    vertexC = new TrigramVertex(row, topIndex, c);
                    permutation = scheme.UpPermutation;
                    placementOrientation = TrigramOrientation.Up;

                    bottomIndex += 2;
                    topIndex += 1;
                }

                var tokenFirst = GetVertex(vertexA, vertexB, vertexC, permutation.First);
                var tokenSecond = GetVertex(vertexA, vertexB, vertexC, permutation.Second);
                var tokenThird = GetVertex(vertexA, vertexB, vertexC, permutation.Third);

                placements.Add(new TrigramPlacement(
                    message.Id,
                    index++,
                    placementOrientation,
                    row,
                    vertexA,
                    vertexB,
                    vertexC,
                    tokenFirst,
                    tokenSecond,
                    tokenThird));

                orientation = orientation == TrigramOrientation.Down
                    ? TrigramOrientation.Up
                    : TrigramOrientation.Down;
            }

            if (topIndex != top.Length || bottomIndex != bottom.Length)
            {
                throw new InvalidOperationException(
                    $"Weave did not consume all eyes for message {message.Id} row pair {row}/{row + 1}. " +
                    $"Consumed top {topIndex}/{top.Length}, bottom {bottomIndex}/{bottom.Length}.");
            }
        }

        return placements;
    }

    private static EyeGlyph ParseGlyph(char ch)
    {
        if (ch < '0' || ch > '4')
        {
            throw new ArgumentOutOfRangeException(nameof(ch), $"Invalid glyph digit '{ch}'.");
        }

        return (EyeGlyph)(ch - '0');
    }

    private static TrigramVertex GetVertex(TrigramVertex a, TrigramVertex b, TrigramVertex c, int index)
    {
        return index switch
        {
            0 => a,
            1 => b,
            2 => c,
            _ => throw new ArgumentOutOfRangeException(nameof(index), "Permutation index must be 0, 1, or 2."),
        };
    }
}
