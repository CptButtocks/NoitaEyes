namespace NoitaEyes.Engine;

public readonly record struct TrigramPermutation(int First, int Second, int Third)
{
    public static IReadOnlyList<TrigramPermutation> All { get; } = new[]
    {
        new TrigramPermutation(0, 1, 2),
        new TrigramPermutation(0, 2, 1),
        new TrigramPermutation(1, 0, 2),
        new TrigramPermutation(1, 2, 0),
        new TrigramPermutation(2, 0, 1),
        new TrigramPermutation(2, 1, 0),
    };

    public (EyeGlyph First, EyeGlyph Second, EyeGlyph Third) Apply(EyeGlyph a, EyeGlyph b, EyeGlyph c)
    {
        var first = GetIndex(a, b, c, First);
        var second = GetIndex(a, b, c, Second);
        var third = GetIndex(a, b, c, Third);
        return (first, second, third);
    }

    private static EyeGlyph GetIndex(EyeGlyph a, EyeGlyph b, EyeGlyph c, int index)
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
