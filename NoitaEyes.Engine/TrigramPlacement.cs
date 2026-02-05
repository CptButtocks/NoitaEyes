namespace NoitaEyes.Engine;

public readonly record struct TrigramPlacement(
    int MessageId,
    int Index,
    TrigramOrientation Orientation,
    int RowPair,
    TrigramVertex VertexA,
    TrigramVertex VertexB,
    TrigramVertex VertexC,
    TrigramVertex TokenFirst,
    TrigramVertex TokenSecond,
    TrigramVertex TokenThird
)
{
    public int TopRow => RowPair;
    public int BottomRow => RowPair + 1;
    public string Base5 => $"{(int)TokenFirst.Glyph}{(int)TokenSecond.Glyph}{(int)TokenThird.Glyph}";
    public int Base10Value => ((int)TokenFirst.Glyph * 25) + ((int)TokenSecond.Glyph * 5) + (int)TokenThird.Glyph;
}
