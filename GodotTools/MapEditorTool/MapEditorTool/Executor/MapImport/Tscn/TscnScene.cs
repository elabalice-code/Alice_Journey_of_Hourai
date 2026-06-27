using System;
using System.Collections.Generic;

namespace MapEditorTool.Executor.MapImport.Tscn
{
    public sealed class TscnScene
    {
        public TscnScene()
        {
            SceneUid = string.Empty;
            ExtResources = new List<TscnExtResource>();
            Nodes = new List<TscnNode>();
        }

        public string SceneUid { get; set; }
        public List<TscnExtResource> ExtResources { get; private set; }
        public List<TscnNode> Nodes { get; private set; }

        public string FindExtResourcePathById(string id)
        {
            foreach (var resource in ExtResources)
            {
                if (string.Equals(resource.Id, id, StringComparison.Ordinal))
                    return resource.Path;
            }

            return null;
        }
    }

    public sealed class TscnExtResource
    {
        public TscnExtResource()
        {
            Type = string.Empty;
            Path = string.Empty;
            Id = string.Empty;
        }

        public string Type { get; set; }
        public string Path { get; set; }
        public string Id { get; set; }
    }

    public sealed class TscnNode
    {
        public TscnNode()
        {
            Name = string.Empty;
            Type = string.Empty;
            Parent = string.Empty;
            RawProps = new Dictionary<string, string>();
        }

        public string Name { get; set; }
        public string Type { get; set; }
        public string Parent { get; set; }
        public string InstanceExtResourceId { get; set; }
        public Dictionary<string, string> RawProps { get; private set; }
    }
}
