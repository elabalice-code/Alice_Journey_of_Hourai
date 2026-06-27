using System;
using System.Windows.Forms;
using MapEditorTool.Cli;
using MapEditorTool.UI;

namespace MapEditorTool
{
    internal static class Program
    {
        /// <summary>
        /// Main application entry point.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                NativeConsole.EnsureConsole();
                var code = CliEntry.Run(args);
                Environment.Exit(code);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
