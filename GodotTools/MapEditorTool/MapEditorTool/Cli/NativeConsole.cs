using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MapEditorTool.Cli
{
    internal static class NativeConsole
    {
        private const uint AttachParentProcess = 0xFFFFFFFF;

        public static void EnsureConsole()
        {
            if (!Console.IsOutputRedirected && !Console.IsErrorRedirected && !HasConsole())
            {
                if (!AttachConsole(AttachParentProcess))
                    AllocConsole();
            }

            RebindStandardWriters();
        }

        private static void RebindStandardWriters()
        {
            TrySetWriter(Console.OpenStandardOutput, Console.SetOut);
            TrySetWriter(Console.OpenStandardError, Console.SetError);
        }

        private static void TrySetWriter(Func<Stream> openStream, Action<TextWriter> setWriter)
        {
            try
            {
                var stream = openStream();
                if (stream == Stream.Null)
                    return;

                setWriter(new StreamWriter(stream, Console.OutputEncoding) { AutoFlush = true });
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool HasConsole()
        {
            return GetConsoleWindow() != IntPtr.Zero;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();
    }
}
