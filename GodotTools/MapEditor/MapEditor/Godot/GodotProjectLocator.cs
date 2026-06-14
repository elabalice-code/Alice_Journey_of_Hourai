namespace MapEditor.Godot;

public static class GodotProjectLocator
{
    public static string FindGodotRoot(string startDir)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir));
        while (dir != null)
        {
            var godotMarker = Path.Combine(dir.FullName, ".godot");
            if (Directory.Exists(godotMarker))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"Could not find Godot root (folder containing .godot) starting from: {startDir}");
    }
}
