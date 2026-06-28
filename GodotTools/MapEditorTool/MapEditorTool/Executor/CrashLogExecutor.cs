using System;
using System.IO;
using System.Text;

namespace MapEditorTool.Executor
{
    public sealed class CrashLogExecutor
    {
        private readonly string _crashLogPath;

        public CrashLogExecutor(string baseDirectory)
        {
            _crashLogPath = Path.Combine(
                baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
                "logs",
                "crash.log");
        }

        public string CrashLogPath
        {
            get { return _crashLogPath; }
        }

        public void WriteCrash(string source, Exception exception, bool isTerminating)
        {
            EnsureCrashLogFile();

            var builder = new StringBuilder();
            builder.AppendLine("============================================================");
            builder.AppendLine("Timestamp: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.AppendLine("Source: " + (source ?? string.Empty));
            builder.AppendLine("IsTerminating: " + isTerminating);
            builder.AppendLine("BaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory);
            builder.AppendLine("Exception:");
            builder.AppendLine(exception == null ? "(null exception)" : exception.ToString());

            File.AppendAllText(_crashLogPath, builder.ToString(), Encoding.UTF8);
        }

        public string EnsureCrashLogFile()
        {
            var directory = Path.GetDirectoryName(_crashLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            if (!File.Exists(_crashLogPath))
                File.WriteAllText(_crashLogPath, string.Empty, Encoding.UTF8);

            return _crashLogPath;
        }
    }
}
