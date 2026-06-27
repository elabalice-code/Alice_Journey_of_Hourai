using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MapEditorTool.Executor.CollisionLayout;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Executor.TileCollision;
using MapEditorTool.Models;

namespace MapEditorTool.UI
{
    internal sealed class MapPreviewCanvas : Control
    {
        private const int TileSize = 32;
        private const float TransformHandleSize = 9f;
        private const float RotateHandleOffset = 26f;
        private const float RotateHandleRadius = 8f;
        private readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GodotTileSet> _tileSetCache = new Dictionary<string, GodotTileSet>(StringComparer.OrdinalIgnoreCase);
        private MapDefinition _map;
        private string _godotRoot;
        private CollisionLayoutData _collisionLayout;
        private CollisionLayoutTarget _collisionTarget;
        private bool _showCollisionOverlay;
        private CollisionEditorMode _collisionEditorMode;
        private CollisionEditorTool _collisionEditorTool;
        private bool _collisionPainting;
        private bool _collisionPaintValue;
        private int _lastPaintedCollisionIndex;
        private int _selectedCollisionPolygonIndex;
        private bool _collisionPolygonVertexDragging;
        private int _collisionPolygonDragVertexIndex;
        private bool _collisionPolygonVertexDragMoved;
        private CollisionLayoutData _collisionPolygonVertexDragBefore;
        private CollisionPolygonTransformDrag _collisionPolygonTransformDrag;
        private readonly List<TileCollisionSelection> _selectedTileCollisions;
        private bool _tileCollisionVertexDragging;
        private int _tileCollisionDragVertexIndex;
        private List<GodotVector2> _tileCollisionDragFromPoints;
        private bool _tileCollisionDragMoved;
        private TileCollisionMarquee _tileCollisionMarquee;
        private TileCollisionGroupTransformDrag _tileCollisionGroupTransformDrag;
        private Portal _dragPortal;
        private float _dragFromX;
        private float _dragFromY;
        private float _dragOffsetX;
        private float _dragOffsetY;
        private bool _dragMoved;

        public event EventHandler<PortalMoveCommittedEventArgs> PortalMoveCommitted;
        public event EventHandler<PortalAddRequestedEventArgs> PortalAddRequested;
        public event EventHandler<PortalContextRequestedEventArgs> PortalContextRequested;
        public event EventHandler<CollisionLayoutEditedEventArgs> CollisionLayoutEdited;
        public event EventHandler<CollisionLayoutPolygonSelectedEventArgs> CollisionLayoutPolygonSelected;
        public event EventHandler<CollisionLayoutPolygonEditedEventArgs> CollisionLayoutPolygonEdited;
        public event EventHandler<TileCollisionSelectedEventArgs> TileCollisionSelected;
        public event EventHandler<TileCollisionEditCommittedEventArgs> TileCollisionEditCommitted;
        public event EventHandler<TileCollisionAddBoxRequestedEventArgs> TileCollisionAddBoxRequested;
        public event EventHandler<TileCollisionRemoveRequestedEventArgs> TileCollisionRemoveRequested;
        public event EventHandler<TileCollisionContextRequestedEventArgs> TileCollisionContextRequested;

        public MapPreviewCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(245, 247, 250);
            ForeColor = Color.FromArgb(30, 35, 42);
            Font = new Font("Segoe UI", 9f);
            TabStop = true;
            _godotRoot = string.Empty;
            _selectedCollisionPolygonIndex = -1;
            _collisionPolygonDragVertexIndex = -1;
            _selectedTileCollisions = new List<TileCollisionSelection>();
        }

        public void SetData(MapDefinition map, string godotRoot)
        {
            _map = map;
            _godotRoot = godotRoot ?? string.Empty;
            Invalidate();
        }

        public void SetCollisionEditorState(CollisionEditorMode mode, CollisionEditorTool tool)
        {
            _collisionEditorMode = mode;
            _collisionEditorTool = tool;
            Invalidate();
        }

        public void SetCollisionOverlay(CollisionLayoutData layout, CollisionLayoutTarget target, bool visible)
        {
            _collisionLayout = layout;
            _collisionTarget = target;
            _showCollisionOverlay = visible;
            if (!IsValidCollisionPolygonIndex(_selectedCollisionPolygonIndex))
                _selectedCollisionPolygonIndex = -1;
            if (_collisionEditorMode != CollisionEditorMode.TileSetCollision)
                ClearTileCollisionSelectionState();
            Invalidate();
        }

        public void EvictTileSetCacheForResPath(string resPath)
        {
            var absolute = ToAbsoluteGodotPath(resPath);
            if (absolute.Length > 0)
                _tileSetCache.Remove(absolute);
        }

        public void ClearTileCollisionSelection()
        {
            ClearTileCollisionSelectionState();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var image in _imageCache.Values)
                    image.Dispose();
                _imageCache.Clear();
                _tileSetCache.Clear();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.Clear(BackColor);

            if (_map == null)
            {
                DrawCenteredText(graphics, "Import or open a project, then select a map.");
                return;
            }

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);

            using (var roomBrush = new SolidBrush(Color.White))
            using (var borderPen = new Pen(Color.FromArgb(80, 95, 110)))
            {
                graphics.FillRectangle(roomBrush, bounds);
                graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }

            DrawTextures(graphics, bounds, roomPixelWidth, roomPixelHeight);
            DrawTileLayers(graphics, bounds);
            DrawCollisionOverlay(graphics, bounds);
            DrawGrid(graphics, bounds, _map.RoomWidth, _map.RoomHeight);
            DrawPortals(graphics, bounds);
            DrawSceneInfo(graphics, bounds);
            DrawEditorInfo(graphics, bounds);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_map == null)
                return;

            if (CanSelectTileCollision() && e.Button == MouseButtons.Right)
            {
                if (RequestTileCollisionContext(e.Location))
                    return;
            }

            if (CanSelectTileCollision() && e.Button == MouseButtons.Left)
            {
                if (TryHandleTileCollisionAddRemove(e.Location))
                    return;

                if (BeginTileCollisionGroupTransform(e.Location))
                    return;

                if (BeginTileCollisionVertexDrag(e.Location))
                    return;

                if (SelectTileCollisionAt(e.Location))
                    return;

                if (_collisionEditorTool == CollisionEditorTool.Select)
                {
                    _tileCollisionMarquee = new TileCollisionMarquee(e.Location);
                    Capture = true;
                    Cursor = Cursors.Cross;
                    Invalidate();
                    return;
                }
            }

            if (CanEditCollisionPolygons() && e.Button == MouseButtons.Left)
            {
                if (BeginCollisionPolygonTransform(e.Location))
                    return;

                if (BeginCollisionPolygonInteraction(e.Location))
                    return;
            }

            if (CanPaintCollisionLayout() && (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right))
            {
                _collisionPainting = true;
                _collisionPaintValue = _collisionEditorTool == CollisionEditorTool.AddBox && e.Button == MouseButtons.Left;
                if (_collisionEditorTool == CollisionEditorTool.Remove || e.Button == MouseButtons.Right)
                    _collisionPaintValue = false;
                _lastPaintedCollisionIndex = -1;
                ApplyCollisionPaintAt(e.Location);
                Capture = true;
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                var hitPortal = HitTestPortal(e.Location);
                if (hitPortal != null)
                {
                    var contextHandler = PortalContextRequested;
                    if (contextHandler != null)
                        contextHandler(this, new PortalContextRequestedEventArgs(hitPortal));
                    return;
                }

                var screenWorld = ScreenToWorld(e.Location);
                var addPosition = ClampToRoom(screenWorld.X, screenWorld.Y);
                var args = new PortalAddRequestedEventArgs(addPosition.X, addPosition.Y);
                var addHandler = PortalAddRequested;
                if (addHandler != null)
                    addHandler(this, args);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            var hit = HitTestPortal(e.Location);
            if (hit == null)
                return;

            var world = ScreenToWorld(e.Location);
            _dragPortal = hit;
            _dragFromX = hit.X;
            _dragFromY = hit.Y;
            _dragOffsetX = hit.X - world.X;
            _dragOffsetY = hit.Y - world.Y;
            _dragMoved = false;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_map == null)
                return;

            if (_tileCollisionVertexDragging && e.Button == MouseButtons.Left)
            {
                ApplyTileCollisionVertexDrag(e.Location);
                return;
            }

            if (_tileCollisionGroupTransformDrag != null && e.Button == MouseButtons.Left)
            {
                ApplyTileCollisionGroupTransformDrag(e.Location);
                return;
            }

            if (_tileCollisionMarquee != null && e.Button == MouseButtons.Left)
            {
                _tileCollisionMarquee.End = e.Location;
                Invalidate();
                return;
            }

            if (_collisionPolygonTransformDrag != null && e.Button == MouseButtons.Left)
            {
                ApplyCollisionPolygonTransformDrag(e.Location);
                return;
            }

            if (_collisionPolygonVertexDragging && e.Button == MouseButtons.Left)
            {
                ApplyCollisionPolygonVertexDrag(e.Location);
                return;
            }

            if (_collisionPainting && (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right))
            {
                ApplyCollisionPaintAt(e.Location);
                return;
            }

            if (_dragPortal != null && e.Button == MouseButtons.Left)
            {
                var world = ScreenToWorld(e.Location);
                var clamped = ClampToRoom(world.X + _dragOffsetX, world.Y + _dragOffsetY);
                if (Math.Abs(_dragPortal.X - clamped.X) > 0.01f || Math.Abs(_dragPortal.Y - clamped.Y) > 0.01f)
                {
                    _dragPortal.X = clamped.X;
                    _dragPortal.Y = clamped.Y;
                    _dragMoved = true;
                    Invalidate();
                }

                return;
            }

            if (e.Button == MouseButtons.None)
                Cursor = HitTestPortal(e.Location) == null ? Cursors.Default : Cursors.Hand;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_dragPortal == null)
                Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_tileCollisionVertexDragging)
            {
                var tileMoved = _tileCollisionDragMoved;
                var selection = GetPrimaryTileCollisionSelection();
                var fromPoints = _tileCollisionDragFromPoints;
                _tileCollisionVertexDragging = false;
                _tileCollisionDragVertexIndex = -1;
                _tileCollisionDragFromPoints = null;
                _tileCollisionDragMoved = false;
                Capture = false;
                Cursor = Cursors.Default;
                if (tileMoved && selection != null)
                    RaiseTileCollisionEditCommitted(selection, fromPoints, CloneGodotVectorPoints(selection.Points));
                return;
            }

            if (_tileCollisionGroupTransformDrag != null)
            {
                var drag = _tileCollisionGroupTransformDrag;
                _tileCollisionGroupTransformDrag = null;
                Capture = false;
                Cursor = Cursors.Default;
                if (drag.Moved)
                    RaiseTileCollisionGroupEditCommitted(drag);
                return;
            }

            if (_tileCollisionMarquee != null)
            {
                var marquee = _tileCollisionMarquee;
                _tileCollisionMarquee = null;
                Capture = false;
                Cursor = Cursors.Default;
                ApplyTileCollisionMarqueeSelection(marquee);
                return;
            }

            if (_collisionPolygonTransformDrag != null)
            {
                var edited = _collisionPolygonTransformDrag.Moved;
                var editName = _collisionPolygonTransformDrag.EditName;
                var polygonIndex = _collisionPolygonTransformDrag.PolygonIndex;
                var beforeLayout = _collisionPolygonTransformDrag.BeforeLayout;
                _collisionPolygonTransformDrag = null;
                Capture = false;
                Cursor = Cursors.Default;
                if (edited)
                    RaiseCollisionPolygonEdited(editName, polygonIndex, beforeLayout);
                return;
            }

            if (_collisionPolygonVertexDragging)
            {
                var vertexMoved = _collisionPolygonVertexDragMoved;
                var polygonIndex = _selectedCollisionPolygonIndex;
                _collisionPolygonVertexDragging = false;
                _collisionPolygonDragVertexIndex = -1;
                _collisionPolygonVertexDragMoved = false;
                Capture = false;
                Cursor = Cursors.Default;
                if (vertexMoved)
                    RaiseCollisionPolygonEdited("Vertex moved", polygonIndex, _collisionPolygonVertexDragBefore);
                _collisionPolygonVertexDragBefore = null;
                return;
            }

            if (_collisionPainting)
            {
                _collisionPainting = false;
                _lastPaintedCollisionIndex = -1;
                Capture = false;
                return;
            }

            if (_dragPortal == null)
                return;

            var portal = _dragPortal;
            var fromX = _dragFromX;
            var fromY = _dragFromY;
            var toX = portal.X;
            var toY = portal.Y;
            var moved = _dragMoved;

            _dragPortal = null;
            _dragMoved = false;
            Capture = false;
            Cursor = HitTestPortal(e.Location) == null ? Cursors.Default : Cursors.Hand;

            if (!moved)
                return;

            var args = new PortalMoveCommittedEventArgs(portal, fromX, fromY, toX, toY);
            var handler = PortalMoveCommitted;
            if (handler != null)
                handler(this, args);

            if (!args.Accepted)
            {
                portal.X = fromX;
                portal.Y = fromY;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Delete &&
                _collisionEditorMode == CollisionEditorMode.CollisionLayout &&
                IsValidCollisionPolygonIndex(_selectedCollisionPolygonIndex))
            {
                RemoveSelectedCollisionPolygon();
                e.Handled = true;
            }
        }

        private RectangleF ComputeRoomBounds(int roomPixelWidth, int roomPixelHeight)
        {
            var pad = 18f;
            var availableWidth = Math.Max(1f, ClientSize.Width - pad * 2f);
            var availableHeight = Math.Max(1f, ClientSize.Height - pad * 2f);
            var scale = Math.Min(availableWidth / roomPixelWidth, availableHeight / roomPixelHeight);
            scale = Math.Max(0.05f, Math.Min(scale, 2.5f));

            var width = roomPixelWidth * scale;
            var height = roomPixelHeight * scale;
            return new RectangleF(
                (ClientSize.Width - width) / 2f,
                (ClientSize.Height - height) / 2f,
                width,
                height);
        }

        private bool CanPaintCollisionLayout()
        {
            return _showCollisionOverlay &&
                _collisionLayout != null &&
                _collisionLayout.Solid != null &&
                (_collisionLayout.Polygons == null || _collisionLayout.Polygons.Count == 0) &&
                _collisionEditorMode == CollisionEditorMode.CollisionLayout &&
                (_collisionEditorTool == CollisionEditorTool.AddBox || _collisionEditorTool == CollisionEditorTool.Remove);
        }

        private bool CanSelectTileCollision()
        {
            return _showCollisionOverlay &&
                _map != null &&
                _map.TileLayers != null &&
                _collisionEditorMode == CollisionEditorMode.TileSetCollision &&
                (_collisionEditorTool == CollisionEditorTool.Select ||
                 _collisionEditorTool == CollisionEditorTool.Vertex ||
                 _collisionEditorTool == CollisionEditorTool.Move ||
                 _collisionEditorTool == CollisionEditorTool.Rotate ||
                 _collisionEditorTool == CollisionEditorTool.Scale ||
                 _collisionEditorTool == CollisionEditorTool.AddBox ||
                 _collisionEditorTool == CollisionEditorTool.Remove);
        }

        private bool SelectTileCollisionAt(Point location)
        {
            TileCollisionSelection selection;
            if (!HitTestTileCollisionPolygon(location, out selection))
            {
                if (!IsControlPressed())
                    SetSingleTileCollisionSelection(null);
                return false;
            }

            if (IsControlPressed() && _collisionEditorTool == CollisionEditorTool.Select)
                ToggleTileCollisionSelection(selection);
            else
                SetSingleTileCollisionSelection(selection);

            if (_collisionEditorTool == CollisionEditorTool.Move ||
                _collisionEditorTool == CollisionEditorTool.Rotate)
                BeginTileCollisionGroupTransform(location);

            return true;
        }

        private bool RequestTileCollisionContext(Point location)
        {
            TileCollisionSelection selection;
            if (!HitTestTileCollisionPolygon(location, out selection))
                return false;

            if (FindTileCollisionSelection(selection.LayerNodePath, selection.CellX, selection.CellY, selection.Alternative) == null)
                SetSingleTileCollisionSelection(selection);
            var handler = TileCollisionContextRequested;
            if (handler != null)
                handler(this, new TileCollisionContextRequestedEventArgs(GetSelectedTileCollisions(), location));
            return true;
        }

        private bool TryHandleTileCollisionAddRemove(Point location)
        {
            if (_collisionEditorTool != CollisionEditorTool.AddBox && _collisionEditorTool != CollisionEditorTool.Remove)
                return false;

            TileCollisionCellHit hit;
            if (!HitTestTileCell(location, out hit))
            {
                SetSingleTileCollisionSelection(null);
                return true;
            }

            if (_collisionEditorTool == CollisionEditorTool.AddBox)
            {
                var handler = TileCollisionAddBoxRequested;
                if (handler != null)
                    handler(this, new TileCollisionAddBoxRequestedEventArgs(hit, CreateDefaultTileCollisionSquare()));
            }
            else
            {
                var handler = TileCollisionRemoveRequested;
                if (handler != null)
                    handler(this, new TileCollisionRemoveRequestedEventArgs(hit));
            }

            return true;
        }

        private static List<GodotVector2> CreateDefaultTileCollisionSquare()
        {
            var half = TileSize / 2f;
            return new List<GodotVector2>
            {
                new GodotVector2(-half, -half),
                new GodotVector2(half, -half),
                new GodotVector2(half, half),
                new GodotVector2(-half, half)
            };
        }

        private void SetSingleTileCollisionSelection(TileCollisionSelection selection)
        {
            _selectedTileCollisions.Clear();
            if (selection != null)
                _selectedTileCollisions.Add(selection);
            Invalidate();

            var handler = TileCollisionSelected;
            if (handler != null)
                handler(this, new TileCollisionSelectedEventArgs(GetPrimaryTileCollisionSelection(), _selectedTileCollisions.Count));
        }

        private void ToggleTileCollisionSelection(TileCollisionSelection selection)
        {
            if (selection == null)
                return;

            var existing = FindTileCollisionSelection(selection.LayerNodePath, selection.CellX, selection.CellY, selection.Alternative);
            if (existing == null)
                _selectedTileCollisions.Add(selection);
            else
                _selectedTileCollisions.Remove(existing);

            Invalidate();
            var handler = TileCollisionSelected;
            if (handler != null)
                handler(this, new TileCollisionSelectedEventArgs(GetPrimaryTileCollisionSelection(), _selectedTileCollisions.Count));
        }

        private void AddTileCollisionSelection(TileCollisionSelection selection)
        {
            if (selection == null)
                return;
            if (FindTileCollisionSelection(selection.LayerNodePath, selection.CellX, selection.CellY, selection.Alternative) != null)
                return;

            _selectedTileCollisions.Add(selection);
        }

        private void ClearTileCollisionSelectionState()
        {
            _selectedTileCollisions.Clear();
            _tileCollisionMarquee = null;
            _tileCollisionGroupTransformDrag = null;
            _tileCollisionVertexDragging = false;
            _tileCollisionDragVertexIndex = -1;
            _tileCollisionDragFromPoints = null;
            _tileCollisionDragMoved = false;
        }

        private TileCollisionSelection GetPrimaryTileCollisionSelection()
        {
            return _selectedTileCollisions.Count == 0 ? null : _selectedTileCollisions[0];
        }

        private List<TileCollisionSelection> GetSelectedTileCollisions()
        {
            return new List<TileCollisionSelection>(_selectedTileCollisions);
        }

        private TileCollisionSelection FindTileCollisionSelection(string layerNodePath, int cellX, int cellY, int alternative)
        {
            foreach (var selection in _selectedTileCollisions)
            {
                if (selection.CellX == cellX &&
                    selection.CellY == cellY &&
                    selection.Alternative == alternative &&
                    string.Equals(selection.LayerNodePath, layerNodePath, StringComparison.Ordinal))
                    return selection;
            }

            return null;
        }

        private bool BeginTileCollisionVertexDrag(Point location)
        {
            var primary = GetPrimaryTileCollisionSelection();
            if (_collisionEditorTool != CollisionEditorTool.Vertex || primary == null)
                return false;

            int vertexIndex;
            if (!HitTestSelectedTileCollisionVertex(location, out vertexIndex))
                return false;

            _tileCollisionVertexDragging = true;
            _tileCollisionDragVertexIndex = vertexIndex;
            _tileCollisionDragFromPoints = CloneGodotVectorPoints(primary.Points);
            _tileCollisionDragMoved = false;
            Capture = true;
            Cursor = Cursors.SizeAll;
            return true;
        }

        private void ApplyTileCollisionVertexDrag(Point location)
        {
            var primary = GetPrimaryTileCollisionSelection();
            if (primary == null || _tileCollisionDragVertexIndex < 0 || _tileCollisionDragVertexIndex >= primary.Points.Count)
                return;

            var screenWorld = ScreenToWorld(location);
            var world = ClampToRoom(screenWorld.X, screenWorld.Y);
            var local = WorldToTileLocal(primary.CellX, primary.CellY, world);
            var current = primary.Points[_tileCollisionDragVertexIndex];
            if (Math.Abs(current.X - local.X) < 0.01f && Math.Abs(current.Y - local.Y) < 0.01f)
                return;

            primary.Points[_tileCollisionDragVertexIndex] = local;
            _tileCollisionDragMoved = true;
            Invalidate();
        }

        private bool HitTestSelectedTileCollisionVertex(Point location, out int vertexIndex)
        {
            vertexIndex = -1;
            var primary = GetPrimaryTileCollisionSelection();
            if (primary == null || primary.Points == null || primary.Points.Count == 0)
                return false;
            if (_map == null)
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;
            var points = BuildTileCollisionScreenPoints(primary.CellX, primary.CellY, primary.Points, bounds, scale);
            var radius = Math.Max(6f, 7f * scale);
            var radiusSquared = radius * radius;
            for (var i = 0; i < points.Length; i++)
            {
                var dx = location.X - points[i].X;
                var dy = location.Y - points[i].Y;
                if (dx * dx + dy * dy <= radiusSquared)
                {
                    vertexIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void RaiseTileCollisionEditCommitted(TileCollisionSelection selection, List<GodotVector2> fromPoints, List<GodotVector2> toPoints)
        {
            var handler = TileCollisionEditCommitted;
            if (handler != null)
            {
                var edit = new TileCollisionEditItem(selection, fromPoints, toPoints);
                var args = new TileCollisionEditCommittedEventArgs(new List<TileCollisionEditItem> { edit });
                handler(this, args);
                if (!args.Accepted && selection != null)
                {
                    selection.ReplacePoints(CloneGodotVectorPoints(fromPoints));
                    Invalidate();
                }
            }
        }

        private void RaiseTileCollisionGroupEditCommitted(TileCollisionGroupTransformDrag drag)
        {
            var handler = TileCollisionEditCommitted;
            if (handler == null || drag == null || drag.Selections.Count == 0)
                return;

            var edits = new List<TileCollisionEditItem>();
            for (var i = 0; i < drag.Selections.Count; i++)
                edits.Add(new TileCollisionEditItem(drag.Selections[i], drag.StartPoints[i], CloneGodotVectorPoints(drag.Selections[i].Points)));

            var args = new TileCollisionEditCommittedEventArgs(edits);
            handler(this, args);
            if (!args.Accepted)
            {
                for (var i = 0; i < drag.Selections.Count; i++)
                    drag.Selections[i].ReplacePoints(CloneGodotVectorPoints(drag.StartPoints[i]));
                Invalidate();
            }
        }

        private bool BeginTileCollisionGroupTransform(Point location)
        {
            if (_selectedTileCollisions.Count == 0)
                return false;
            if (_collisionEditorTool != CollisionEditorTool.Move &&
                _collisionEditorTool != CollisionEditorTool.Rotate &&
                _collisionEditorTool != CollisionEditorTool.Scale)
                return false;

            TileCollisionGroupTransformHit hit;
            if (!HitTestTileCollisionGroupTransform(location, out hit))
                return false;

            var startMouseWorld = ScreenToWorld(location);
            _tileCollisionGroupTransformDrag = new TileCollisionGroupTransformDrag(
                hit.Kind,
                GetSelectedTileCollisions(),
                CloneTileCollisionSelectionPoints(_selectedTileCollisions),
                GetTileCollisionGroupBounds(_selectedTileCollisions),
                startMouseWorld,
                hit.ScaleHandleKind);
            Capture = true;
            Cursor = hit.Kind == CollisionPolygonTransformKind.Rotate ? Cursors.Cross : Cursors.SizeAll;
            return true;
        }

        private void ApplyTileCollisionGroupTransformDrag(Point location)
        {
            if (_tileCollisionGroupTransformDrag == null)
                return;

            var world = ScreenToWorld(location);
            for (var i = 0; i < _tileCollisionGroupTransformDrag.Selections.Count; i++)
            {
                var selection = _tileCollisionGroupTransformDrag.Selections[i];
                if (selection == null)
                    continue;
                var target = selection.Points;
                var start = _tileCollisionGroupTransformDrag.StartPoints[i];
                if (_tileCollisionGroupTransformDrag.Kind == CollisionPolygonTransformKind.Move)
                    ApplyTileCollisionGroupMove(selection, target, start, _tileCollisionGroupTransformDrag, world);
                else if (_tileCollisionGroupTransformDrag.Kind == CollisionPolygonTransformKind.Rotate)
                    ApplyTileCollisionGroupRotate(selection, target, start, _tileCollisionGroupTransformDrag, world);
                else if (_tileCollisionGroupTransformDrag.Kind == CollisionPolygonTransformKind.Scale)
                    ApplyTileCollisionGroupScale(selection, target, start, _tileCollisionGroupTransformDrag, world);
            }

            _tileCollisionGroupTransformDrag.Moved = true;
            Invalidate();
        }

        private void ApplyTileCollisionGroupMove(
            TileCollisionSelection selection,
            List<GodotVector2> target,
            List<GodotVector2> start,
            TileCollisionGroupTransformDrag drag,
            PointF world)
        {
            var dx = world.X - drag.StartMouseWorld.X;
            var dy = world.Y - drag.StartMouseWorld.Y;
            target.Clear();
            for (var i = 0; i < start.Count; i++)
            {
                var startWorld = TileLocalToWorld(selection.CellX, selection.CellY, start[i]);
                var clamped = ClampToRoom(startWorld.X + dx, startWorld.Y + dy);
                target.Add(WorldToTileLocal(selection.CellX, selection.CellY, clamped));
            }
        }

        private void ApplyTileCollisionGroupRotate(
            TileCollisionSelection selection,
            List<GodotVector2> target,
            List<GodotVector2> start,
            TileCollisionGroupTransformDrag drag,
            PointF world)
        {
            var pivot = drag.StartBounds.Center;
            var startAngle = Math.Atan2(drag.StartMouseWorld.Y - pivot.Y, drag.StartMouseWorld.X - pivot.X);
            var currentAngle = Math.Atan2(world.Y - pivot.Y, world.X - pivot.X);
            var radians = currentAngle - startAngle;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);
            target.Clear();
            for (var i = 0; i < start.Count; i++)
            {
                var startWorld = TileLocalToWorld(selection.CellX, selection.CellY, start[i]);
                var x = startWorld.X - pivot.X;
                var y = startWorld.Y - pivot.Y;
                var clamped = ClampToRoom(pivot.X + x * cos - y * sin, pivot.Y + x * sin + y * cos);
                target.Add(WorldToTileLocal(selection.CellX, selection.CellY, clamped));
            }
        }

        private void ApplyTileCollisionGroupScale(
            TileCollisionSelection selection,
            List<GodotVector2> target,
            List<GodotVector2> start,
            TileCollisionGroupTransformDrag drag,
            PointF world)
        {
            var pivot = drag.StartBounds.GetOppositeHandle(drag.ScaleHandleKind);
            var startHandle = drag.StartBounds.GetHandle(drag.ScaleHandleKind);
            var startDx = startHandle.X - pivot.X;
            var startDy = startHandle.Y - pivot.Y;
            var currentDx = world.X - pivot.X;
            var currentDy = world.Y - pivot.Y;
            var scaleX = Math.Abs(startDx) < 0.001f ? 1f : currentDx / startDx;
            var scaleY = Math.Abs(startDy) < 0.001f ? 1f : currentDy / startDy;
            scaleX = Clamp(scaleX, 0.05f, 20f);
            scaleY = Clamp(scaleY, 0.05f, 20f);
            if (!CollisionScaleHandleAffectsX(drag.ScaleHandleKind))
                scaleX = 1f;
            if (!CollisionScaleHandleAffectsY(drag.ScaleHandleKind))
                scaleY = 1f;

            target.Clear();
            for (var i = 0; i < start.Count; i++)
            {
                var startWorld = TileLocalToWorld(selection.CellX, selection.CellY, start[i]);
                var clamped = ClampToRoom(
                    pivot.X + (startWorld.X - pivot.X) * scaleX,
                    pivot.Y + (startWorld.Y - pivot.Y) * scaleY);
                target.Add(WorldToTileLocal(selection.CellX, selection.CellY, clamped));
            }
        }

        private bool CanEditCollisionPolygons()
        {
            return _showCollisionOverlay &&
                _collisionLayout != null &&
                _collisionLayout.Polygons != null &&
                _collisionLayout.Polygons.Count > 0 &&
                _collisionEditorMode == CollisionEditorMode.CollisionLayout &&
                (_collisionEditorTool == CollisionEditorTool.Select ||
                 _collisionEditorTool == CollisionEditorTool.Vertex ||
                 _collisionEditorTool == CollisionEditorTool.AddBox ||
                 _collisionEditorTool == CollisionEditorTool.Remove);
        }

        private bool BeginCollisionPolygonInteraction(Point location)
        {
            if (_collisionEditorTool == CollisionEditorTool.Vertex)
            {
                int polygonIndex;
                int vertexIndex;
                if (HitTestCollisionPolygonVertex(location, out polygonIndex, out vertexIndex))
                {
                    SelectCollisionPolygon(polygonIndex);
                    _collisionPolygonVertexDragging = true;
                    _collisionPolygonDragVertexIndex = vertexIndex;
                    _collisionPolygonVertexDragMoved = false;
                    _collisionPolygonVertexDragBefore = CloneCollisionLayoutData(_collisionLayout);
                    Capture = true;
                    Cursor = Cursors.SizeAll;
                    return true;
                }
            }

            int hitPolygonIndex;
            if (!HitTestCollisionPolygon(location, out hitPolygonIndex))
            {
                if (_collisionEditorTool == CollisionEditorTool.Select ||
                    _collisionEditorTool == CollisionEditorTool.Vertex ||
                    _collisionEditorTool == CollisionEditorTool.AddBox)
                    SelectCollisionPolygon(-1);
                return false;
            }

            SelectCollisionPolygon(hitPolygonIndex);
            if (_collisionEditorTool == CollisionEditorTool.Remove)
            {
                RemoveSelectedCollisionPolygon();
                return true;
            }

            return _collisionEditorTool == CollisionEditorTool.Select || _collisionEditorTool == CollisionEditorTool.Vertex;
        }

        private bool BeginCollisionPolygonTransform(Point location)
        {
            if (!IsValidCollisionPolygonIndex(_selectedCollisionPolygonIndex))
                return false;

            if (_collisionEditorTool != CollisionEditorTool.Move &&
                _collisionEditorTool != CollisionEditorTool.Rotate &&
                _collisionEditorTool != CollisionEditorTool.Scale)
                return false;

            CollisionPolygonTransformHit hit;
            if (!HitTestCollisionPolygonTransform(location, out hit))
                return false;

            var polygon = _collisionLayout.Polygons[_selectedCollisionPolygonIndex];
            if (polygon == null || polygon.Count < 3)
                return false;

            var startMouseWorld = ScreenToWorld(location);
            _collisionPolygonTransformDrag = new CollisionPolygonTransformDrag(
                hit.Kind,
                _selectedCollisionPolygonIndex,
                CloneCollisionLayoutData(_collisionLayout),
                ClonePolygonPoints(polygon),
                GetCollisionPolygonBounds(polygon),
                startMouseWorld,
                hit.ScaleHandleKind);
            Capture = true;
            Cursor = hit.Kind == CollisionPolygonTransformKind.Rotate ? Cursors.Cross : Cursors.SizeAll;
            return true;
        }

        private void ApplyCollisionPolygonTransformDrag(Point location)
        {
            if (_collisionPolygonTransformDrag == null)
                return;
            if (!IsValidCollisionPolygonIndex(_collisionPolygonTransformDrag.PolygonIndex))
                return;

            var polygon = _collisionLayout.Polygons[_collisionPolygonTransformDrag.PolygonIndex];
            if (polygon == null || polygon.Count != _collisionPolygonTransformDrag.StartPoints.Count)
                return;

            var world = ScreenToWorld(location);
            if (_collisionPolygonTransformDrag.Kind == CollisionPolygonTransformKind.Move)
                ApplyCollisionPolygonMove(polygon, _collisionPolygonTransformDrag, world);
            else if (_collisionPolygonTransformDrag.Kind == CollisionPolygonTransformKind.Rotate)
                ApplyCollisionPolygonRotate(polygon, _collisionPolygonTransformDrag, world);
            else if (_collisionPolygonTransformDrag.Kind == CollisionPolygonTransformKind.Scale)
                ApplyCollisionPolygonScale(polygon, _collisionPolygonTransformDrag, world);

            _collisionPolygonTransformDrag.Moved = true;
            Invalidate();
        }

        private void ApplyCollisionPolygonMove(List<GodotVector2Data> polygon, CollisionPolygonTransformDrag drag, PointF world)
        {
            var dx = world.X - drag.StartMouseWorld.X;
            var dy = world.Y - drag.StartMouseWorld.Y;
            for (var i = 0; i < polygon.Count; i++)
            {
                var start = drag.StartPoints[i];
                var clamped = ClampToRoom(start.X + dx, start.Y + dy);
                polygon[i] = new GodotVector2Data { X = clamped.X, Y = clamped.Y };
            }
        }

        private void ApplyCollisionPolygonRotate(List<GodotVector2Data> polygon, CollisionPolygonTransformDrag drag, PointF world)
        {
            var pivot = drag.StartBounds.Center;
            var startAngle = Math.Atan2(drag.StartMouseWorld.Y - pivot.Y, drag.StartMouseWorld.X - pivot.X);
            var currentAngle = Math.Atan2(world.Y - pivot.Y, world.X - pivot.X);
            var radians = currentAngle - startAngle;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);
            for (var i = 0; i < polygon.Count; i++)
            {
                var start = drag.StartPoints[i];
                var x = start.X - pivot.X;
                var y = start.Y - pivot.Y;
                var clamped = ClampToRoom(pivot.X + x * cos - y * sin, pivot.Y + x * sin + y * cos);
                polygon[i] = new GodotVector2Data { X = clamped.X, Y = clamped.Y };
            }
        }

        private void ApplyCollisionPolygonScale(List<GodotVector2Data> polygon, CollisionPolygonTransformDrag drag, PointF world)
        {
            var pivot = drag.StartBounds.GetOppositeHandle(drag.ScaleHandleKind);
            var startHandle = drag.StartBounds.GetHandle(drag.ScaleHandleKind);
            var startDx = startHandle.X - pivot.X;
            var startDy = startHandle.Y - pivot.Y;
            var currentDx = world.X - pivot.X;
            var currentDy = world.Y - pivot.Y;
            var scaleX = Math.Abs(startDx) < 0.001f ? 1f : currentDx / startDx;
            var scaleY = Math.Abs(startDy) < 0.001f ? 1f : currentDy / startDy;
            scaleX = Clamp(scaleX, 0.05f, 20f);
            scaleY = Clamp(scaleY, 0.05f, 20f);

            if (!CollisionScaleHandleAffectsX(drag.ScaleHandleKind))
                scaleX = 1f;
            if (!CollisionScaleHandleAffectsY(drag.ScaleHandleKind))
                scaleY = 1f;

            for (var i = 0; i < polygon.Count; i++)
            {
                var start = drag.StartPoints[i];
                var clamped = ClampToRoom(pivot.X + (start.X - pivot.X) * scaleX, pivot.Y + (start.Y - pivot.Y) * scaleY);
                polygon[i] = new GodotVector2Data { X = clamped.X, Y = clamped.Y };
            }
        }

        private void ApplyCollisionPolygonVertexDrag(Point location)
        {
            if (!IsValidCollisionPolygonIndex(_selectedCollisionPolygonIndex))
                return;

            var polygon = _collisionLayout.Polygons[_selectedCollisionPolygonIndex];
            if (polygon == null || _collisionPolygonDragVertexIndex < 0 || _collisionPolygonDragVertexIndex >= polygon.Count)
                return;

            var screenWorld = ScreenToWorld(location);
            var world = ClampToRoom(screenWorld.X, screenWorld.Y);
            var current = polygon[_collisionPolygonDragVertexIndex];
            if (current != null &&
                Math.Abs(current.X - world.X) < 0.01f &&
                Math.Abs(current.Y - world.Y) < 0.01f)
                return;

            polygon[_collisionPolygonDragVertexIndex] = new GodotVector2Data { X = world.X, Y = world.Y };
            _collisionPolygonVertexDragMoved = true;
            Invalidate();
        }

        private void RemoveSelectedCollisionPolygon()
        {
            if (!IsValidCollisionPolygonIndex(_selectedCollisionPolygonIndex))
                return;

            var beforeLayout = CloneCollisionLayoutData(_collisionLayout);
            var removedIndex = _selectedCollisionPolygonIndex;
            _collisionLayout.Polygons.RemoveAt(removedIndex);
            _selectedCollisionPolygonIndex = -1;
            Invalidate();
            RaiseCollisionPolygonEdited("Polygon removed", removedIndex, beforeLayout);
            RaiseCollisionPolygonSelected(-1);
        }

        private void SelectCollisionPolygon(int polygonIndex)
        {
            if (polygonIndex >= 0 && !IsValidCollisionPolygonIndex(polygonIndex))
                polygonIndex = -1;
            if (_selectedCollisionPolygonIndex == polygonIndex)
                return;

            _selectedCollisionPolygonIndex = polygonIndex;
            Invalidate();
            RaiseCollisionPolygonSelected(polygonIndex);
        }

        private bool IsValidCollisionPolygonIndex(int polygonIndex)
        {
            return _collisionLayout != null &&
                _collisionLayout.Polygons != null &&
                polygonIndex >= 0 &&
                polygonIndex < _collisionLayout.Polygons.Count;
        }

        private void RaiseCollisionPolygonSelected(int polygonIndex)
        {
            var handler = CollisionLayoutPolygonSelected;
            if (handler != null)
                handler(this, new CollisionLayoutPolygonSelectedEventArgs(_collisionLayout, _collisionTarget, polygonIndex));
        }

        private void RaiseCollisionPolygonEdited(string editName, int polygonIndex, CollisionLayoutData beforeLayout)
        {
            var handler = CollisionLayoutPolygonEdited;
            if (handler != null)
                handler(this, new CollisionLayoutPolygonEditedEventArgs(_collisionLayout, _collisionTarget, polygonIndex, editName, beforeLayout, CloneCollisionLayoutData(_collisionLayout)));
        }

        private void ApplyCollisionPaintAt(Point location)
        {
            if (!CanPaintCollisionLayout())
                return;

            int x;
            int y;
            if (!TryGetCollisionCell(location, out x, out y))
                return;

            var roomWidth = Math.Max(1, _collisionLayout.RoomWidth);
            var index = y * roomWidth + x;
            if (index < 0 || index >= _collisionLayout.Solid.Length || index == _lastPaintedCollisionIndex)
                return;

            _lastPaintedCollisionIndex = index;
            if (_collisionLayout.Solid[index] == _collisionPaintValue)
                return;

            var beforeLayout = CloneCollisionLayoutData(_collisionLayout);
            _collisionLayout.Solid[index] = _collisionPaintValue;
            Invalidate();

            var handler = CollisionLayoutEdited;
            if (handler != null)
                handler(this, new CollisionLayoutEditedEventArgs(_collisionLayout, _collisionTarget, x, y, _collisionPaintValue, beforeLayout, CloneCollisionLayoutData(_collisionLayout)));
        }

        private bool TryGetCollisionCell(Point location, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (_collisionLayout == null || _map == null)
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            if (!bounds.Contains(location))
                return false;

            var roomWidth = Math.Max(1, _collisionLayout.RoomWidth);
            var roomHeight = Math.Max(1, _collisionLayout.RoomHeight);
            x = (int)((location.X - bounds.X) / Math.Max(1f, bounds.Width) * roomWidth);
            y = (int)((location.Y - bounds.Y) / Math.Max(1f, bounds.Height) * roomHeight);
            x = ClampInt(x, 0, roomWidth - 1);
            y = ClampInt(y, 0, roomHeight - 1);
            return true;
        }

        private bool HitTestCollisionPolygonVertex(Point location, out int polygonIndex, out int vertexIndex)
        {
            polygonIndex = -1;
            vertexIndex = -1;
            if (_collisionLayout == null || _collisionLayout.Polygons == null || _map == null)
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;
            var radius = Math.Max(6f, 7f * scale);
            var radiusSquared = radius * radius;

            if (TryHitCollisionPolygonVertexAtIndex(_selectedCollisionPolygonIndex, location, bounds, scale, radiusSquared, out polygonIndex, out vertexIndex))
                return true;

            for (var i = _collisionLayout.Polygons.Count - 1; i >= 0; i--)
            {
                if (i == _selectedCollisionPolygonIndex)
                    continue;
                if (TryHitCollisionPolygonVertexAtIndex(i, location, bounds, scale, radiusSquared, out polygonIndex, out vertexIndex))
                    return true;
            }

            return false;
        }

        private bool TryHitCollisionPolygonVertexAtIndex(
            int candidatePolygonIndex,
            Point location,
            RectangleF bounds,
            float scale,
            float radiusSquared,
            out int polygonIndex,
            out int vertexIndex)
        {
            polygonIndex = -1;
            vertexIndex = -1;
            if (!IsValidCollisionPolygonIndex(candidatePolygonIndex))
                return false;

            var polygon = _collisionLayout.Polygons[candidatePolygonIndex];
            if (polygon == null)
                return false;

            for (var i = 0; i < polygon.Count; i++)
            {
                var point = polygon[i];
                if (point == null)
                    continue;

                var screenX = bounds.X + point.X * scale;
                var screenY = bounds.Y + point.Y * scale;
                var dx = location.X - screenX;
                var dy = location.Y - screenY;
                if (dx * dx + dy * dy <= radiusSquared)
                {
                    polygonIndex = candidatePolygonIndex;
                    vertexIndex = i;
                    return true;
                }
            }

            return false;
        }

        private bool HitTestCollisionPolygon(Point location, out int polygonIndex)
        {
            polygonIndex = -1;
            if (_collisionLayout == null || _collisionLayout.Polygons == null || _map == null)
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;

            for (var i = _collisionLayout.Polygons.Count - 1; i >= 0; i--)
            {
                var polygon = _collisionLayout.Polygons[i];
                if (polygon == null || polygon.Count < 3)
                    continue;

                var points = polygon
                    .Where(point => point != null)
                    .Select(point => new PointF(bounds.X + point.X * scale, bounds.Y + point.Y * scale))
                    .ToArray();
                if (points.Length >= 3 && PointInPolygon(points, location))
                {
                    polygonIndex = i;
                    return true;
                }
            }

            return false;
        }

        private bool HitTestCollisionPolygonTransform(Point location, out CollisionPolygonTransformHit hit)
        {
            hit = new CollisionPolygonTransformHit(CollisionPolygonTransformKind.Move, CollisionScaleHandleKind.BottomRight);
            if (!IsValidCollisionPolygonIndex(_selectedCollisionPolygonIndex))
                return false;

            var polygon = _collisionLayout.Polygons[_selectedCollisionPolygonIndex];
            if (polygon == null || polygon.Count < 3 || _map == null)
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;
            var polygonBounds = GetCollisionPolygonScreenBounds(polygon, bounds, scale);

            if (_collisionEditorTool == CollisionEditorTool.Move)
            {
                var points = polygon
                    .Where(point => point != null)
                    .Select(point => new PointF(bounds.X + point.X * scale, bounds.Y + point.Y * scale))
                    .ToArray();
                if (points.Length >= 3 && PointInPolygon(points, location))
                {
                    hit = new CollisionPolygonTransformHit(CollisionPolygonTransformKind.Move, CollisionScaleHandleKind.BottomRight);
                    return true;
                }

                return false;
            }

            if (_collisionEditorTool == CollisionEditorTool.Scale)
            {
                var handles = GetScaleHandleRects(polygonBounds);
                foreach (var pair in handles)
                {
                    if (!pair.Value.Contains(location))
                        continue;

                    hit = new CollisionPolygonTransformHit(CollisionPolygonTransformKind.Scale, pair.Key);
                    return true;
                }

                return false;
            }

            if (_collisionEditorTool == CollisionEditorTool.Rotate)
            {
                var rotateCenter = GetRotateHandleCenter(polygonBounds, scale);
                var radius = Math.Max(5f, RotateHandleRadius * scale);
                var dx = location.X - rotateCenter.X;
                var dy = location.Y - rotateCenter.Y;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    hit = new CollisionPolygonTransformHit(CollisionPolygonTransformKind.Rotate, CollisionScaleHandleKind.BottomRight);
                    return true;
                }
            }

            return false;
        }

        private bool HitTestTileCell(Point location, out TileCollisionCellHit hit)
        {
            hit = null;
            if (_map == null || _map.TileLayers == null)
                return false;

            var world = ScreenToWorld(location);
            var cellX = (int)Math.Floor(world.X / TileSize);
            var cellY = (int)Math.Floor(world.Y / TileSize);
            if (cellX < 0 || cellY < 0 || cellX >= Math.Max(1, _map.RoomWidth) || cellY >= Math.Max(1, _map.RoomHeight))
                return false;

            foreach (var layer in _map.TileLayers.OrderByDescending(layer => layer.ZIndex).ThenByDescending(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (layer == null || !layer.Visible || layer.Cells == null || layer.Cells.Count == 0)
                    continue;

                var cell = layer.Cells.FirstOrDefault(item => item.X == cellX && item.Y == cellY);
                if (cell == null)
                    continue;

                hit = new TileCollisionCellHit(
                    layer.TileSetPath,
                    layer.NodePath,
                    cell.SourceId,
                    cell.AtlasX,
                    cell.AtlasY,
                    cell.X,
                    cell.Y,
                    cell.Alternative);
                return true;
            }

            return false;
        }

        private bool HitTestTileCollisionPolygon(Point location, out TileCollisionSelection selection)
        {
            selection = null;
            if (_map == null || _map.TileLayers == null || string.IsNullOrWhiteSpace(_godotRoot))
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;

            foreach (var layer in _map.TileLayers.OrderByDescending(layer => layer.ZIndex).ThenByDescending(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (layer == null || !layer.Visible || layer.Cells == null || layer.Cells.Count == 0 || string.IsNullOrWhiteSpace(layer.TileSetPath))
                    continue;

                var tileSet = TryLoadTileSet(layer.TileSetPath);
                if (tileSet == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    GodotTileAtlasSource source;
                    if (!tileSet.Sources.TryGetValue(cell.SourceId, out source))
                        continue;

                    GodotTilePhysicsPolygon polygon;
                    if (!source.PhysicsPolygons.TryGetValue(BuildTilePhysicsPolygonKey(cell.AtlasX, cell.AtlasY, cell.Alternative), out polygon))
                        continue;
                    if (polygon.Points == null || polygon.Points.Count < 3)
                        continue;

                    var points = BuildTileCollisionScreenPoints(cell.X, cell.Y, polygon.Points, bounds, scale);
                    if (!PointInPolygon(points, location))
                        continue;

                    selection = new TileCollisionSelection(
                        layer.TileSetPath,
                        layer.NodePath,
                        cell.SourceId,
                        cell.AtlasX,
                        cell.AtlasY,
                        cell.X,
                        cell.Y,
                        cell.Alternative,
                        polygon.OneWay,
                        CloneGodotVectorPoints(polygon.Points));
                    return true;
                }
            }

            return false;
        }

        private List<TileCollisionSelection> HitTestTileCollisionsInRectangle(Rectangle rectangle)
        {
            var selections = new List<TileCollisionSelection>();
            if (_map == null || _map.TileLayers == null || string.IsNullOrWhiteSpace(_godotRoot))
                return selections;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;

            foreach (var layer in _map.TileLayers.OrderByDescending(layer => layer.ZIndex).ThenByDescending(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (layer == null || !layer.Visible || layer.Cells == null || layer.Cells.Count == 0 || string.IsNullOrWhiteSpace(layer.TileSetPath))
                    continue;

                var tileSet = TryLoadTileSet(layer.TileSetPath);
                if (tileSet == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    GodotTileAtlasSource source;
                    if (!tileSet.Sources.TryGetValue(cell.SourceId, out source))
                        continue;

                    GodotTilePhysicsPolygon polygon;
                    if (!source.PhysicsPolygons.TryGetValue(BuildTilePhysicsPolygonKey(cell.AtlasX, cell.AtlasY, cell.Alternative), out polygon))
                        continue;
                    if (polygon.Points == null || polygon.Points.Count < 3)
                        continue;

                    var points = BuildTileCollisionScreenPoints(cell.X, cell.Y, polygon.Points, bounds, scale);
                    if (!TileCollisionIntersectsRectangle(points, rectangle))
                        continue;

                    selections.Add(new TileCollisionSelection(
                        layer.TileSetPath,
                        layer.NodePath,
                        cell.SourceId,
                        cell.AtlasX,
                        cell.AtlasY,
                        cell.X,
                        cell.Y,
                        cell.Alternative,
                        polygon.OneWay,
                        CloneGodotVectorPoints(polygon.Points)));
                }
            }

            return selections;
        }

        private static bool TileCollisionIntersectsRectangle(PointF[] points, Rectangle rectangle)
        {
            if (points == null || points.Length < 3 || rectangle.Width <= 0 || rectangle.Height <= 0)
                return false;

            foreach (var point in points)
            {
                if (rectangle.Contains(Point.Round(point)))
                    return true;
            }

            var corners = new[]
            {
                new Point(rectangle.Left, rectangle.Top),
                new Point(rectangle.Right, rectangle.Top),
                new Point(rectangle.Right, rectangle.Bottom),
                new Point(rectangle.Left, rectangle.Bottom)
            };
            foreach (var corner in corners)
            {
                if (PointInPolygon(points, corner))
                    return true;
            }

            return GetPointsBounds(points).IntersectsWith(rectangle);
        }

        private static RectangleF GetPointsBounds(PointF[] points)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
                return RectangleF.Empty;
            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        private void ApplyTileCollisionMarqueeSelection(TileCollisionMarquee marquee)
        {
            if (marquee == null)
                return;

            if (!marquee.IsDrag)
                return;

            if (!IsControlPressed())
                _selectedTileCollisions.Clear();

            var hits = HitTestTileCollisionsInRectangle(marquee.Bounds);
            foreach (var hit in hits)
                AddTileCollisionSelection(hit);

            Invalidate();
            var handler = TileCollisionSelected;
            if (handler != null)
                handler(this, new TileCollisionSelectedEventArgs(GetPrimaryTileCollisionSelection(), _selectedTileCollisions.Count));
        }

        private bool HitTestTileCollisionGroupTransform(Point location, out TileCollisionGroupTransformHit hit)
        {
            hit = new TileCollisionGroupTransformHit(CollisionPolygonTransformKind.Move, CollisionScaleHandleKind.BottomRight);
            if (_selectedTileCollisions.Count == 0 || _map == null)
                return false;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var roomBounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = roomBounds.Width / roomPixelWidth;
            var screenBounds = GetTileCollisionGroupScreenBounds(_selectedTileCollisions, roomBounds, scale);

            if (_collisionEditorTool == CollisionEditorTool.Move)
            {
                for (var i = _selectedTileCollisions.Count - 1; i >= 0; i--)
                {
                    var selection = _selectedTileCollisions[i];
                    var points = BuildTileCollisionScreenPoints(selection.CellX, selection.CellY, selection.Points, roomBounds, scale);
                    if (PointInPolygon(points, location))
                    {
                        hit = new TileCollisionGroupTransformHit(CollisionPolygonTransformKind.Move, CollisionScaleHandleKind.BottomRight);
                        return true;
                    }
                }

                return false;
            }

            if (_collisionEditorTool == CollisionEditorTool.Scale)
            {
                foreach (var pair in GetScaleHandleRects(screenBounds))
                {
                    if (!pair.Value.Contains(location))
                        continue;
                    hit = new TileCollisionGroupTransformHit(CollisionPolygonTransformKind.Scale, pair.Key);
                    return true;
                }

                return false;
            }

            if (_collisionEditorTool == CollisionEditorTool.Rotate)
            {
                var rotateCenter = GetRotateHandleCenter(screenBounds, scale);
                var radius = Math.Max(5f, RotateHandleRadius * scale);
                var dx = location.X - rotateCenter.X;
                var dy = location.Y - rotateCenter.Y;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    hit = new TileCollisionGroupTransformHit(CollisionPolygonTransformKind.Rotate, CollisionScaleHandleKind.BottomRight);
                    return true;
                }
            }

            return false;
        }

        private Portal HitTestPortal(Point point)
        {
            if (_map == null || _map.Portals == null)
                return null;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;
            var hitRadius = Math.Max(8f, 9f * scale);

            for (var i = _map.Portals.Count - 1; i >= 0; i--)
            {
                var portal = _map.Portals[i];
                if (portal == null)
                    continue;

                var x = bounds.X + portal.X * scale;
                var y = bounds.Y + portal.Y * scale;
                var dx = point.X - x;
                var dy = point.Y - y;
                if (dx * dx + dy * dy <= hitRadius * hitRadius)
                    return portal;
            }

            return null;
        }

        private PointF ScreenToWorld(Point point)
        {
            if (_map == null)
                return PointF.Empty;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomPixelHeight = Math.Max(1, _map.RoomHeight) * TileSize;
            var bounds = ComputeRoomBounds(roomPixelWidth, roomPixelHeight);
            var scale = bounds.Width / roomPixelWidth;
            if (scale <= 0f)
                return PointF.Empty;

            return new PointF((point.X - bounds.X) / scale, (point.Y - bounds.Y) / scale);
        }

        private PointF ClampToRoom(float x, float y)
        {
            if (_map == null)
                return new PointF(x, y);

            var maxX = Math.Max(1, _map.RoomWidth) * TileSize;
            var maxY = Math.Max(1, _map.RoomHeight) * TileSize;
            return new PointF(Clamp(x, 0f, maxX), Clamp(y, 0f, maxY));
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static bool PointInPolygon(PointF[] polygon, Point point)
        {
            if (polygon == null || polygon.Length < 3)
                return false;

            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                var dy = pj.Y - pi.Y;
                if (Math.Abs(dy) < 0.0001f)
                    dy = 0.0001f;

                var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    point.X < (pj.X - pi.X) * (point.Y - pi.Y) / dy + pi.X;
                if (intersects)
                    inside = !inside;
            }

            return inside;
        }

        private CollisionPolygonBounds GetCollisionPolygonBounds(List<GodotVector2Data> polygon)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;

            foreach (var point in polygon)
            {
                if (point == null)
                    continue;

                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
                return new CollisionPolygonBounds(0f, 0f, 0f, 0f);

            return new CollisionPolygonBounds(minX, minY, maxX, maxY);
        }

        private static List<GodotVector2Data> ClonePolygonPoints(List<GodotVector2Data> polygon)
        {
            var clone = new List<GodotVector2Data>();
            foreach (var point in polygon)
            {
                if (point == null)
                    clone.Add(new GodotVector2Data());
                else
                    clone.Add(new GodotVector2Data { X = point.X, Y = point.Y });
            }

            return clone;
        }

        private static CollisionLayoutData CloneCollisionLayoutData(CollisionLayoutData layout)
        {
            if (layout == null)
                return null;

            var clone = new CollisionLayoutData
            {
                RoomWidth = layout.RoomWidth,
                RoomHeight = layout.RoomHeight,
                Solid = layout.Solid == null ? new bool[0] : (bool[])layout.Solid.Clone(),
                Polygons = new List<List<GodotVector2Data>>()
            };

            if (layout.Polygons != null)
            {
                foreach (var polygon in layout.Polygons)
                    clone.Polygons.Add(polygon == null ? new List<GodotVector2Data>() : ClonePolygonPoints(polygon));
            }

            return clone;
        }

        private static RectangleF GetCollisionPolygonScreenBounds(List<GodotVector2Data> polygon, RectangleF roomBounds, float scale)
        {
            var worldBounds = GetStaticCollisionPolygonBounds(polygon);
            return new RectangleF(
                roomBounds.X + worldBounds.MinX * scale,
                roomBounds.Y + worldBounds.MinY * scale,
                Math.Max(1f, (worldBounds.MaxX - worldBounds.MinX) * scale),
                Math.Max(1f, (worldBounds.MaxY - worldBounds.MinY) * scale));
        }

        private static CollisionPolygonBounds GetStaticCollisionPolygonBounds(List<GodotVector2Data> polygon)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;

            foreach (var point in polygon)
            {
                if (point == null)
                    continue;

                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
                return new CollisionPolygonBounds(0f, 0f, 0f, 0f);

            return new CollisionPolygonBounds(minX, minY, maxX, maxY);
        }

        private static Dictionary<CollisionScaleHandleKind, RectangleF> GetScaleHandleRects(RectangleF bounds)
        {
            var size = TransformHandleSize;
            var half = size / 2f;
            var centerX = bounds.X + bounds.Width / 2f;
            var centerY = bounds.Y + bounds.Height / 2f;

            return new Dictionary<CollisionScaleHandleKind, RectangleF>
            {
                { CollisionScaleHandleKind.TopLeft, new RectangleF(bounds.Left - half, bounds.Top - half, size, size) },
                { CollisionScaleHandleKind.Top, new RectangleF(centerX - half, bounds.Top - half, size, size) },
                { CollisionScaleHandleKind.TopRight, new RectangleF(bounds.Right - half, bounds.Top - half, size, size) },
                { CollisionScaleHandleKind.Right, new RectangleF(bounds.Right - half, centerY - half, size, size) },
                { CollisionScaleHandleKind.BottomRight, new RectangleF(bounds.Right - half, bounds.Bottom - half, size, size) },
                { CollisionScaleHandleKind.Bottom, new RectangleF(centerX - half, bounds.Bottom - half, size, size) },
                { CollisionScaleHandleKind.BottomLeft, new RectangleF(bounds.Left - half, bounds.Bottom - half, size, size) },
                { CollisionScaleHandleKind.Left, new RectangleF(bounds.Left - half, centerY - half, size, size) }
            };
        }

        private static PointF GetRotateHandleCenter(RectangleF bounds, float scale)
        {
            return new PointF(bounds.X + bounds.Width / 2f, bounds.Y - RotateHandleOffset * scale);
        }

        private static bool CollisionScaleHandleAffectsX(CollisionScaleHandleKind kind)
        {
            return kind == CollisionScaleHandleKind.Left ||
                kind == CollisionScaleHandleKind.Right ||
                kind == CollisionScaleHandleKind.TopLeft ||
                kind == CollisionScaleHandleKind.TopRight ||
                kind == CollisionScaleHandleKind.BottomLeft ||
                kind == CollisionScaleHandleKind.BottomRight;
        }

        private static bool CollisionScaleHandleAffectsY(CollisionScaleHandleKind kind)
        {
            return kind == CollisionScaleHandleKind.Top ||
                kind == CollisionScaleHandleKind.Bottom ||
                kind == CollisionScaleHandleKind.TopLeft ||
                kind == CollisionScaleHandleKind.TopRight ||
                kind == CollisionScaleHandleKind.BottomLeft ||
                kind == CollisionScaleHandleKind.BottomRight;
        }

        private static string BuildTilePhysicsPolygonKey(int atlasX, int atlasY, int alternative)
        {
            return atlasX.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ":" +
                atlasY.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "/" +
                alternative.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static PointF[] BuildTileCollisionScreenPoints(int cellX, int cellY, List<GodotVector2> localPoints, RectangleF bounds, float scale)
        {
            var points = new PointF[localPoints.Count];
            var centerX = cellX * TileSize + TileSize / 2f;
            var centerY = cellY * TileSize + TileSize / 2f;
            for (var i = 0; i < localPoints.Count; i++)
            {
                var worldX = centerX + localPoints[i].X;
                var worldY = centerY + localPoints[i].Y;
                points[i] = new PointF(bounds.X + worldX * scale, bounds.Y + worldY * scale);
            }

            return points;
        }

        private static GodotVector2 WorldToTileLocal(int cellX, int cellY, PointF world)
        {
            var centerX = cellX * TileSize + TileSize / 2f;
            var centerY = cellY * TileSize + TileSize / 2f;
            return new GodotVector2(world.X - centerX, world.Y - centerY);
        }

        private static PointF TileLocalToWorld(int cellX, int cellY, GodotVector2 local)
        {
            var centerX = cellX * TileSize + TileSize / 2f;
            var centerY = cellY * TileSize + TileSize / 2f;
            return new PointF(centerX + local.X, centerY + local.Y);
        }

        private static List<List<GodotVector2>> CloneTileCollisionSelectionPoints(List<TileCollisionSelection> selections)
        {
            var clone = new List<List<GodotVector2>>();
            foreach (var selection in selections)
                clone.Add(CloneGodotVectorPoints(selection == null ? null : selection.Points));
            return clone;
        }

        private static CollisionPolygonBounds GetTileCollisionGroupBounds(List<TileCollisionSelection> selections)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var selection in selections)
            {
                if (selection == null || selection.Points == null)
                    continue;
                foreach (var point in selection.Points)
                {
                    var world = TileLocalToWorld(selection.CellX, selection.CellY, point);
                    minX = Math.Min(minX, world.X);
                    minY = Math.Min(minY, world.Y);
                    maxX = Math.Max(maxX, world.X);
                    maxY = Math.Max(maxY, world.Y);
                }
            }

            if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
                return new CollisionPolygonBounds(0f, 0f, 0f, 0f);
            return new CollisionPolygonBounds(minX, minY, maxX, maxY);
        }

        private static RectangleF GetTileCollisionGroupScreenBounds(List<TileCollisionSelection> selections, RectangleF roomBounds, float scale)
        {
            var worldBounds = GetTileCollisionGroupBounds(selections);
            return new RectangleF(
                roomBounds.X + worldBounds.MinX * scale,
                roomBounds.Y + worldBounds.MinY * scale,
                Math.Max(1f, (worldBounds.MaxX - worldBounds.MinX) * scale),
                Math.Max(1f, (worldBounds.MaxY - worldBounds.MinY) * scale));
        }

        private static bool IsControlPressed()
        {
            return (Control.ModifierKeys & Keys.Control) == Keys.Control;
        }

        private static List<GodotVector2> CloneGodotVectorPoints(List<GodotVector2> points)
        {
            var clone = new List<GodotVector2>();
            if (points == null)
                return clone;

            foreach (var point in points)
                clone.Add(new GodotVector2(point.X, point.Y));

            return clone;
        }

        private void DrawTextures(Graphics graphics, RectangleF bounds, int roomPixelWidth, int roomPixelHeight)
        {
            if (_map == null)
                return;

            if (_map.BackgroundTextureEnabled)
            {
                var path = string.IsNullOrWhiteSpace(_map.BackgroundTexturePath)
                    ? _map.TemplateTexturePath
                    : _map.BackgroundTexturePath;
                DrawTexture(graphics, bounds, roomPixelWidth, roomPixelHeight, path, _map.BackgroundTextureAnchor, _map.BackgroundTextureUpscale, 0.85f);
            }

            if (_map.ForegroundTextureEnabled)
                DrawTexture(graphics, bounds, roomPixelWidth, roomPixelHeight, _map.ForegroundTexturePath, _map.ForegroundTextureAnchor, _map.ForegroundTextureUpscale, 0.92f);
        }

        private void DrawTexture(
            Graphics graphics,
            RectangleF bounds,
            int roomPixelWidth,
            int roomPixelHeight,
            string resPath,
            TextureAnchor anchor,
            float upscale,
            float opacity)
        {
            var image = TryLoadImage(resPath);
            if (image == null)
                return;

            upscale = Math.Max(0.0001f, upscale);
            var textureWidth = image.Width * upscale;
            var textureHeight = image.Height * upscale;
            var origin = ComputeAnchoredOrigin(roomPixelWidth, roomPixelHeight, textureWidth, textureHeight, anchor);
            var scale = bounds.Width / roomPixelWidth;
            var destination = new RectangleF(
                bounds.X + origin.X * scale,
                bounds.Y + origin.Y * scale,
                textureWidth * scale,
                textureHeight * scale);

            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var matrix = new System.Drawing.Imaging.ColorMatrix();
                matrix.Matrix33 = Math.Max(0f, Math.Min(1f, opacity));
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(
                    image,
                    Rectangle.Round(destination),
                    0,
                    0,
                    image.Width,
                    image.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
        }

        private void DrawTileLayers(Graphics graphics, RectangleF bounds)
        {
            if (_map == null || _map.TileLayers == null)
                return;

            var scale = bounds.Width / (Math.Max(1, _map.RoomWidth) * TileSize);
            foreach (var layer in _map.TileLayers.OrderBy(layer => layer.ZIndex).ThenBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (layer == null || !layer.Visible || layer.Cells == null || layer.Cells.Count == 0)
                    continue;

                var tileSet = TryLoadTileSet(layer.TileSetPath);
                if (tileSet == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    GodotTileAtlasSource source;
                    if (!tileSet.Sources.TryGetValue(cell.SourceId, out source))
                        continue;

                    var atlas = TryLoadImage(source.TextureResPath);
                    if (atlas == null)
                        continue;

                    var sourceRect = new Rectangle(
                        cell.AtlasX * source.RegionWidth,
                        cell.AtlasY * source.RegionHeight,
                        source.RegionWidth,
                        source.RegionHeight);
                    var destination = new RectangleF(
                        bounds.X + cell.X * TileSize * scale,
                        bounds.Y + cell.Y * TileSize * scale,
                        TileSize * scale,
                        TileSize * scale);

                    graphics.DrawImage(atlas, destination, sourceRect, GraphicsUnit.Pixel);
                }
            }
        }

        private void DrawGrid(Graphics graphics, RectangleF bounds, int roomWidth, int roomHeight)
        {
            roomWidth = Math.Max(1, roomWidth);
            roomHeight = Math.Max(1, roomHeight);

            using (var pen = new Pen(Color.FromArgb(38, 66, 82, 95)))
            {
                for (var x = 1; x < roomWidth; x++)
                {
                    var px = bounds.X + bounds.Width * x / roomWidth;
                    graphics.DrawLine(pen, px, bounds.Top, px, bounds.Bottom);
                }

                for (var y = 1; y < roomHeight; y++)
                {
                    var py = bounds.Y + bounds.Height * y / roomHeight;
                    graphics.DrawLine(pen, bounds.Left, py, bounds.Right, py);
                }
            }
        }

        private void DrawCollisionOverlay(Graphics graphics, RectangleF bounds)
        {
            if (!_showCollisionOverlay || _map == null)
                return;

            if (_collisionEditorMode == CollisionEditorMode.TileSetCollision)
            {
                DrawTileCollisionPolygons(graphics, bounds);
                return;
            }

            if (_collisionLayout == null)
                return;

            DrawCollisionSolidCells(graphics, bounds);
            DrawCollisionPolygons(graphics, bounds);
        }

        private void DrawTileCollisionPolygons(Graphics graphics, RectangleF bounds)
        {
            if (_map == null || _map.TileLayers == null || string.IsNullOrWhiteSpace(_godotRoot))
                return;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var scale = bounds.Width / roomPixelWidth;
            using (var dim = new SolidBrush(Color.FromArgb(55, 20, 24, 28)))
            using (var solidPen = new Pen(Color.FromArgb(210, 235, 90, 90), 2f))
            using (var oneWayPen = new Pen(Color.FromArgb(210, 90, 210, 130), 2f))
            using (var selectedPen = new Pen(Color.FromArgb(240, 90, 170, 245), 3f))
            using (var selectedFill = new SolidBrush(Color.FromArgb(72, 90, 170, 245)))
            using (var handleFill = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (var handlePen = new Pen(Color.FromArgb(220, 35, 40, 48), 1f))
            {
                graphics.FillRectangle(dim, bounds);

                foreach (var layer in _map.TileLayers.OrderBy(layer => layer.ZIndex).ThenBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (layer == null || !layer.Visible || layer.Cells == null || layer.Cells.Count == 0 || string.IsNullOrWhiteSpace(layer.TileSetPath))
                        continue;

                    var tileSet = TryLoadTileSet(layer.TileSetPath);
                    if (tileSet == null)
                        continue;

                    foreach (var cell in layer.Cells)
                    {
                        GodotTileAtlasSource source;
                        if (!tileSet.Sources.TryGetValue(cell.SourceId, out source))
                            continue;

                        GodotTilePhysicsPolygon polygon;
                        if (!source.PhysicsPolygons.TryGetValue(BuildTilePhysicsPolygonKey(cell.AtlasX, cell.AtlasY, cell.Alternative), out polygon))
                            continue;
                        if (polygon.Points == null || polygon.Points.Count < 3)
                            continue;

                        var selectedCollision = FindTileCollisionSelection(layer.NodePath, cell.X, cell.Y, cell.Alternative);
                        var selected = selectedCollision != null;
                        var localPoints = selected ? selectedCollision.Points : polygon.Points;
                        var points = BuildTileCollisionScreenPoints(cell.X, cell.Y, localPoints, bounds, scale);
                        if (selected)
                            graphics.FillPolygon(selectedFill, points);
                        graphics.DrawPolygon(selected ? selectedPen : (polygon.OneWay ? oneWayPen : solidPen), points);

                        if (!selected || _collisionEditorTool != CollisionEditorTool.Vertex || selectedCollision != GetPrimaryTileCollisionSelection())
                            continue;

                        var radius = Math.Max(4f, 5f * scale);
                        for (var i = 0; i < points.Length; i++)
                        {
                            var rect = new RectangleF(points[i].X - radius, points[i].Y - radius, radius * 2f, radius * 2f);
                            graphics.FillEllipse(handleFill, rect);
                            graphics.DrawEllipse(handlePen, rect);
                        }
                    }
                }

                if (_tileCollisionMarquee != null)
                {
                    var rect = _tileCollisionMarquee.Bounds;
                    using (var fill = new SolidBrush(Color.FromArgb(42, 90, 170, 245)))
                    using (var pen = new Pen(Color.FromArgb(190, 90, 170, 245), 1.5f))
                    {
                        graphics.FillRectangle(fill, rect);
                        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                }

                if ((_collisionEditorTool == CollisionEditorTool.Move ||
                    _collisionEditorTool == CollisionEditorTool.Rotate ||
                    _collisionEditorTool == CollisionEditorTool.Scale) &&
                    _selectedTileCollisions.Count > 0)
                    DrawTileCollisionGroupTransformGizmo(graphics, bounds, scale);
            }
        }

        private void DrawTileCollisionGroupTransformGizmo(Graphics graphics, RectangleF roomBounds, float scale)
        {
            if (_selectedTileCollisions.Count == 0)
                return;

            var bounds = GetTileCollisionGroupScreenBounds(_selectedTileCollisions, roomBounds, scale);
            if (bounds.Width <= 0f || bounds.Height <= 0f)
                return;

            using (var boxPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f))
            using (var handleFill = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (var handleBorder = new Pen(Color.FromArgb(220, 35, 40, 48), 1f))
            using (var rotatePen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f))
            {
                graphics.DrawRectangle(boxPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);

                if (_collisionEditorTool == CollisionEditorTool.Scale)
                {
                    foreach (var rect in GetScaleHandleRects(bounds).Values)
                    {
                        graphics.FillRectangle(handleFill, rect);
                        graphics.DrawRectangle(handleBorder, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                }

                if (_collisionEditorTool == CollisionEditorTool.Rotate)
                {
                    var rotateCenter = GetRotateHandleCenter(bounds, scale);
                    var topCenter = new PointF(bounds.X + bounds.Width / 2f, bounds.Y);
                    var radius = Math.Max(5f, RotateHandleRadius * scale);
                    graphics.DrawLine(rotatePen, topCenter, rotateCenter);
                    graphics.DrawEllipse(rotatePen, rotateCenter.X - radius, rotateCenter.Y - radius, radius * 2f, radius * 2f);
                }
            }
        }

        private void DrawCollisionSolidCells(Graphics graphics, RectangleF bounds)
        {
            if (_collisionLayout.Solid == null || _collisionLayout.Solid.Length == 0)
                return;

            var roomWidth = Math.Max(1, _collisionLayout.RoomWidth);
            var roomHeight = Math.Max(1, _collisionLayout.RoomHeight);
            var cellWidth = bounds.Width / roomWidth;
            var cellHeight = bounds.Height / roomHeight;
            using (var fill = new SolidBrush(Color.FromArgb(72, 225, 65, 72)))
            using (var outline = new Pen(Color.FromArgb(130, 180, 30, 40)))
            {
                for (var y = 0; y < roomHeight; y++)
                {
                    for (var x = 0; x < roomWidth; x++)
                    {
                        var index = y * roomWidth + x;
                        if (index < 0 || index >= _collisionLayout.Solid.Length || !_collisionLayout.Solid[index])
                            continue;

                        var rect = new RectangleF(bounds.X + x * cellWidth, bounds.Y + y * cellHeight, cellWidth, cellHeight);
                        graphics.FillRectangle(fill, rect);
                        graphics.DrawRectangle(outline, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                }
            }
        }

        private void DrawCollisionPolygons(Graphics graphics, RectangleF bounds)
        {
            if (_collisionLayout.Polygons == null || _collisionLayout.Polygons.Count == 0)
                return;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var scale = bounds.Width / roomPixelWidth;
            var fillColor = _collisionTarget == CollisionLayoutTarget.ForegroundTexture
                ? Color.FromArgb(58, 70, 130, 230)
                : Color.FromArgb(58, 225, 65, 72);
            var lineColor = _collisionTarget == CollisionLayoutTarget.ForegroundTexture
                ? Color.FromArgb(170, 50, 95, 220)
                : Color.FromArgb(170, 190, 35, 45);

            using (var fill = new SolidBrush(fillColor))
            using (var pen = new Pen(lineColor, 2f))
            using (var selectedFill = new SolidBrush(Color.FromArgb(76, 120, 200, 255)))
            using (var selectedPen = new Pen(Color.FromArgb(210, 70, 145, 235), 3f))
            using (var handleBrush = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (var handlePen = new Pen(Color.FromArgb(220, 35, 40, 48), 1f))
            {
                for (var polygonIndex = 0; polygonIndex < _collisionLayout.Polygons.Count; polygonIndex++)
                {
                    var polygon = _collisionLayout.Polygons[polygonIndex];
                    if (polygon == null || polygon.Count < 2)
                        continue;

                    var points = polygon
                        .Where(point => point != null)
                        .Select(point => new PointF(bounds.X + point.X * scale, bounds.Y + point.Y * scale))
                        .ToArray();
                    var selected = polygonIndex == _selectedCollisionPolygonIndex;
                    if (points.Length >= 3)
                        graphics.FillPolygon(selected ? selectedFill : fill, points);
                    if (points.Length >= 2)
                        graphics.DrawPolygon(selected ? selectedPen : pen, points);

                    if (!selected)
                        continue;

                    var radius = Math.Max(4f, 5f * scale);
                    for (var i = 0; i < points.Length; i++)
                    {
                        var rect = new RectangleF(points[i].X - radius, points[i].Y - radius, radius * 2f, radius * 2f);
                        graphics.FillEllipse(handleBrush, rect);
                        graphics.DrawEllipse(handlePen, rect);
                    }

                    DrawCollisionPolygonTransformGizmo(graphics, polygon, bounds, scale);
                }
            }
        }

        private void DrawCollisionPolygonTransformGizmo(Graphics graphics, List<GodotVector2Data> polygon, RectangleF roomBounds, float scale)
        {
            if (polygon == null || polygon.Count < 3)
                return;
            if (_collisionEditorTool != CollisionEditorTool.Move &&
                _collisionEditorTool != CollisionEditorTool.Rotate &&
                _collisionEditorTool != CollisionEditorTool.Scale)
                return;

            var bounds = GetCollisionPolygonScreenBounds(polygon, roomBounds, scale);
            using (var boxPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f))
            using (var handleFill = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (var handleBorder = new Pen(Color.FromArgb(220, 35, 40, 48), 1f))
            using (var rotatePen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f))
            {
                graphics.DrawRectangle(boxPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);

                if (_collisionEditorTool == CollisionEditorTool.Scale)
                {
                    foreach (var rect in GetScaleHandleRects(bounds).Values)
                    {
                        graphics.FillRectangle(handleFill, rect);
                        graphics.DrawRectangle(handleBorder, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                }

                if (_collisionEditorTool == CollisionEditorTool.Rotate)
                {
                    var rotateCenter = GetRotateHandleCenter(bounds, scale);
                    var topCenter = new PointF(bounds.X + bounds.Width / 2f, bounds.Y);
                    var radius = Math.Max(5f, RotateHandleRadius * scale);
                    graphics.DrawLine(rotatePen, topCenter, rotateCenter);
                    graphics.DrawEllipse(rotatePen, rotateCenter.X - radius, rotateCenter.Y - radius, radius * 2f, radius * 2f);
                }
            }
        }

        private void DrawPortals(Graphics graphics, RectangleF bounds)
        {
            if (_map == null || _map.Portals == null)
                return;

            var roomPixelWidth = Math.Max(1, _map.RoomWidth) * TileSize;
            var scale = bounds.Width / roomPixelWidth;
            foreach (var portal in _map.Portals)
            {
                if (portal == null)
                    continue;

                var x = bounds.X + portal.X * scale;
                var y = bounds.Y + portal.Y * scale;
                var radius = Math.Max(5f, 7f * scale);
                using (var brush = new SolidBrush(Color.FromArgb(230, 46, 134, 193)))
                using (var outline = new Pen(Color.White, 2f))
                {
                    graphics.FillEllipse(brush, x - radius, y - radius, radius * 2f, radius * 2f);
                    graphics.DrawEllipse(outline, x - radius, y - radius, radius * 2f, radius * 2f);
                }

                var label = string.IsNullOrWhiteSpace(portal.Name) ? portal.Id : portal.Name;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    using (var brush = new SolidBrush(Color.FromArgb(35, 40, 48)))
                        graphics.DrawString(label, Font, brush, x + radius + 3f, y - radius);
                }
            }
        }

        private void DrawSceneInfo(Graphics graphics, RectangleF bounds)
        {
            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(_map.DisplayName) ? _map.Id : _map.DisplayName,
                _map.RoomWidth + " x " + _map.RoomHeight + " tiles",
                "Layers: " + (_map.TileLayers == null ? 0 : _map.TileLayers.Count) +
                    "  Portals: " + (_map.Portals == null ? 0 : _map.Portals.Count)
            };

            if (string.IsNullOrWhiteSpace(_godotRoot))
                lines.Add("Godot root not found; external textures may be hidden.");

            using (var brush = new SolidBrush(Color.FromArgb(35, 40, 48)))
            {
                var text = string.Join(Environment.NewLine, lines.ToArray());
                graphics.DrawString(text, Font, brush, Math.Max(6f, bounds.Left), 6f);
            }
        }

        private void DrawEditorInfo(Graphics graphics, RectangleF bounds)
        {
            if (!_showCollisionOverlay)
                return;

            var text = "Collision: " + FormatCollisionEditorMode(_collisionEditorMode) +
                " / " + FormatCollisionEditorTool(_collisionEditorTool);
            var size = graphics.MeasureString(text, Font);
            var rect = new RectangleF(
                bounds.Right - size.Width - 16f,
                Math.Max(6f, bounds.Top + 6f),
                size.Width + 10f,
                size.Height + 6f);

            using (var brush = new SolidBrush(Color.FromArgb(210, 35, 40, 48)))
            using (var textBrush = new SolidBrush(Color.White))
            {
                graphics.FillRectangle(brush, rect);
                graphics.DrawString(text, Font, textBrush, rect.X + 5f, rect.Y + 3f);
            }
        }

        private void DrawCenteredText(Graphics graphics, string text)
        {
            using (var brush = new SolidBrush(Color.FromArgb(55, 65, 75)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(text, Font, brush, ClientRectangle, format);
            }
        }

        private Image TryLoadImage(string resPath)
        {
            var absolute = ToAbsoluteGodotPath(resPath);
            if (absolute.Length == 0 || !File.Exists(absolute))
                return null;

            Image cached;
            if (_imageCache.TryGetValue(absolute, out cached))
                return cached;

            try
            {
                var bytes = File.ReadAllBytes(absolute);
                using (var stream = new MemoryStream(bytes))
                using (var source = Image.FromStream(stream))
                {
                    var bitmap = new Bitmap(source);
                    _imageCache[absolute] = bitmap;
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private GodotTileSet TryLoadTileSet(string resPath)
        {
            var absolute = ToAbsoluteGodotPath(resPath);
            if (absolute.Length == 0 || !File.Exists(absolute))
                return null;

            GodotTileSet cached;
            if (_tileSetCache.TryGetValue(absolute, out cached))
                return cached;

            try
            {
                var tileSet = GodotTileSetLoader.Load(absolute);
                _tileSetCache[absolute] = tileSet;
                return tileSet;
            }
            catch
            {
                return null;
            }
        }

        private string ToAbsoluteGodotPath(string resPath)
        {
            resPath = (resPath ?? string.Empty).Trim();
            if (resPath.Length == 0)
                return string.Empty;

            if (Path.IsPathRooted(resPath))
                return Path.GetFullPath(resPath);

            if (string.IsNullOrWhiteSpace(_godotRoot))
                return string.Empty;

            var relative = resPath.StartsWith("res://", StringComparison.Ordinal)
                ? resPath.Substring("res://".Length)
                : resPath;
            relative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_godotRoot, relative);
        }

        private static PointF ComputeAnchoredOrigin(float roomWidth, float roomHeight, float textureWidth, float textureHeight, TextureAnchor anchor)
        {
            switch (anchor)
            {
                case TextureAnchor.TopRight:
                    return new PointF(roomWidth - textureWidth, 0);
                case TextureAnchor.BottomLeft:
                    return new PointF(0, roomHeight - textureHeight);
                case TextureAnchor.BottomRight:
                    return new PointF(roomWidth - textureWidth, roomHeight - textureHeight);
                case TextureAnchor.Center:
                    return new PointF((roomWidth - textureWidth) / 2f, (roomHeight - textureHeight) / 2f);
                case TextureAnchor.TopLeft:
                default:
                    return new PointF(0, 0);
            }
        }

        private static string FormatCollisionEditorMode(CollisionEditorMode mode)
        {
            switch (mode)
            {
                case CollisionEditorMode.TileSetCollision:
                    return "TileSet";
                case CollisionEditorMode.CollisionLayout:
                default:
                    return "Layout";
            }
        }

        private static string FormatCollisionEditorTool(CollisionEditorTool tool)
        {
            switch (tool)
            {
                case CollisionEditorTool.Vertex:
                    return "Vertex";
                case CollisionEditorTool.Move:
                    return "Move";
                case CollisionEditorTool.Rotate:
                    return "Rotate";
                case CollisionEditorTool.Scale:
                    return "Scale";
                case CollisionEditorTool.AddBox:
                    return "Add Box";
                case CollisionEditorTool.Remove:
                    return "Remove";
                case CollisionEditorTool.Select:
                default:
                    return "Select";
            }
        }
    }

    internal enum CollisionEditorMode
    {
        TileSetCollision = 0,
        CollisionLayout = 1
    }

    internal enum CollisionEditorTool
    {
        Select = 0,
        Vertex = 1,
        Move = 2,
        Rotate = 3,
        Scale = 4,
        AddBox = 5,
        Remove = 6
    }

    internal enum CollisionPolygonTransformKind
    {
        Move = 0,
        Rotate = 1,
        Scale = 2
    }

    internal enum CollisionScaleHandleKind
    {
        TopLeft = 0,
        Top = 1,
        TopRight = 2,
        Right = 3,
        BottomRight = 4,
        Bottom = 5,
        BottomLeft = 6,
        Left = 7
    }

    internal struct CollisionPolygonTransformHit
    {
        public CollisionPolygonTransformHit(CollisionPolygonTransformKind kind, CollisionScaleHandleKind scaleHandleKind)
        {
            Kind = kind;
            ScaleHandleKind = scaleHandleKind;
        }

        public CollisionPolygonTransformKind Kind { get; private set; }
        public CollisionScaleHandleKind ScaleHandleKind { get; private set; }
    }

    internal struct TileCollisionGroupTransformHit
    {
        public TileCollisionGroupTransformHit(CollisionPolygonTransformKind kind, CollisionScaleHandleKind scaleHandleKind)
        {
            Kind = kind;
            ScaleHandleKind = scaleHandleKind;
        }

        public CollisionPolygonTransformKind Kind { get; private set; }
        public CollisionScaleHandleKind ScaleHandleKind { get; private set; }
    }

    internal struct CollisionPolygonBounds
    {
        public CollisionPolygonBounds(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public float MinX { get; private set; }
        public float MinY { get; private set; }
        public float MaxX { get; private set; }
        public float MaxY { get; private set; }

        public PointF Center
        {
            get { return new PointF((MinX + MaxX) / 2f, (MinY + MaxY) / 2f); }
        }

        public PointF GetHandle(CollisionScaleHandleKind kind)
        {
            switch (kind)
            {
                case CollisionScaleHandleKind.TopLeft:
                    return new PointF(MinX, MinY);
                case CollisionScaleHandleKind.Top:
                    return new PointF((MinX + MaxX) / 2f, MinY);
                case CollisionScaleHandleKind.TopRight:
                    return new PointF(MaxX, MinY);
                case CollisionScaleHandleKind.Right:
                    return new PointF(MaxX, (MinY + MaxY) / 2f);
                case CollisionScaleHandleKind.BottomRight:
                    return new PointF(MaxX, MaxY);
                case CollisionScaleHandleKind.Bottom:
                    return new PointF((MinX + MaxX) / 2f, MaxY);
                case CollisionScaleHandleKind.BottomLeft:
                    return new PointF(MinX, MaxY);
                case CollisionScaleHandleKind.Left:
                    return new PointF(MinX, (MinY + MaxY) / 2f);
                default:
                    return new PointF(MaxX, MaxY);
            }
        }

        public PointF GetOppositeHandle(CollisionScaleHandleKind kind)
        {
            switch (kind)
            {
                case CollisionScaleHandleKind.TopLeft:
                    return GetHandle(CollisionScaleHandleKind.BottomRight);
                case CollisionScaleHandleKind.Top:
                    return GetHandle(CollisionScaleHandleKind.Bottom);
                case CollisionScaleHandleKind.TopRight:
                    return GetHandle(CollisionScaleHandleKind.BottomLeft);
                case CollisionScaleHandleKind.Right:
                    return GetHandle(CollisionScaleHandleKind.Left);
                case CollisionScaleHandleKind.BottomRight:
                    return GetHandle(CollisionScaleHandleKind.TopLeft);
                case CollisionScaleHandleKind.Bottom:
                    return GetHandle(CollisionScaleHandleKind.Top);
                case CollisionScaleHandleKind.BottomLeft:
                    return GetHandle(CollisionScaleHandleKind.TopRight);
                case CollisionScaleHandleKind.Left:
                    return GetHandle(CollisionScaleHandleKind.Right);
                default:
                    return Center;
            }
        }
    }

    internal sealed class CollisionPolygonTransformDrag
    {
        public CollisionPolygonTransformDrag(
            CollisionPolygonTransformKind kind,
            int polygonIndex,
            CollisionLayoutData beforeLayout,
            List<GodotVector2Data> startPoints,
            CollisionPolygonBounds startBounds,
            PointF startMouseWorld,
            CollisionScaleHandleKind scaleHandleKind)
        {
            Kind = kind;
            PolygonIndex = polygonIndex;
            BeforeLayout = beforeLayout;
            StartPoints = startPoints;
            StartBounds = startBounds;
            StartMouseWorld = startMouseWorld;
            ScaleHandleKind = scaleHandleKind;
        }

        public CollisionPolygonTransformKind Kind { get; private set; }
        public int PolygonIndex { get; private set; }
        public CollisionLayoutData BeforeLayout { get; private set; }
        public List<GodotVector2Data> StartPoints { get; private set; }
        public CollisionPolygonBounds StartBounds { get; private set; }
        public PointF StartMouseWorld { get; private set; }
        public CollisionScaleHandleKind ScaleHandleKind { get; private set; }
        public bool Moved { get; set; }

        public string EditName
        {
            get
            {
                if (Kind == CollisionPolygonTransformKind.Rotate)
                    return "Polygon rotated";
                if (Kind == CollisionPolygonTransformKind.Scale)
                    return "Polygon scaled";
                return "Polygon moved";
            }
        }
    }

    internal sealed class TileCollisionGroupTransformDrag
    {
        public TileCollisionGroupTransformDrag(
            CollisionPolygonTransformKind kind,
            List<TileCollisionSelection> selections,
            List<List<GodotVector2>> startPoints,
            CollisionPolygonBounds startBounds,
            PointF startMouseWorld,
            CollisionScaleHandleKind scaleHandleKind)
        {
            Kind = kind;
            Selections = selections ?? new List<TileCollisionSelection>();
            StartPoints = startPoints ?? new List<List<GodotVector2>>();
            StartBounds = startBounds;
            StartMouseWorld = startMouseWorld;
            ScaleHandleKind = scaleHandleKind;
        }

        public CollisionPolygonTransformKind Kind { get; private set; }
        public List<TileCollisionSelection> Selections { get; private set; }
        public List<List<GodotVector2>> StartPoints { get; private set; }
        public CollisionPolygonBounds StartBounds { get; private set; }
        public PointF StartMouseWorld { get; private set; }
        public CollisionScaleHandleKind ScaleHandleKind { get; private set; }
        public bool Moved { get; set; }
    }

    internal sealed class TileCollisionMarquee
    {
        public TileCollisionMarquee(Point start)
        {
            Start = start;
            End = start;
        }

        public Point Start { get; private set; }
        public Point End { get; set; }

        public Rectangle Bounds
        {
            get
            {
                var left = Math.Min(Start.X, End.X);
                var top = Math.Min(Start.Y, End.Y);
                var right = Math.Max(Start.X, End.X);
                var bottom = Math.Max(Start.Y, End.Y);
                return Rectangle.FromLTRB(left, top, right, bottom);
            }
        }

        public bool IsDrag
        {
            get { return Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y) >= 6; }
        }
    }

    internal sealed class CollisionLayoutEditedEventArgs : EventArgs
    {
        public CollisionLayoutEditedEventArgs(
            CollisionLayoutData layout,
            CollisionLayoutTarget target,
            int cellX,
            int cellY,
            bool solid,
            CollisionLayoutData beforeLayout,
            CollisionLayoutData afterLayout)
        {
            Layout = layout;
            Target = target;
            CellX = cellX;
            CellY = cellY;
            Solid = solid;
            BeforeLayout = beforeLayout;
            AfterLayout = afterLayout;
        }

        public CollisionLayoutData Layout { get; private set; }
        public CollisionLayoutTarget Target { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public bool Solid { get; private set; }
        public CollisionLayoutData BeforeLayout { get; private set; }
        public CollisionLayoutData AfterLayout { get; private set; }
    }

    internal sealed class CollisionLayoutPolygonSelectedEventArgs : EventArgs
    {
        public CollisionLayoutPolygonSelectedEventArgs(CollisionLayoutData layout, CollisionLayoutTarget target, int polygonIndex)
        {
            Layout = layout;
            Target = target;
            PolygonIndex = polygonIndex;
        }

        public CollisionLayoutData Layout { get; private set; }
        public CollisionLayoutTarget Target { get; private set; }
        public int PolygonIndex { get; private set; }
    }

    internal sealed class CollisionLayoutPolygonEditedEventArgs : EventArgs
    {
        public CollisionLayoutPolygonEditedEventArgs(
            CollisionLayoutData layout,
            CollisionLayoutTarget target,
            int polygonIndex,
            string editName,
            CollisionLayoutData beforeLayout,
            CollisionLayoutData afterLayout)
        {
            Layout = layout;
            Target = target;
            PolygonIndex = polygonIndex;
            EditName = editName ?? string.Empty;
            BeforeLayout = beforeLayout;
            AfterLayout = afterLayout;
        }

        public CollisionLayoutData Layout { get; private set; }
        public CollisionLayoutTarget Target { get; private set; }
        public int PolygonIndex { get; private set; }
        public string EditName { get; private set; }
        public CollisionLayoutData BeforeLayout { get; private set; }
        public CollisionLayoutData AfterLayout { get; private set; }
    }

    internal sealed class TileCollisionSelectedEventArgs : EventArgs
    {
        public TileCollisionSelectedEventArgs(TileCollisionSelection selection)
            : this(selection, selection == null ? 0 : 1)
        {
        }

        public TileCollisionSelectedEventArgs(TileCollisionSelection selection, int selectionCount)
        {
            Selection = selection;
            SelectionCount = selectionCount;
        }

        public TileCollisionSelection Selection { get; private set; }
        public int SelectionCount { get; private set; }
    }

    internal sealed class TileCollisionEditCommittedEventArgs : EventArgs
    {
        public TileCollisionEditCommittedEventArgs(TileCollisionSelection selection, List<GodotVector2> fromPoints, List<GodotVector2> toPoints)
            : this(new List<TileCollisionEditItem> { new TileCollisionEditItem(selection, fromPoints, toPoints) })
        {
        }

        public TileCollisionEditCommittedEventArgs(List<TileCollisionEditItem> edits)
        {
            Edits = edits ?? new List<TileCollisionEditItem>();
            Selection = Edits.Count == 0 ? null : Edits[0].Selection;
            FromPoints = Edits.Count == 0 ? new List<GodotVector2>() : Edits[0].FromPoints;
            ToPoints = Edits.Count == 0 ? new List<GodotVector2>() : Edits[0].ToPoints;
        }

        public List<TileCollisionEditItem> Edits { get; private set; }
        public TileCollisionSelection Selection { get; private set; }
        public List<GodotVector2> FromPoints { get; private set; }
        public List<GodotVector2> ToPoints { get; private set; }
        public bool Accepted { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal sealed class TileCollisionEditItem
    {
        public TileCollisionEditItem(TileCollisionSelection selection, List<GodotVector2> fromPoints, List<GodotVector2> toPoints)
        {
            Selection = selection;
            FromPoints = fromPoints ?? new List<GodotVector2>();
            ToPoints = toPoints ?? new List<GodotVector2>();
        }

        public TileCollisionSelection Selection { get; private set; }
        public List<GodotVector2> FromPoints { get; private set; }
        public List<GodotVector2> ToPoints { get; private set; }
    }

    internal sealed class TileCollisionAddBoxRequestedEventArgs : EventArgs
    {
        public TileCollisionAddBoxRequestedEventArgs(TileCollisionCellHit cell, List<GodotVector2> points)
        {
            Cell = cell;
            Points = points ?? new List<GodotVector2>();
        }

        public TileCollisionCellHit Cell { get; private set; }
        public List<GodotVector2> Points { get; private set; }
        public bool Accepted { get; set; }
    }

    internal sealed class TileCollisionRemoveRequestedEventArgs : EventArgs
    {
        public TileCollisionRemoveRequestedEventArgs(TileCollisionCellHit cell)
        {
            Cell = cell;
        }

        public TileCollisionCellHit Cell { get; private set; }
        public bool Accepted { get; set; }
    }

    internal sealed class TileCollisionContextRequestedEventArgs : EventArgs
    {
        public TileCollisionContextRequestedEventArgs(TileCollisionSelection selection, Point location)
            : this(selection == null ? new List<TileCollisionSelection>() : new List<TileCollisionSelection> { selection }, location)
        {
        }

        public TileCollisionContextRequestedEventArgs(List<TileCollisionSelection> selections, Point location)
        {
            Selections = selections ?? new List<TileCollisionSelection>();
            Selection = Selections.Count == 0 ? null : Selections[0];
            Location = location;
        }

        public List<TileCollisionSelection> Selections { get; private set; }
        public TileCollisionSelection Selection { get; private set; }
        public Point Location { get; private set; }
    }

    internal sealed class TileCollisionCellHit
    {
        public TileCollisionCellHit(
            string tileSetResPath,
            string layerNodePath,
            int sourceId,
            int atlasX,
            int atlasY,
            int cellX,
            int cellY,
            int alternative)
        {
            TileSetResPath = tileSetResPath ?? string.Empty;
            LayerNodePath = layerNodePath ?? string.Empty;
            SourceId = sourceId;
            AtlasX = atlasX;
            AtlasY = atlasY;
            CellX = cellX;
            CellY = cellY;
            Alternative = alternative;
        }

        public string TileSetResPath { get; private set; }
        public string LayerNodePath { get; private set; }
        public int SourceId { get; private set; }
        public int AtlasX { get; private set; }
        public int AtlasY { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public int Alternative { get; private set; }
    }

    internal sealed class TileCollisionSelection
    {
        public TileCollisionSelection(
            string tileSetResPath,
            string layerNodePath,
            int sourceId,
            int atlasX,
            int atlasY,
            int cellX,
            int cellY,
            int alternative,
            bool oneWay,
            List<GodotVector2> points)
        {
            TileSetResPath = tileSetResPath ?? string.Empty;
            LayerNodePath = layerNodePath ?? string.Empty;
            SourceId = sourceId;
            AtlasX = atlasX;
            AtlasY = atlasY;
            CellX = cellX;
            CellY = cellY;
            Alternative = alternative;
            OneWay = oneWay;
            Points = points ?? new List<GodotVector2>();
        }

        public string TileSetResPath { get; private set; }
        public string LayerNodePath { get; private set; }
        public int SourceId { get; private set; }
        public int AtlasX { get; private set; }
        public int AtlasY { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public int Alternative { get; private set; }
        public bool OneWay { get; private set; }
        public List<GodotVector2> Points { get; private set; }

        public string FormatSummary()
        {
            return "layer=" + LayerNodePath +
                "; cell=" + CellX + "," + CellY +
                "; atlas=" + AtlasX + ":" + AtlasY +
                "; alt=" + Alternative +
                "; vertices=" + Points.Count +
                "; oneWay=" + OneWay;
        }

        public void ReplacePoints(List<GodotVector2> points)
        {
            Points = points ?? new List<GodotVector2>();
        }
    }

    internal sealed class PortalMoveCommittedEventArgs : EventArgs
    {
        public PortalMoveCommittedEventArgs(Portal portal, float fromX, float fromY, float toX, float toY)
        {
            Portal = portal;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
        }

        public Portal Portal { get; private set; }
        public float FromX { get; private set; }
        public float FromY { get; private set; }
        public float ToX { get; private set; }
        public float ToY { get; private set; }
        public bool Accepted { get; set; }
    }

    internal sealed class PortalAddRequestedEventArgs : EventArgs
    {
        public PortalAddRequestedEventArgs(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; private set; }
        public float Y { get; private set; }
    }

    internal sealed class PortalContextRequestedEventArgs : EventArgs
    {
        public PortalContextRequestedEventArgs(Portal portal)
        {
            Portal = portal;
        }

        public Portal Portal { get; private set; }
    }
}
