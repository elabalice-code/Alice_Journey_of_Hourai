namespace MapEditorTool.ViewModel
{
    public sealed class LinkShellState
    {
        public LinkShellState()
        {
            FromMap = "Forest Entrance";
            FromPortal = "Portal_A";
            ToMap = "Corridor";
            ToPortal = "Portal_B";
            Notes = "Consumer snapshot placeholder. Link UI refreshes from this object.";
        }

        public string FromMap { get; set; }
        public string FromPortal { get; set; }
        public string ToMap { get; set; }
        public string ToPortal { get; set; }
        public string Notes { get; set; }
    }
}
