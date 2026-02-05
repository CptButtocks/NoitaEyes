using System.Collections.Generic;

namespace NoitaEyes.Engine;

public sealed class EyePuzzleLayout
{
    public EyePuzzleLayout(
        EyeMessage message,
        IReadOnlyList<EyeGlyphCell> cells,
        IReadOnlyList<EyeTrigram> trigrams,
        double width,
        double height)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        Trigrams = trigrams ?? throw new ArgumentNullException(nameof(trigrams));
        Width = width;
        Height = height;
    }

    public EyeMessage Message { get; }
    public IReadOnlyList<EyeGlyphCell> Cells { get; }
    public IReadOnlyList<EyeTrigram> Trigrams { get; }
    public double Width { get; }
    public double Height { get; }

    public EyeGlyphCell? GetCell(int row, int column)
    {
        foreach (var cell in Cells)
        {
            if (cell.Row == row && cell.Column == column)
            {
                return cell;
            }
        }

        return null;
    }
}
