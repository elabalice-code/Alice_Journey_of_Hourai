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
                Id = "grp_main",
                Title = "Main Quest",
                NodeKind = EventNodeKind.TaskGroup
            });
            project.Events.Add(new EventNode
            {
                Id = "evt_start",
                Title = "Start",
                ParentGroupId = "grp_main",
                InteractionObjectId = "npc_alice",
                StateKey = "quest.main.start",
                StateValueOnActivate = "active",
                CompletionItemId = "hourai_token",
                CompletionItemCount = 1,
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
            if (graph.Nodes.Count != 2 || graph.StartEventId != "evt_start" || graph.Nodes.Any(x => x.Id == "grp_main"))
            {
                Console.Error.WriteLine("EventStudio agent self-test failed runtime graph check.");
                return 1;
            }
            var runtimeStart = graph.Nodes.FirstOrDefault(x => x.Id == "evt_start");
            if (runtimeStart == null ||
                runtimeStart.InteractionObjectId != "npc_alice" ||
                runtimeStart.StateKey != "quest.main.start" ||
                runtimeStart.StateValueOnActivate != "active" ||
                runtimeStart.CompletionItemId != "hourai_token" ||
                runtimeStart.CompletionItemCount != 1)
            {
                Console.Error.WriteLine("EventStudio agent self-test failed runtime state export check.");
                return 1;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), "EventStudio.AgentSelfTest.events.json");
            EventProjectStore.Save(tempPath, project);
            var loaded = EventProjectStore.Load(tempPath);
            var loadedStart = loaded.Find("evt_start");
            if (loaded.Events.Count != 3 || loadedStart == null ||
                loadedStart.InteractionObjectId != "npc_alice" ||
                loadedStart.StateKey != "quest.main.start" ||
                loadedStart.CompletionItemId != "hourai_token")
            {
                Console.Error.WriteLine("EventStudio agent self-test failed store roundtrip.");
                return 1;
            }

            project.StartEventId = "grp_main";
            if (!EventProjectStore.ValidateProject(project).Any(x => x.Severity == ValidationSeverity.Error && x.Code == "START_IS_GROUP"))
            {
                Console.Error.WriteLine("EventStudio agent self-test failed group start validation.");
                return 1;
            }

            project.StartEventId = "evt_start";
            project.Find("evt_start")!.Actions[0].TargetEventId = "grp_main";
            if (!EventProjectStore.ValidateProject(project).Any(x => x.Severity == ValidationSeverity.Error && x.Code == "ACTION_TARGET_GROUP"))
            {
                Console.Error.WriteLine("EventStudio agent self-test failed group target validation.");
                return 1;
            }

            project.Find("evt_start")!.Actions[0].TargetEventId = "evt_next";
            var folderA = new EventNode { Id = "grp_a", Title = "Folder A", NodeKind = EventNodeKind.TaskGroup, ParentGroupId = "grp_b" };
            var folderB = new EventNode { Id = "grp_b", Title = "Folder B", NodeKind = EventNodeKind.TaskGroup, ParentGroupId = "grp_a" };
            project.Events.Add(folderA);
            project.Events.Add(folderB);
            if (!EventProjectStore.ValidateProject(project).Any(x => x.Severity == ValidationSeverity.Error && x.Code == "GROUP_PARENT_CYCLE"))
            {
                Console.Error.WriteLine("EventStudio agent self-test failed group parent cycle validation.");
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
