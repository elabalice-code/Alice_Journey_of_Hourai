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

        public MapPreviewCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(245, 247, 250);
            ForeColor = Color.FromArgb(30, 35, 42);
            Font = new Font("Segoe UI", 9f);
            TabStop = true;
            _godotRoot = string.Empty;
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
                _collisionEditorMode == CollisionEditorMode.CollisionLayout &&
                (_collisionEditorTool == CollisionEditorTool.AddBox || _collisionEditorTool == CollisionEditorTool.Remove);
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

            _collisionLayout.Solid[index] = _collisionPaintValue;
            Invalidate();

            var handler = CollisionLayoutEdited;
            if (handler != null)
                handler(this, new CollisionLayoutEditedEventArgs(_collisionLayout, _collisionTarget, x, y, _collisionPaintValue));
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
            if (!_showCollisionOverlay || _collisionLayout == null || _map == null)
                return;

            DrawCollisionSolidCells(graphics, bounds);
            DrawCollisionPolygons(graphics, bounds);
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
            {
                foreach (var polygon in _collisionLayout.Polygons)
                {
                    if (polygon == null || polygon.Count < 2)
                        continue;

                    var points = polygon
                        .Select(point => new PointF(bounds.X + point.X * scale, bounds.Y + point.Y * scale))
                        .ToArray();
                    if (points.Length >= 3)
                        graphics.FillPolygon(fill, points);
                    if (points.Length >= 2)
                        graphics.DrawPolygon(pen, points);
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

    internal sealed class CollisionLayoutEditedEventArgs : EventArgs
    {
        public CollisionLayoutEditedEventArgs(CollisionLayoutData layout, CollisionLayoutTarget target, int cellX, int cellY, bool solid)
        {
            Layout = layout;
            Target = target;
            CellX = cellX;
            CellY = cellY;
            Solid = solid;
        }

        public CollisionLayoutData Layout { get; private set; }
        public CollisionLayoutTarget Target { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public bool Solid { get; private set; }
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
