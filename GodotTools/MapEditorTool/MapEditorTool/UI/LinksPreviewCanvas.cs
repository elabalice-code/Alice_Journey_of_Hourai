using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using MapEditorTool.Models;

namespace MapEditorTool.UI
{
    internal sealed class LinksPreviewCanvas : Control
    {
        private const float NodeMinWidth = 120f;
        private const float NodeHeight = 44f;
        private const float NodePadX = 12f;
        private const float ArrowSize = 8f;
        private readonly Dictionary<string, GraphNode> _nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        private readonly List<GraphEdge> _edges = new List<GraphEdge>();
        private MapProject _project;
        private MapDefinition _selectedMap;
        private MapLink _selectedLink;

        public event EventHandler<LinkMapSelectedEventArgs> MapSelected;
        public event EventHandler<LinkSelectedEventArgs> LinkSelected;
        public event EventHandler<PortalSelectedEventArgs> PortalSelected;
        public event EventHandler<PortalTargetRequestedEventArgs> PortalTargetRequested;

        public LinksPreviewCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(32, 35, 40);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9f);
            TabStop = true;
        }

        public void SetData(MapProject project, MapDefinition selectedMap, MapLink selectedLink)
        {
            _project = project;
            _selectedMap = selectedMap;
            _selectedLink = selectedLink;
            RebuildGraph();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(BackColor);

            if (_project == null || _nodes.Count == 0)
            {
                DrawCenteredText(graphics, "Import or open a project to preview map links.");
                return;
            }

            foreach (var node in _nodes.Values)
                node.Bounds = MeasureNodeBounds(graphics, node);

            DrawEdges(graphics);
            DrawNodes(graphics);
            DrawLegend(graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Right)
            {
                var node = HitTestNode(e.Location);
                if (node != null && node.Map != null)
                    ShowPortalTargetMenu(node, e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            var hitNode = HitTestNode(e.Location);
            if (hitNode != null)
            {
                var handler = MapSelected;
                if (handler != null)
                    handler(this, new LinkMapSelectedEventArgs(hitNode.MapId));
                return;
            }

            var hitEdge = HitTestEdge(e.Location);
            if (hitEdge != null)
            {
                var handler = LinkSelected;
                if (handler != null)
                    handler(this, new LinkSelectedEventArgs(hitEdge.Link));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = HitTestNode(e.Location) != null || HitTestEdge(e.Location) != null
                ? Cursors.Hand
                : Cursors.Default;
        }

        private void RebuildGraph()
        {
            _nodes.Clear();
            _edges.Clear();

            if (_project == null || _project.Maps == null)
                return;

            foreach (var map in _project.Maps)
            {
                var id = NormalizeMapId(map);
                if (id.Length == 0)
                    continue;

                _nodes[id] = new GraphNode(id, FormatMapLabel(map), map);
            }

            if (_project.Links != null)
            {
                foreach (var link in _project.Links)
                {
                    if (link == null || link.From == null || link.To == null)
                        continue;

                    var fromId = (link.From.MapId ?? string.Empty).Trim();
                    var toId = (link.To.MapId ?? string.Empty).Trim();
                    if (fromId.Length == 0 || toId.Length == 0)
                        continue;

                    var from = EnsureNode(fromId);
                    var to = EnsureNode(toId);
                    _edges.Add(new GraphEdge(link, from, to));
                }
            }

            LayoutNodes();
        }

        private GraphNode EnsureNode(string mapId)
        {
            GraphNode node;
            if (_nodes.TryGetValue(mapId, out node))
                return node;

            node = new GraphNode(mapId, mapId, null) { IsGhost = true };
            _nodes[mapId] = node;
            return node;
        }

        private void LayoutNodes()
        {
            if (_nodes.Count == 0)
                return;

            var nodes = _nodes.Values.OrderBy(node => node.Label, StringComparer.OrdinalIgnoreCase).ToList();
            if (nodes.Count == 1)
            {
                nodes[0].Position = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
                return;
            }

            var center = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
            var radiusX = Math.Max(120f, ClientSize.Width * 0.36f);
            var radiusY = Math.Max(90f, ClientSize.Height * 0.34f);
            for (var index = 0; index < nodes.Count; index++)
            {
                var angle = -Math.PI / 2.0 + index * Math.PI * 2.0 / nodes.Count;
                nodes[index].Position = new PointF(
                    center.X + (float)Math.Cos(angle) * radiusX,
                    center.Y + (float)Math.Sin(angle) * radiusY);
            }
        }

        private RectangleF MeasureNodeBounds(Graphics graphics, GraphNode node)
        {
            var size = graphics.MeasureString(node.Label, Font);
            var width = Math.Max(NodeMinWidth, size.Width + NodePadX * 2f);
            return new RectangleF(
                node.Position.X - width / 2f,
                node.Position.Y - NodeHeight / 2f,
                width,
                NodeHeight);
        }

        private void DrawEdges(Graphics graphics)
        {
            foreach (var edge in _edges)
            {
                var selected = ReferenceEquals(edge.Link, _selectedLink);
                var color = selected ? Color.FromArgb(246, 190, 105) : Color.FromArgb(145, 155, 168);
                if (edge.From.IsGhost || edge.To.IsGhost)
                    color = Color.FromArgb(205, 105, 105);

                using (var pen = new Pen(color, selected ? 2.4f : 1.5f))
                {
                    var start = TrimLineToNode(edge.From.Position, edge.To.Position, edge.From.Bounds);
                    var end = TrimLineToNode(edge.To.Position, edge.From.Position, edge.To.Bounds);
                    graphics.DrawLine(pen, start, end);
                    DrawArrow(graphics, color, start, end);
                }
            }
        }

        private void DrawNodes(Graphics graphics)
        {
            foreach (var node in _nodes.Values.OrderBy(node => node.IsGhost).ThenBy(node => node.Label, StringComparer.OrdinalIgnoreCase))
            {
                var selected = _selectedMap != null && string.Equals(node.MapId, NormalizeMapId(_selectedMap), StringComparison.Ordinal);
                var fill = node.IsGhost ? Color.FromArgb(72, 38, 42) : Color.FromArgb(48, 54, 62);
                var border = selected ? Color.FromArgb(246, 190, 105) : Color.FromArgb(104, 118, 132);
                var text = node.IsGhost ? Color.FromArgb(235, 170, 170) : ForeColor;

                using (var brush = new SolidBrush(fill))
                using (var borderPen = new Pen(border, selected ? 2.4f : 1.4f))
                using (var textBrush = new SolidBrush(text))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.FillRectangle(brush, node.Bounds);
                    graphics.DrawRectangle(borderPen, node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);
                    graphics.DrawString(node.Label, Font, textBrush, node.Bounds, format);
                }
            }
        }

        private void DrawLegend(Graphics graphics)
        {
            var text = "Maps: " + _nodes.Count + "   Links: " + _edges.Count;
            using (var brush = new SolidBrush(Color.FromArgb(210, 220, 230)))
                graphics.DrawString(text, Font, brush, 10f, 8f);
        }

        private GraphNode HitTestNode(Point point)
        {
            foreach (var node in _nodes.Values.OrderBy(node => node.IsGhost))
            {
                if (node.Bounds.Contains(point))
                    return node;
            }

            return null;
        }

        private GraphEdge HitTestEdge(Point point)
        {
            GraphEdge best = null;
            var bestDistance = 8f;

            foreach (var edge in _edges)
            {
                var start = TrimLineToNode(edge.From.Position, edge.To.Position, edge.From.Bounds);
                var end = TrimLineToNode(edge.To.Position, edge.From.Position, edge.To.Bounds);
                var distance = DistanceToSegment(point, start, end);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = edge;
                }
            }

            return best;
        }

        private static float DistanceToSegment(PointF point, PointF start, PointF end)
        {
            var vx = end.X - start.X;
            var vy = end.Y - start.Y;
            var wx = point.X - start.X;
            var wy = point.Y - start.Y;

            var c1 = vx * wx + vy * wy;
            if (c1 <= 0f)
                return Distance(point, start);

            var c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return Distance(point, end);

            var t = c1 / c2;
            var projection = new PointF(start.X + t * vx, start.Y + t * vy);
            return Distance(point, projection);
        }

        private static float Distance(PointF a, PointF b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private void DrawCenteredText(Graphics graphics, string text)
        {
            using (var brush = new SolidBrush(ForeColor))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(text, Font, brush, ClientRectangle, format);
            }
        }

        private static PointF TrimLineToNode(PointF from, PointF to, RectangleF bounds)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f)
                return from;

            var halfW = bounds.Width / 2f;
            var halfH = bounds.Height / 2f;
            var scaleX = Math.Abs(dx) < 0.001f ? float.MaxValue : halfW / Math.Abs(dx);
            var scaleY = Math.Abs(dy) < 0.001f ? float.MaxValue : halfH / Math.Abs(dy);
            var scale = Math.Min(scaleX, scaleY);
            return new PointF(from.X + dx * scale, from.Y + dy * scale);
        }

        private static void DrawArrow(Graphics graphics, Color color, PointF start, PointF end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f)
                return;

            var ux = dx / length;
            var uy = dy / length;
            var px = -uy;
            var py = ux;
            var left = new PointF(end.X - ux * ArrowSize - px * ArrowSize * 0.6f, end.Y - uy * ArrowSize - py * ArrowSize * 0.6f);
            var right = new PointF(end.X - ux * ArrowSize + px * ArrowSize * 0.6f, end.Y - uy * ArrowSize + py * ArrowSize * 0.6f);

            using (var brush = new SolidBrush(color))
                graphics.FillPolygon(brush, new[] { end, left, right });
        }

        private static string NormalizeMapId(MapDefinition map)
        {
            if (map == null)
                return string.Empty;

            var scenePath = (map.ScenePath ?? string.Empty).Trim();
            if (scenePath.Length > 0)
                return scenePath;

            return (map.Id ?? string.Empty).Trim();
        }

        private static string FormatMapLabel(MapDefinition map)
        {
            if (map == null)
                return string.Empty;

            var name = (map.DisplayName ?? string.Empty).Trim();
            if (name.Length > 0)
                return name;

            var id = NormalizeMapId(map);
            if (id.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.GetFileNameWithoutExtension(id);

            return id;
        }

        private void ShowPortalTargetMenu(GraphNode node, Point location)
        {
            if (_project == null || _project.Maps == null || node == null || node.Map == null)
                return;

            var menu = new ContextMenuStrip();
            menu.Closed += delegate { menu.Dispose(); };

            var title = new ToolStripMenuItem("Portal links for " + node.Label) { Enabled = false };
            menu.Items.Add(title);

            var portals = (node.Map.Portals ?? new List<Portal>())
                .OrderBy(FormatPortalLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(portal => portal.NodePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (portals.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem("No portals") { Enabled = false });
                menu.Show(this, location);
                return;
            }

            foreach (var portal in portals)
            {
                var portalItem = new ToolStripMenuItem(FormatPortalLabel(portal) + "  " + FormatPortalTargetSummary(portal));
                var fromMapId = node.MapId;
                var fromPortalId = (portal.Id ?? string.Empty).Trim();

                var openItem = new ToolStripMenuItem("Select Link");
                openItem.Click += delegate { RaisePortalSelected(fromMapId, fromPortalId); };
                portalItem.DropDownItems.Add(openItem);

                var setTarget = new ToolStripMenuItem("Set Target");
                foreach (var map in _project.Maps.OrderBy(FormatMapLabel, StringComparer.OrdinalIgnoreCase))
                {
                    var targetMapId = NormalizeMapId(map);
                    if (targetMapId.Length == 0)
                        continue;

                    var mapItem = new ToolStripMenuItem(FormatMapLabel(map));
                    var mapOnly = new ToolStripMenuItem("Map Only");
                    mapOnly.Click += delegate { RaisePortalTargetRequested(fromMapId, fromPortalId, targetMapId, string.Empty); };
                    mapItem.DropDownItems.Add(mapOnly);

                    foreach (var targetPortal in (map.Portals ?? new List<Portal>()).OrderBy(FormatPortalLabel, StringComparer.OrdinalIgnoreCase))
                    {
                        var targetPortalId = (targetPortal.Id ?? string.Empty).Trim();
                        var targetPortalItem = new ToolStripMenuItem(FormatPortalLabel(targetPortal));
                        targetPortalItem.Click += delegate { RaisePortalTargetRequested(fromMapId, fromPortalId, targetMapId, targetPortalId); };
                        mapItem.DropDownItems.Add(targetPortalItem);
                    }

                    setTarget.DropDownItems.Add(mapItem);
                }

                portalItem.DropDownItems.Add(setTarget);

                var clearItem = new ToolStripMenuItem("Clear Link");
                clearItem.Click += delegate { RaisePortalTargetRequested(fromMapId, fromPortalId, string.Empty, string.Empty); };
                portalItem.DropDownItems.Add(clearItem);

                menu.Items.Add(portalItem);
            }

            menu.Show(this, location);
        }

        private void RaisePortalSelected(string mapId, string portalId)
        {
            var handler = PortalSelected;
            if (handler != null)
                handler(this, new PortalSelectedEventArgs(mapId, portalId));
        }

        private void RaisePortalTargetRequested(string fromMapId, string fromPortalId, string targetMapId, string targetPortalId)
        {
            var handler = PortalTargetRequested;
            if (handler != null)
                handler(this, new PortalTargetRequestedEventArgs(fromMapId, fromPortalId, targetMapId, targetPortalId));
        }

        private string FormatPortalTargetSummary(Portal portal)
        {
            if (portal == null || string.IsNullOrWhiteSpace(portal.TargetMapId))
                return "(unlinked)";

            var targetMap = FindMap(portal.TargetMapId);
            var mapLabel = targetMap == null ? ShortenMapId(portal.TargetMapId) : FormatMapLabel(targetMap);
            var portalLabel = string.Empty;
            if (!string.IsNullOrWhiteSpace(portal.TargetPortalId) && targetMap != null && targetMap.Portals != null)
            {
                var targetPortal = targetMap.Portals.FirstOrDefault(item =>
                    string.Equals((item.Id ?? string.Empty).Trim(), portal.TargetPortalId.Trim(), StringComparison.Ordinal));
                if (targetPortal != null)
                    portalLabel = FormatPortalLabel(targetPortal);
            }

            return portalLabel.Length == 0 ? "-> " + mapLabel : "-> " + mapLabel + " / " + portalLabel;
        }

        private MapDefinition FindMap(string mapId)
        {
            mapId = (mapId ?? string.Empty).Trim();
            if (_project == null || _project.Maps == null || mapId.Length == 0)
                return null;

            return _project.Maps.FirstOrDefault(map =>
                string.Equals((map.Id ?? string.Empty).Trim(), mapId, StringComparison.Ordinal) ||
                string.Equals((map.ScenePath ?? string.Empty).Trim(), mapId, StringComparison.Ordinal));
        }

        private static string FormatPortalLabel(Portal portal)
        {
            if (portal == null)
                return string.Empty;

            var name = (portal.Name ?? string.Empty).Trim();
            if (name.Length > 0)
                return name;

            var id = (portal.Id ?? string.Empty).Trim();
            if (id.Length > 0)
                return id;

            return (portal.NodePath ?? string.Empty).Trim();
        }

        private static string ShortenMapId(string mapId)
        {
            mapId = (mapId ?? string.Empty).Trim();
            if (mapId.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.GetFileNameWithoutExtension(mapId);

            return mapId;
        }

        private sealed class GraphNode
        {
            public GraphNode(string mapId, string label, MapDefinition map)
            {
                MapId = mapId ?? string.Empty;
                Label = string.IsNullOrWhiteSpace(label) ? MapId : label;
                Map = map;
            }

            public string MapId { get; private set; }
            public string Label { get; private set; }
            public MapDefinition Map { get; private set; }
            public bool IsGhost { get; set; }
            public PointF Position { get; set; }
            public RectangleF Bounds { get; set; }
        }

        private sealed class GraphEdge
        {
            public GraphEdge(MapLink link, GraphNode from, GraphNode to)
            {
                Link = link;
                From = from;
                To = to;
            }

            public MapLink Link { get; private set; }
            public GraphNode From { get; private set; }
            public GraphNode To { get; private set; }
        }
    }

    internal sealed class LinkMapSelectedEventArgs : EventArgs
    {
        public LinkMapSelectedEventArgs(string mapId)
        {
            MapId = mapId ?? string.Empty;
        }

        public string MapId { get; private set; }
    }

    internal sealed class LinkSelectedEventArgs : EventArgs
    {
        public LinkSelectedEventArgs(MapLink link)
        {
            Link = link;
        }

        public MapLink Link { get; private set; }
    }

    internal sealed class PortalSelectedEventArgs : EventArgs
    {
        public PortalSelectedEventArgs(string mapId, string portalId)
        {
            MapId = mapId ?? string.Empty;
            PortalId = portalId ?? string.Empty;
        }

        public string MapId { get; private set; }
        public string PortalId { get; private set; }
    }

    internal sealed class PortalTargetRequestedEventArgs : EventArgs
    {
        public PortalTargetRequestedEventArgs(string fromMapId, string fromPortalId, string targetMapId, string targetPortalId)
        {
            FromMapId = fromMapId ?? string.Empty;
            FromPortalId = fromPortalId ?? string.Empty;
            TargetMapId = targetMapId ?? string.Empty;
            TargetPortalId = targetPortalId ?? string.Empty;
        }

        public string FromMapId { get; private set; }
        public string FromPortalId { get; private set; }
        public string TargetMapId { get; private set; }
        public string TargetPortalId { get; private set; }
    }
}
