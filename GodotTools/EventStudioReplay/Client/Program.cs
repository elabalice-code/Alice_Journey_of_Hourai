using System.IO.Pipes;
using EventStudioReplay.Shared;

namespace EventStudioReplayClient;

internal static class Program
{
    private sealed class Step
    {
        public int Index { get; set; }
        public int LineNumber { get; set; }
        public string Raw { get; set; } = "";
    }

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Any(x => x.Equals("--agent-self-test", StringComparison.OrdinalIgnoreCase) ||
                              x.Equals("agent-self-test", StringComparison.OrdinalIgnoreCase)))
            {
                return RunAgentSelfTest();
            }

            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            if (args[0].Equals("script", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("missing script path");
                    return 1;
                }
                return await RunScript(args[1]);
            }

            if (args[0].Equals("script-list", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("missing script path");
                    return 1;
                }
                return ListScript(args[1]);
            }

            if (args[0].Equals("script-step", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    Console.Error.WriteLine("missing script path or step number");
                    return 1;
                }
                return await RunScriptStep(args[1], args[2]);
            }

            return await ExecuteRaw(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunAgentSelfTest()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "EventStudioReplayClient.AgentSelfTest.replay.txt");
        File.WriteAllLines(tempPath,
        [
            "# ignored",
            "delay 1",
            "set-text-b64 main.txtProjectName QWdlbnRTZWxmVGVzdA=="
        ]);

        var steps = LoadSteps(tempPath);
        if (steps.Count != 2)
        {
            Console.Error.WriteLine("EventStudioReplayClient agent self-test failed script loading.");
            return 1;
        }

        var tokens = Tokenize(steps[1].Raw);
        if (tokens.Length != 3 || tokens[0] != "set-text-b64" || DecodeBase64(tokens[2]) != "AgentSelfTest")
        {
            Console.Error.WriteLine("EventStudioReplayClient agent self-test failed token parsing.");
            return 1;
        }

        Console.WriteLine("EventStudioReplayClient agent self-test OK.");
        return 0;
    }

    private static async Task<int> RunScript(string path)
    {
        var steps = LoadSteps(path);
        foreach (var step in steps)
        {
            Console.WriteLine($"[step] {step.Index}/{steps.Count} line={step.LineNumber} {step.Raw}");
            var code = await ExecuteRaw(Tokenize(step.Raw));
            if (code != 0)
            {
                return code;
            }
        }
        return 0;
    }

    private static int ListScript(string path)
    {
        var steps = LoadSteps(path);
        foreach (var s in steps)
        {
            Console.WriteLine($"[step] {s.Index} line={s.LineNumber} {s.Raw}");
        }
        return 0;
    }

    private static async Task<int> RunScriptStep(string path, string stepText)
    {
        if (!int.TryParse(stepText, out var idx) || idx <= 0)
        {
            Console.Error.WriteLine("step must be positive integer");
            return 1;
        }
        var steps = LoadSteps(path);
        if (idx > steps.Count)
        {
            Console.Error.WriteLine("step out of range");
            return 1;
        }
        return await ExecuteRaw(Tokenize(steps[idx - 1].Raw));
    }

    private static async Task<int> ExecuteRaw(string[] args)
    {
        var list = new List<string>(args);
        var delay = PopOptionInt(list, "--delay", 0);
        var duration = PopOptionInt(list, "--duration", 800);
        if (delay > 0)
        {
            await Task.Delay(delay);
        }
        if (list.Count == 0)
        {
            return 1;
        }
        if (list[0].Equals("delay", StringComparison.OrdinalIgnoreCase))
        {
            if (list.Count < 2 || !int.TryParse(list[1], out var d) || d < 0)
            {
                Console.Error.WriteLine("delay <ms>");
                return 1;
            }
            await Task.Delay(d);
            return 0;
        }

        var command = list[0].ToLowerInvariant();
        var target = list.Count > 1 ? list[1] : "";
        var value = list.Count > 2 ? string.Join(" ", list.Skip(2)) : "";
        if (command == "set-text-b64")
        {
            command = "set-text";
            value = DecodeBase64(value);
        }
        var response = await Send(new ReplayRequest
        {
            Command = command,
            Target = target,
            Value = value,
            DurationMs = duration
        });
        if (response.Success)
        {
            Console.WriteLine("[ok] " + response.Message);
            return 0;
        }
        Console.Error.WriteLine("[error] " + response.Message);
        return 1;
    }

    private static async Task<ReplayResponse> Send(ReplayRequest request)
    {
        using var pipe = new NamedPipeClientStream(".", ReplayProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(3000);
        var req = ReplayProtocol.Serialize(request);
        await ReplayProtocol.WriteLineAsync(pipe, req, CancellationToken.None);
        var line = await ReplayProtocol.ReadLineAsync(pipe, CancellationToken.None);
        return ReplayProtocol.Deserialize<ReplayResponse>(line);
    }

    private static List<Step> LoadSteps(string path)
    {
        var full = Path.GetFullPath(path);
        var lines = File.ReadAllLines(full);
        var steps = new List<Step>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
            {
                continue;
            }
            steps.Add(new Step { Index = steps.Count + 1, LineNumber = i + 1, Raw = line });
        }
        return steps;
    }

    private static int PopOptionInt(List<string> args, string name, int defaultValue)
    {
        var idx = args.FindIndex(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            return defaultValue;
        }
        if (idx + 1 >= args.Count || !int.TryParse(args[idx + 1], out var value))
        {
            return defaultValue;
        }
        args.RemoveAt(idx + 1);
        args.RemoveAt(idx);
        return value;
    }

    private static string DecodeBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }
        var bytes = Convert.FromBase64String(value);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static string[] Tokenize(string line)
    {
        var result = new List<string>();
        var cur = "";
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (cur.Length > 0)
                {
                    result.Add(cur);
                    cur = "";
                }
                continue;
            }
            cur += ch;
        }
        if (cur.Length > 0)
        {
            result.Add(cur);
        }
        return result.ToArray();
    }

    private static bool IsHelp(string s) => s is "-h" or "--help" or "/?";

    private static void PrintHelp()
    {
        Console.WriteLine("EventStudioReplayClient usage:");
        Console.WriteLine("  ping");
        Console.WriteLine("  list-controls");
        Console.WriteLine("  click <target> [--delay ms]");
        Console.WriteLine("  set-text <target> <value> [--delay ms]");
        Console.WriteLine("  set-text-b64 <target> <base64> [--delay ms]");
        Console.WriteLine("  focus <target>");
        Console.WriteLine("  close-dialog");
        Console.WriteLine("  script <path>");
        Console.WriteLine("  script-list <path>");
        Console.WriteLine("  script-step <path> <n>");
        Console.WriteLine("  delay <ms>");
    }
}
