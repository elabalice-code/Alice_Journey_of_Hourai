namespace MapEditor.Godot.Tscn;

public sealed class TscnScene
{
    public string SceneUid { get; set; } = "";
    public List<TscnExtResource> ExtResources { get; } = [];
    public List<TscnNode> Nodes { get; } = [];

    public string? FindExtResourcePathById(string id)
    {
        foreach (var r in ExtResources)
        {
            if (string.Equals(r.Id, id, StringComparison.Ordinal))
                return r.Path;
        }
        return null;
    }
}

public sealed class TscnExtResource
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string Id { get; set; } = "";
}

public sealed class TscnNode
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Parent { get; set; } = "";
    public string? InstanceExtResourceId { get; set; }
    public Dictionary<string, string> RawProps { get; } = [];
}
