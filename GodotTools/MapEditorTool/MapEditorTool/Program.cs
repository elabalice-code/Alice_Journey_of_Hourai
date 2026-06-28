using System;
using System.Threading;
using System.Windows.Forms;
using MapEditorTool.Cli;
using MapEditorTool.Executor;
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

            RegisterGlobalExceptionHandlers();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.Run(new Form1());
        }

        private static void RegisterGlobalExceptionHandlers()
        {
            // Keep both global handlers: WinForms UI-thread crashes and AppDomain crashes reach different events.
            Application.ThreadException += ApplicationThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
        }

        private static void ApplicationThreadException(object sender, ThreadExceptionEventArgs e)
        {
            WriteCrashLog("Application.ThreadException", e == null ? null : e.Exception, false);
            MessageBox.Show(
                "MapEditorTool caught an unexpected UI error and wrote logs/crash.log.",
                "MapEditorTool Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteCrashLog(
                "AppDomain.CurrentDomain.UnhandledException",
                e == null ? null : e.ExceptionObject as Exception,
                e != null && e.IsTerminating);
        }

        private static void WriteCrashLog(string source, Exception exception, bool isTerminating)
        {
            try
            {
                new CrashLogExecutor(AppDomain.CurrentDomain.BaseDirectory)
                    .WriteCrash(source, exception, isTerminating);
            }
            catch
            {
            }
        }
    }
}
