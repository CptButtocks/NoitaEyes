using System.Collections.Generic;
using System.Linq;

namespace NoitaEyes.Engine;

public sealed class EyeMessage
{
    public EyeMessage(int id, IEnumerable<string> lines)
    {
        if (lines is null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        var normalized = lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(l => l.Replace("5", string.Empty))
            .Where(l => l.Length > 0)
            .ToArray();
        ValidateLines(normalized);

        Id = id;
        Lines = normalized;
    }

    public int Id { get; }
    public IReadOnlyList<string> Lines { get; }
    public int Height => Lines.Count;
    public int Width => Lines.Count == 0 ? 0 : Lines.Max(l => l.Length);

    public static EyeMessage FromDigitString(int id, string digitsWithLineBreaks, char lineBreakToken = '5')
    {
        if (digitsWithLineBreaks is null)
        {
            throw new ArgumentNullException(nameof(digitsWithLineBreaks));
        }

        var lines = new List<string>();
        var current = new List<char>();

        foreach (var ch in digitsWithLineBreaks)
        {
            if (ch == lineBreakToken)
            {
                if (current.Count > 0)
                {
                    lines.Add(new string(current.ToArray()));
                    current.Clear();
                }
                continue;
            }

            current.Add(ch);
        }

        if (current.Count > 0)
        {
            lines.Add(new string(current.ToArray()));
        }

        return new EyeMessage(id, lines);
    }

    private static void ValidateLines(IReadOnlyList<string> lines)
    {
        for (var row = 0; row < lines.Count; row++)
        {
            var line = lines[row];
            if (line.Length == 0)
            {
                throw new ArgumentException($"Line {row} is empty.", nameof(lines));
            }

            for (var col = 0; col < line.Length; col++)
            {
                var ch = line[col];
                if (ch < '0' || ch > '4')
                {
                    throw new ArgumentException($"Invalid glyph '{ch}' at row {row}, col {col}.", nameof(lines));
                }
            }
        }
    }
}
