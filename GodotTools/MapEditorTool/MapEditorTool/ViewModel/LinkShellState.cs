namespace MapEditorTool.ViewModel
{
    public sealed class LinkShellState
    {
        public LinkShellState()
        {
            FromMap = string.Empty;
            FromPortal = string.Empty;
            ToMap = string.Empty;
            ToPortal = string.Empty;
            Notes = "Import from Godot to populate this consumer snapshot.";
        }

        public string FromMap { get; set; }
        public string FromPortal { get; set; }
        public string ToMap { get; set; }
        public string ToPortal { get; set; }
        public string Notes { get; set; }
    }
}
