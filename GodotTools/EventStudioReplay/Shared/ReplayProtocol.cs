using System.Text;
using System.Text.Json;

namespace EventStudioReplay.Shared;

public sealed class ReplayRequest
{
    public string Command { get; set; } = "";
    public string Target { get; set; } = "";
    public string Value { get; set; } = "";
    public int DurationMs { get; set; } = 800;
}

public sealed class ReplayResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string ActiveDialog { get; set; } = "";
}

public static class ReplayProtocol
{
    public const string PipeName = "EventStudioReplayPipe";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidDataException("Replay protocol payload is invalid.");

    public static async Task WriteLineAsync(Stream stream, string content, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(content + "\n");
        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(buffer, 0, 1, ct);
            if (n == 0)
            {
                break;
            }
            if (buffer[0] == (byte)'\n')
            {
                break;
            }
            ms.WriteByte(buffer[0]);
        }
        return Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\r');
    }
}
