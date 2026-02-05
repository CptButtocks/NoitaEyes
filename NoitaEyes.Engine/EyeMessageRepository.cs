using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace NoitaEyes.Engine;

public sealed class EyeMessageRepository
{
    private const string ResourceName = "NoitaEyes.Engine.data.messages.json";

    private readonly IReadOnlyDictionary<int, EyeMessage> _messages;

    public EyeMessageRepository()
    {
        _messages = LoadEmbeddedMessages();
    }

    public IReadOnlyList<EyeMessage> All => _messages.Values.OrderBy(m => m.Id).ToArray();

    public EyeMessage Get(int id)
    {
        if (_messages.TryGetValue(id, out var message))
        {
            return message;
        }

        throw new KeyNotFoundException($"Message {id} not found.");
    }

    private static IReadOnlyDictionary<int, EyeMessage> LoadEmbeddedMessages()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            var names = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found. Available: {names}");
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
                  ?? throw new InvalidOperationException("Failed to parse messages.json.");

        var dict = new Dictionary<int, EyeMessage>();
        foreach (var (key, lines) in raw)
        {
            if (!int.TryParse(key, out var id))
            {
                throw new InvalidOperationException($"Invalid message key '{key}'.");
            }

            var joined = string.Concat(lines);
            dict[id] = EyeMessage.FromDigitString(id, joined);
        }

        return new ReadOnlyDictionary<int, EyeMessage>(dict);
    }
}
