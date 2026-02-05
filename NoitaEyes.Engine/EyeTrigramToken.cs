namespace NoitaEyes.Engine;

public readonly record struct EyeTrigramToken(
    int Index,
    TrigramOrientation Orientation,
    EyeGlyph First,
    EyeGlyph Second,
    EyeGlyph Third
)
{
    public string Base5 => $"{(int)First}{(int)Second}{(int)Third}";
    public int Base10Value => ((int)First * 25) + ((int)Second * 5) + (int)Third;
}
