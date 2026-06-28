namespace MapEditorTool.SignalWeaver.Featuror.Persistence.Action
{
    public enum PersistenceActionKind
    {
        None = 0,
        NewProject = 1,
        OpenProject = 2,
        SaveProject = 3,
        SaveProjectAs = 4,
        ImportFromGodot = 5,
        ApplySelectedMapToGodot = 6,
        Exit = 7
    }
}
