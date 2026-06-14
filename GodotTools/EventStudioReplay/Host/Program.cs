using System.IO.Pipes;
using System.Text;
using EventStudioReplay.Shared;

namespace EventStudioReplayHost;

internal static class Program
{
    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["main.txtProjectName"] = "PrologueFlow",
        ["main.txtStartEvent"] = "evt_prologue_start",
        ["main.txtLastExport"] = "",
        ["dialog.txtValue"] = ""
    };

    private static string _activeDialog = "";
    private static string _dialogContext = "";
    private static readonly List<string> EventFlow = [];
    private static readonly string[] Targets =
    [
        "main.btnNewProject",
        "main.btnStartEvent",
        "main.btnAddFlow",
        "main.btnValidateExport",
        "main.btnCloseDialog",
        "main.txtProjectName",
        "main.txtStartEvent",
        "main.txtLastExport",
        "dialog.txtValue",
        "dialog.btnConfirm",
        "dialog.btnCancel"
    ];

    private static async Task<int> Main(string[] args)
    {
        if (args.Any(x => x.Equals("--agent-self-test", StringComparison.OrdinalIgnoreCase) ||
                          x.Equals("agent-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunAgentSelfTest();
        }

        Console.WriteLine("EventStudioReplayHost started.");
        Console.WriteLine("Pipe: " + ReplayProtocol.PipeName);
        while (true)
        {
            using var server = new NamedPipeServerStream(ReplayProtocol.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            var line = await ReplayProtocol.ReadLineAsync(server, CancellationToken.None);
            var request = ReplayProtocol.Deserialize<ReplayRequest>(line);
            var response = Handle(request);
            var resp = ReplayProtocol.Serialize(response);
            await ReplayProtocol.WriteLineAsync(server, resp, CancellationToken.None);
            if (request.Command.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Host is shutting down.");
                return 0;
            }
        }
    }

    private static int RunAgentSelfTest()
    {
        var checks = new[]
        {
            Handle(new ReplayRequest { Command = "ping" }),
            Handle(new ReplayRequest { Command = "list-controls" }),
            Handle(new ReplayRequest { Command = "set-text", Target = "main.txtProjectName", Value = "AgentSelfTest" }),
            Handle(new ReplayRequest { Command = "click", Target = "main.btnAddFlow" }),
            Handle(new ReplayRequest { Command = "click", Target = "main.btnValidateExport" })
        };

        var failed = checks.FirstOrDefault(x => !x.Success);
        if (failed != null)
        {
            Console.Error.WriteLine("EventStudioReplayHost agent self-test failed: " + failed.Message);
            return 1;
        }

        Console.WriteLine("EventStudioReplayHost agent self-test OK.");
        return 0;
    }

    private static ReplayResponse Handle(ReplayRequest req)
    {
        var cmd = req.Command.Trim().ToLowerInvariant();
        return cmd switch
        {
            "ping" => Ok("alive"),
            "list" or "list-controls" => Ok(string.Join(Environment.NewLine, Targets)),
            "set-text" => HandleSetText(req.Target, req.Value),
            "click" => HandleClick(req.Target),
            "focus" or "activate" => Ok("focused " + req.Target),
            "close-dialog" => HandleCloseDialog(),
            "shutdown" => Ok("shutdown"),
            _ => Fail("unsupported command: " + req.Command)
        };
    }

    private static ReplayResponse HandleSetText(string target, string value)
    {
        if (!Fields.ContainsKey(target))
        {
            return Fail("target not found: " + target);
        }
        Fields[target] = value;
        return Ok("set-text " + target);
    }

    private static ReplayResponse HandleClick(string target)
    {
        switch (target)
        {
            case "main.btnNewProject":
                _activeDialog = "dlgNewProject";
                _dialogContext = "project";
                Fields["dialog.txtValue"] = Fields["main.txtProjectName"];
                return Ok("opened dlgNewProject");
            case "main.btnStartEvent":
                _activeDialog = "dlgStartEvent";
                _dialogContext = "start_event";
                Fields["dialog.txtValue"] = Fields["main.txtStartEvent"];
                return Ok("opened dlgStartEvent");
            case "main.btnAddFlow":
                EventFlow.Clear();
                EventFlow.Add("evt_game_start -> StartDialogue(dlg_prologue_0001) -> evt_after_dialogue");
                EventFlow.Add("evt_after_dialogue -> ChangeMap(res://CoreEngine/Maps/DiceRoom.tscn)");
                return Ok("flow added");
            case "main.btnValidateExport":
                return HandleExport();
            case "main.btnCloseDialog":
            case "dialog.btnCancel":
                return HandleCloseDialog();
            case "dialog.btnConfirm":
                return HandleDialogConfirm();
            default:
                return Fail("button target not found: " + target);
        }
    }

    private static ReplayResponse HandleDialogConfirm()
    {
        if (_activeDialog.Length == 0)
        {
            return Fail("no active dialog");
        }
        if (_dialogContext == "project")
        {
            Fields["main.txtProjectName"] = Fields["dialog.txtValue"];
        }
        else if (_dialogContext == "start_event")
        {
            Fields["main.txtStartEvent"] = Fields["dialog.txtValue"];
        }
        _activeDialog = "";
        _dialogContext = "";
        return Ok("dialog confirmed");
    }

    private static ReplayResponse HandleCloseDialog()
    {
        _activeDialog = "";
        _dialogContext = "";
        return Ok("dialog closed");
    }

    private static ReplayResponse HandleExport()
    {
        if (string.IsNullOrWhiteSpace(Fields["main.txtProjectName"]))
        {
            return Fail("project name empty");
        }
        if (string.IsNullOrWhiteSpace(Fields["main.txtStartEvent"]))
        {
            return Fail("start event empty");
        }
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "demo.runtime.events.json");
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"project\": \"{Escape(Fields["main.txtProjectName"])}\",");
        sb.AppendLine($"  \"startEventId\": \"{Escape(Fields["main.txtStartEvent"])}\",");
        sb.AppendLine("  \"events\": [");
        for (var i = 0; i < EventFlow.Count; i++)
        {
            var tail = i == EventFlow.Count - 1 ? "" : ",";
            sb.AppendLine($"    \"{Escape(EventFlow[i])}\"{tail}");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Fields["main.txtLastExport"] = path;
        return Ok("exported " + path);
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static ReplayResponse Ok(string msg) => new() { Success = true, Message = msg, ActiveDialog = _activeDialog };
    private static ReplayResponse Fail(string msg) => new() { Success = false, Message = msg, ActiveDialog = _activeDialog };
}
