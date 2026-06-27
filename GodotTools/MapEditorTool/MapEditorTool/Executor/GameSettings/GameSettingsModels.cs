namespace MapEditorTool.Executor.GameSettings
{
    public sealed class GameStartingMapResult
    {
        public GameStartingMapResult()
        {
            GameSceneFilePath = string.Empty;
            RawStartingMap = string.Empty;
            NormalizedStartingMap = string.Empty;
            Summary = string.Empty;
        }

        public string GameSceneFilePath { get; set; }
        public string RawStartingMap { get; set; }
        public string NormalizedStartingMap { get; set; }
        public bool Patched { get; set; }
        public string Summary { get; set; }
    }
}
