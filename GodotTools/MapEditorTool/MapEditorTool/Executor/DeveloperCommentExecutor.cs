using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MapEditorTool.Executor
{
    // DeveloperCommentExecutor is the terminal mailbox writer. Do not inline or remove this side effect from UI/ViewModel/SignalWeaver.
    public sealed class DeveloperCommentExecutor
    {
        private readonly string _commentLogPath;

        public DeveloperCommentExecutor(string baseDirectory)
        {
            _commentLogPath = Path.Combine(
                baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
                "logs",
                "developer-comments.log");
        }

        public string CommentLogPath
        {
            get { return _commentLogPath; }
        }

        public void WriteComment(string source, string comment)
        {
            EnsureCommentLogFile();

            var safeComment = (comment ?? string.Empty)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            var line = string.Format(
                "{0:yyyy-MM-dd HH:mm:ss.fff zzz}\t{1}\t{2}",
                DateTimeOffset.Now,
                source ?? string.Empty,
                safeComment);

            File.AppendAllText(_commentLogPath, line + Environment.NewLine, Encoding.UTF8);
        }

        public string EnsureCommentLogFile()
        {
            var directory = Path.GetDirectoryName(_commentLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            if (!File.Exists(_commentLogPath))
                File.WriteAllText(_commentLogPath, string.Empty, Encoding.UTF8);

            return _commentLogPath;
        }

        public string OpenCommentLog()
        {
            var path = EnsureCommentLogFile();
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return path;
        }
    }
}
