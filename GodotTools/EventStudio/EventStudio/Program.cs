using EventStudio.IO;
using EventStudio.Models;

namespace EventStudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (IsAgentSelfTest(args))
        {
            return RunAgentSelfTest();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static bool IsAgentSelfTest(string[] args) =>
        args.Any(x => x.Equals("--agent-self-test", StringComparison.OrdinalIgnoreCase) ||
                      x.Equals("agent-self-test", StringComparison.OrdinalIgnoreCase));

    private static int RunAgentSelfTest()
    {
        try
        {
            var project = new EventProject
            {
                Name = "AgentSelfTest",
                StartEventId = "evt_start"
            };
            project.Events.Add(new EventNode
            {
                Id = "evt_start",
                Title = "Start",
                Actions =
                [
                    new DispatchAction
                    {
                        Type = DispatchActionType.StartEvent,
                        TargetEventId = "evt_next"
                    }
                ]
            });
            project.Events.Add(new EventNode
            {
                Id = "evt_next",
                Title = "Next"
            });

            var issues = EventProjectStore.ValidateProject(project)
                .Where(x => x.Severity == ValidationSeverity.Error)
                .ToArray();
            if (issues.Length > 0)
            {
                Console.Error.WriteLine("EventStudio agent self-test failed validation: " + issues[0].Code);
                return 1;
            }

            var graph = EventProjectStore.BuildRuntimeGraph(project);
            if (graph.Nodes.Count != 2 || graph.StartEventId != "evt_start")
            {
                Console.Error.WriteLine("EventStudio agent self-test failed runtime graph check.");
                return 1;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), "EventStudio.AgentSelfTest.events.json");
            EventProjectStore.Save(tempPath, project);
            var loaded = EventProjectStore.Load(tempPath);
            if (loaded.Events.Count != 2 || loaded.Find("evt_start") == null)
            {
                Console.Error.WriteLine("EventStudio agent self-test failed store roundtrip.");
                return 1;
            }

            Console.WriteLine("EventStudio agent self-test OK.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("EventStudio agent self-test failed: " + ex.Message);
            return 1;
        }
    }
}
