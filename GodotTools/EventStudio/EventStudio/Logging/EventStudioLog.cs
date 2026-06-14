namespace EventStudio.Logging;

internal static class EventStudioLog
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly string SessionStamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss_fff");

    public static string CurrentLogPath { get; } = Path.Combine(LogDirectory, $"EventStudio_{SessionStamp}.log");
    public static string LatestLogPath { get; } = Path.Combine(LogDirectory, "EventStudio.latest.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warning(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            var line = $"{timestamp} [{level}] {message}";
            if (exception != null)
            {
                line += Environment.NewLine + exception;
            }

            lock (Sync)
            {
                File.AppendAllText(CurrentLogPath, line + Environment.NewLine);
                File.AppendAllText(LatestLogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never become the user's next crash.
        }
    }
}
