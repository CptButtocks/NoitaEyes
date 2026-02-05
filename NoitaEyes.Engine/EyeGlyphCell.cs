namespace NoitaEyes.Engine;

public readonly record struct EyeGlyphCell(
    int Row,
    int Column,
    EyeGlyph Glyph,
    double X,
    double Y
);
