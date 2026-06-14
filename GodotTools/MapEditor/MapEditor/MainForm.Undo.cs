using MapEditor.Models;

namespace MapEditor;

public sealed partial class MainForm
{
    private interface IUndoableAction
    {
        string Name { get; }
        void Undo();
        void Redo();
    }

    private sealed class UndoManager
    {
        private readonly Stack<IUndoableAction> _undo = new();
        private readonly Stack<IUndoableAction> _redo = new();

        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        public string PeekUndoName() => _undo.TryPeek(out var a) ? a.Name : "";
        public string PeekRedoName() => _redo.TryPeek(out var a) ? a.Name : "";

        public void Push(IUndoableAction action)
        {
            _undo.Push(action);
            _redo.Clear();
        }

        public bool TryUndo()
        {
            if (!CanUndo)
                return false;
            var a = _undo.Pop();
            a.Undo();
            _redo.Push(a);
            return true;
        }

        public bool TryRedo()
        {
            if (!CanRedo)
                return false;
            var a = _redo.Pop();
            a.Redo();
            _undo.Push(a);
            return true;
        }
    }

    private sealed class NodePositionUndoAction : IUndoableAction
    {
        public string Name { get; }
        private readonly MapDefinition _map;
        private readonly string _sceneAbsPath;
        private readonly string _nodePath;
        private readonly float _fromX;
        private readonly float _fromY;
        private readonly float _toX;
        private readonly float _toY;

        public NodePositionUndoAction(string name, MapDefinition map, string sceneAbsPath, string nodePath, float fromX, float fromY, float toX, float toY)
        {
            Name = name;
            _map = map;
            _sceneAbsPath = sceneAbsPath;
            _nodePath = nodePath;
            _fromX = fromX;
            _fromY = fromY;
            _toX = toX;
            _toY = toY;
        }

        public void Undo()
        {
            ApplyToModel(_fromX, _fromY);
            PatchNodePosition(_sceneAbsPath, _nodePath, _fromX, _fromY);
        }

        public void Redo()
        {
            ApplyToModel(_toX, _toY);
            PatchNodePosition(_sceneAbsPath, _nodePath, _toX, _toY);
        }

        private void ApplyToModel(float x, float y)
        {
            var portal = _map.Portals.FirstOrDefault(p => string.Equals(p.NodePath, _nodePath, StringComparison.Ordinal));
            if (portal != null)
            {
                portal.X = x;
                portal.Y = y;
                return;
            }
            var ent = _map.Entities.FirstOrDefault(e => string.Equals(e.NodePath, _nodePath, StringComparison.Ordinal));
            if (ent != null)
            {
                ent.X = x;
                ent.Y = y;
            }
        }
    }

    private sealed class CollisionLayoutUndoAction : IUndoableAction
    {
        public string Name { get; }
        private readonly CollisionLayoutData _before;
        private readonly CollisionLayoutData _after;
        private readonly Action<CollisionLayoutData> _apply;

        public CollisionLayoutUndoAction(string name, CollisionLayoutData before, CollisionLayoutData after, Action<CollisionLayoutData> apply)
        {
            Name = name;
            _before = CloneCollisionLayoutData(before);
            _after = CloneCollisionLayoutData(after);
            _apply = apply;
        }

        public void Undo()
        {
            _apply(CloneCollisionLayoutData(_before));
        }

        public void Redo()
        {
            _apply(CloneCollisionLayoutData(_after));
        }
    }

    private sealed class CollisionUndoAction : IUndoableAction
    {
        public string Name { get; }
        private readonly MapDefinition _map;
        private readonly Dictionary<string, string> _beforeFiles;
        private readonly Dictionary<string, string> _afterFiles;
        private readonly Dictionary<(string layerNodePath, int x, int y), int> _beforeAlt;
        private readonly Dictionary<(string layerNodePath, int x, int y), int> _afterAlt;
        private readonly Action _afterApply;

        public CollisionUndoAction(
            string name,
            MapDefinition map,
            Dictionary<string, string> beforeFiles,
            Dictionary<string, string> afterFiles,
            Dictionary<(string layerNodePath, int x, int y), int> beforeAlt,
            Dictionary<(string layerNodePath, int x, int y), int> afterAlt,
            Action afterApply)
        {
            Name = name;
            _map = map;
            _beforeFiles = beforeFiles;
            _afterFiles = afterFiles;
            _beforeAlt = beforeAlt;
            _afterAlt = afterAlt;
            _afterApply = afterApply;
        }

        public void Undo()
        {
            RestoreFiles(_beforeFiles);
            RestoreAlternatives(_beforeAlt);
            _afterApply();
        }

        public void Redo()
        {
            RestoreFiles(_afterFiles);
            RestoreAlternatives(_afterAlt);
            _afterApply();
        }

        private static void RestoreFiles(Dictionary<string, string> files)
        {
            foreach (var kv in files)
                File.WriteAllText(kv.Key, kv.Value);
        }

        private void RestoreAlternatives(Dictionary<(string layerNodePath, int x, int y), int> alts)
        {
            foreach (var kv in alts)
            {
                var layer = _map.TileLayers.FirstOrDefault(l => string.Equals(l.NodePath, kv.Key.layerNodePath, StringComparison.Ordinal));
                var cell = layer?.Cells.FirstOrDefault(c => c.X == kv.Key.x && c.Y == kv.Key.y);
                if (cell != null)
                    cell.Alternative = kv.Value;
            }
        }
    }
}
