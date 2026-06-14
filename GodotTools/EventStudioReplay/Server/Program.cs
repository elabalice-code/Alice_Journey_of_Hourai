namespace EventStudioReplayServer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(x => x.Equals("--agent-self-test", StringComparison.OrdinalIgnoreCase) ||
                          x.Equals("agent-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunAgentSelfTest();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static int RunAgentSelfTest()
    {
        try
        {
            var request = new EventStudioReplay.Shared.ReplayRequest
            {
                Command = "ping",
                Target = "main",
                Value = "AgentSelfTest",
                DurationMs = 1
            };
            var json = EventStudioReplay.Shared.ReplayProtocol.Serialize(request);
            var parsed = EventStudioReplay.Shared.ReplayProtocol.Deserialize<EventStudioReplay.Shared.ReplayRequest>(json);
            if (parsed.Command != "ping" || parsed.Target != "main" || parsed.Value != "AgentSelfTest")
            {
                Console.Error.WriteLine("EventStudioReplayServer agent self-test failed replay protocol roundtrip.");
                return 1;
            }

            Console.WriteLine("EventStudioReplayServer agent self-test OK.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("EventStudioReplayServer agent self-test failed: " + ex.Message);
            return 1;
        }
    }
}
