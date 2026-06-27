using System.IO;

namespace MapEditorTool.Executor.MapImport
{
    public static class GodotProjectLocator
    {
        public static string FindGodotRoot(string startDir)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startDir));

            while (directory != null)
            {
                var godotMarker = Path.Combine(directory.FullName, ".godot");
                var projectMarker = Path.Combine(directory.FullName, "project.godot");
                if (Directory.Exists(godotMarker) || File.Exists(projectMarker))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Godot root containing project.godot or .godot starting from: " + startDir);
        }
    }
}
