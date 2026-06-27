using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapImport
{
    public sealed class MapImportExecutor
    {
        public MapProject ImportFromGodotRoot(string godotRootDir)
        {
            return GodotMapImporter.ImportFromGodot(godotRootDir);
        }
    }
}
