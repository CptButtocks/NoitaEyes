namespace NoitaEyes.Engine;

public readonly record struct TrigramOccurrence(
    int MessageId,
    int Index,
    TrigramOrientation Orientation,
    int RowPair,
    int TopIndex,
    int BottomIndex,
    EyeGlyph First,
    EyeGlyph Second,
    EyeGlyph Third
)
{
    public int TopRow => RowPair;
    public int BottomRow => RowPair + 1;
    public string Base5 => $"{(int)First}{(int)Second}{(int)Third}";
    public int Base10Value => ((int)First * 25) + ((int)Second * 5) + (int)Third;
}
