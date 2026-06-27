namespace MapEditorTool.ViewModel
{
    public sealed class MapShellState
    {
        public MapShellState()
        {
            SelectedMap = "Forest Entrance";
            ScenePath = "res://CoreEngine/Maps/ForestEntrance.tscn";
            RoomWidth = 24;
            RoomHeight = 14;
            Notes = "Consumer snapshot placeholder. UI reads this data; UI events do not mutate controls directly.";
        }

        public string SelectedMap { get; set; }
        public string ScenePath { get; set; }
        public int RoomWidth { get; set; }
        public int RoomHeight { get; set; }
        public string Notes { get; set; }
    }
}
