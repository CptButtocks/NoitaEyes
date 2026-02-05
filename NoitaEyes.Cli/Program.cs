using System.Text.Json;
using NoitaEyes.Engine;

var engine = new EyePuzzleEngine();
var argsList = args.ToList();

if (argsList.Contains("--list"))
{
    foreach (var message in engine.ListMessages())
    {
        Console.WriteLine($"{message.Id}: {message.Height} rows, width {message.Width}");
    }

    return;
}

var messageId = 0;
var format = "ascii";

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--message":
        case "-m":
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out messageId))
            {
                Console.Error.WriteLine("Missing or invalid message id after --message.");
                return;
            }
            i++;
            break;
        case "--format":
        case "-f":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Missing format after --format.");
                return;
            }
            format = args[i + 1].Trim().ToLowerInvariant();
            i++;
            break;
    }
}

var layout = engine.Generate(messageId);

switch (format)
{
    case "ascii":
        RenderAscii(layout);
        break;
    case "json":
        RenderJson(layout);
        break;
    case "trigrams":
        RenderTrigrams(layout);
        break;
    default:
        Console.Error.WriteLine("Unknown format. Use ascii, json, or trigrams.");
        break;
}

static void RenderAscii(EyePuzzleLayout layout)
{
    for (var row = 0; row < layout.Message.Lines.Count; row++)
    {
        var line = layout.Message.Lines[row];
        var prefix = (row % 2 == 1) ? " " : "";
        Console.WriteLine(prefix + line);
    }
}

static void RenderJson(EyePuzzleLayout layout)
{
    var payload = new
    {
        layout.Message.Id,
        layout.Message.Lines,
        layout.Width,
        layout.Height,
        Cells = layout.Cells.Select(cell => new
        {
            cell.Row,
            cell.Column,
            Glyph = (int)cell.Glyph,
            cell.X,
            cell.Y,
        }),
        Trigrams = layout.Trigrams.Select(trigram => new
        {
            trigram.Row,
            trigram.Column,
            Base5 = trigram.Base5,
            trigram.Base10Value,
        }),
    };

    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    Console.WriteLine(json);
}

static void RenderTrigrams(EyePuzzleLayout layout)
{
    foreach (var trigram in layout.Trigrams)
    {
        Console.WriteLine($"{trigram.Row},{trigram.Column}: {trigram.Base5} -> {trigram.Base10Value}");
    }
}
