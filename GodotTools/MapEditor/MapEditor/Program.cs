using System.Windows.Forms;
using MapEditor.Cli;

namespace MapEditor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            NativeConsole.EnsureConsole();
            var code = CliEntry.Run(args);
            Environment.Exit(code);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
