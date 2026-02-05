using System.Collections.Generic;
using System.Linq;

namespace NoitaEyes.Engine;

public sealed class EyePuzzleEngine
{
    private readonly EyePuzzleEngineOptions _options;
    private readonly EyeMessageRepository _repository;

    public EyePuzzleEngine(EyePuzzleEngineOptions? options = null, EyeMessageRepository? repository = null)
    {
        _options = options ?? new EyePuzzleEngineOptions();
        _repository = repository ?? new EyeMessageRepository();
    }

    public EyeMessageRepository Repository => _repository;

    public EyePuzzleLayout Generate(int messageId)
    {
        var message = _repository.Get(messageId);
        var cells = BuildCells(message);
        var trigrams = _options.BuildTrigrams
            ? (IReadOnlyList<EyeTrigram>)BuildTrigrams(message)
            : Array.Empty<EyeTrigram>();

        var width = cells.Count == 0
            ? 0
            : cells.Max(cell => cell.X) + _options.ColumnSpacing;
        var height = cells.Count == 0
            ? 0
            : cells.Max(cell => cell.Y) + _options.RowSpacing;

        return new EyePuzzleLayout(message, cells, trigrams, width, height);
    }

    public IReadOnlyList<EyeMessage> ListMessages()
    {
        return _repository.All;
    }

    private List<EyeGlyphCell> BuildCells(EyeMessage message)
    {
        var cells = new List<EyeGlyphCell>();
        for (var row = 0; row < message.Lines.Count; row++)
        {
            var line = message.Lines[row];
            // Every other row is horizontally offset to form the triangular mesh.
            var xOffset = (row % 2 == 1) ? _options.RowOffset : 0.0;
            for (var col = 0; col < line.Length; col++)
            {
                var glyph = ParseGlyph(line[col]);
                var x = (col * _options.ColumnSpacing) + xOffset;
                var y = row * _options.RowSpacing;
                cells.Add(new EyeGlyphCell(row, col, glyph, x, y));
            }
        }

        return cells;
    }

    private List<EyeTrigram> BuildTrigrams(EyeMessage message)
    {
        var trigrams = new List<EyeTrigram>();

        for (var row = 0; row < message.Lines.Count - 1; row++)
        {
            var top = message.Lines[row];
            var bottom = message.Lines[row + 1];
            var topOffset = (row % 2 == 1);

            for (var col = 0; col < top.Length - 1; col++)
            {
                var bottomCol = topOffset ? col + 1 : col;
                if (bottomCol < 0 || bottomCol >= bottom.Length)
                {
                    continue;
                }

                // Clockwise order for a downward-pointing triangle: top-left, top-right, bottom.
                var first = ParseGlyph(top[col]);
                var second = ParseGlyph(top[col + 1]);
                var third = ParseGlyph(bottom[bottomCol]);

                trigrams.Add(new EyeTrigram(row, col, first, second, third));
            }
        }

        return trigrams;
    }

    private static EyeGlyph ParseGlyph(char ch)
    {
        if (ch < '0' || ch > '4')
        {
            throw new ArgumentOutOfRangeException(nameof(ch), $"Invalid glyph digit '{ch}'.");
        }

        return (EyeGlyph)(ch - '0');
    }
}
