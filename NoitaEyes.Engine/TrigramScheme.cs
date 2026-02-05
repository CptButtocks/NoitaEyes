namespace NoitaEyes.Engine;

public sealed record TrigramScheme(
    TrigramPermutation DownPermutation,
    TrigramPermutation UpPermutation,
    TrigramOrientation StartOrientation = TrigramOrientation.Down
)
{
    public static TrigramScheme Canonical { get; } = new(
        new TrigramPermutation(0, 1, 2),
        new TrigramPermutation(1, 0, 2),
        TrigramOrientation.Down
    );

    public static IReadOnlyList<TrigramScheme> AllPermutations()
    {
        var list = new List<TrigramScheme>();
        foreach (var down in TrigramPermutation.All)
        {
            foreach (var up in TrigramPermutation.All)
            {
                list.Add(new TrigramScheme(down, up, TrigramOrientation.Down));
            }
        }

        return list;
    }
}
