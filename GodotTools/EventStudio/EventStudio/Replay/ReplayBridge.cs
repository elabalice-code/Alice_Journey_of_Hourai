using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace EventStudio.Replay;

internal sealed class ReplayRequest
{
    public string Command { get; set; } = "";
    public string Target { get; set; } = "";
    public string Value { get; set; } = "";
    public int DurationMs { get; set; } = 800;
}

internal sealed class ReplayResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string ActiveDialog { get; set; } = "";
}

internal static class ReplayBridge
{
    internal const string PipeName = "EventStudioReplayPipe";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    internal static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidDataException("Replay payload is invalid.");

    internal static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(one, 0, 1, ct);
            if (n == 0)
            {
                break;
            }
            if (one[0] == (byte)'\n')
            {
                break;
            }
            ms.WriteByte(one[0]);
        }
        return Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\r');
    }

    internal static async Task WriteLineAsync(Stream stream, string content, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(content + "\n");
        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        await stream.FlushAsync(ct);
    }

    internal static NamedPipeServerStream CreateServer() =>
        new(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
}
