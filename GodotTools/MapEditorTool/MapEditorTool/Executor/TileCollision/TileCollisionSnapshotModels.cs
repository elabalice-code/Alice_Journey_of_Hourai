using System.Collections.Generic;

namespace MapEditorTool.Executor.TileCollision
{
    public sealed class TileCollisionFileSnapshot
    {
        public TileCollisionFileSnapshot()
        {
            Files = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, string> Files { get; private set; }
    }
}
