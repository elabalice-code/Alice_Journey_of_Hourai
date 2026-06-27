namespace MapEditorTool.ViewModel
{
    public sealed class MapShellState
    {
        public MapShellState()
        {
            SelectedMap = "No map loaded";
            ScenePath = string.Empty;
            RoomWidth = 0;
            RoomHeight = 0;
            TileLayerCount = 0;
            PortalCount = 0;
            EntityCount = 0;
            Notes = "Import from Godot to populate this consumer snapshot.";
        }

        public string SelectedMap { get; set; }
        public string ScenePath { get; set; }
        public int RoomWidth { get; set; }
        public int RoomHeight { get; set; }
        public int TileLayerCount { get; set; }
        public int PortalCount { get; set; }
        public int EntityCount { get; set; }
        public string Notes { get; set; }
    }
}
