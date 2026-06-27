using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.ForegroundTextureCollision
{
    internal static class ForegroundTextureCollisionHelper
    {
        public static CollisionLayoutData BuildFromForegroundTexture(MapDefinition map, Bitmap sourceBitmap, int alphaThreshold)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (sourceBitmap == null)
                throw new ArgumentNullException("sourceBitmap");

            var layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            using (var bitmap = Ensure32bppArgb(sourceBitmap))
            {
                var width = layout.RoomWidth;
                var height = layout.RoomHeight;
                if (width <= 0 || height <= 0 || bitmap.Width <= 0 || bitmap.Height <= 0)
                    return BuildFromTileLayers(map);

                alphaThreshold = Clamp(alphaThreshold, 0, 254);
                var roomWorldWidth = Math.Max(1, map.RoomWidth) * 32;
                var roomWorldHeight = Math.Max(1, map.RoomHeight) * 32;
                var upscale = Math.Max(0.0001f, map.ForegroundTextureUpscale);
                var textureWorldWidthFloat = bitmap.Width * upscale;
                var textureWorldHeightFloat = bitmap.Height * upscale;
                var textureWorldWidth = Math.Max(1, RoundToInt(textureWorldWidthFloat));
                var textureWorldHeight = Math.Max(1, RoundToInt(textureWorldHeightFloat));
                var offset = ComputeAnchorOffset(map.ForegroundTextureAnchor, roomWorldWidth, roomWorldHeight, textureWorldWidthFloat, textureWorldHeightFloat);

                layout.Polygons = BuildCollisionPolygonsFromAlpha(bitmap, textureWorldWidth, textureWorldHeight, alphaThreshold);
                if (layout.Polygons.Count > 0)
                {
                    OffsetPolygons(layout.Polygons, offset.X, offset.Y);
                    return layout;
                }

                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var worldX0 = x * 32f;
                        var worldY0 = y * 32f;
                        var worldX1 = (x + 1) * 32f;
                        var worldY1 = (y + 1) * 32f;

                        var localX0 = (worldX0 - offset.X) / upscale;
                        var localY0 = (worldY0 - offset.Y) / upscale;
                        var localX1 = (worldX1 - offset.X) / upscale;
                        var localY1 = (worldY1 - offset.Y) / upscale;

                        var pixelX0 = FloorToInt(localX0);
                        var pixelY0 = FloorToInt(localY0);
                        var pixelX1 = FloorToInt(localX1) - 1;
                        var pixelY1 = FloorToInt(localY1) - 1;

                        if (pixelX1 < 0 || pixelY1 < 0 || pixelX0 >= bitmap.Width || pixelY0 >= bitmap.Height)
                            continue;

                        pixelX0 = Clamp(pixelX0, 0, bitmap.Width - 1);
                        pixelY0 = Clamp(pixelY0, 0, bitmap.Height - 1);
                        pixelX1 = Clamp(pixelX1, 0, bitmap.Width - 1);
                        pixelY1 = Clamp(pixelY1, 0, bitmap.Height - 1);

                        var alpha = SampleRectAverageAlpha(bitmap, pixelX0, pixelY0, pixelX1, pixelY1);
                        if (alpha > alphaThreshold)
                        {
                            var index = y * width + x;
                            if (index >= 0 && index < layout.Solid.Length)
                                layout.Solid[index] = true;
                        }
                    }
                }
            }

            layout.Polygons = BuildCollisionPolygonsFromSolidTiles(
                layout.Solid,
                layout.RoomWidth,
                layout.RoomHeight,
                Math.Max(1, map.RoomWidth) * 32,
                Math.Max(1, map.RoomHeight) * 32);

            if (layout.Polygons.Count > 0)
                return layout;

            return BuildFromTileLayers(map);
        }

        public static CollisionLayoutData BuildFromTileLayers(MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            var layer = SelectForegroundTileLayer(map);
            if (layer == null || layer.Cells == null || layer.Cells.Count == 0)
                return layout;

            var width = layout.RoomWidth;
            var height = layout.RoomHeight;
            foreach (var cell in layer.Cells)
            {
                if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
                    continue;

                var index = cell.Y * width + cell.X;
                if (index >= 0 && index < layout.Solid.Length)
                    layout.Solid[index] = true;
            }

            layout.Polygons = BuildCollisionPolygonsFromSolidTiles(
                layout.Solid,
                width,
                height,
                Math.Max(1, map.RoomWidth) * 32,
                Math.Max(1, map.RoomHeight) * 32);
            return layout;
        }

        public static bool BitmapHasAlphaChannel(Bitmap bitmap)
        {
            if (bitmap == null)
                return false;

            return (bitmap.PixelFormat & PixelFormat.Alpha) != 0 ||
                bitmap.PixelFormat == PixelFormat.Format32bppArgb ||
                bitmap.PixelFormat == PixelFormat.Format32bppPArgb ||
                bitmap.PixelFormat == PixelFormat.Format64bppArgb ||
                bitmap.PixelFormat == PixelFormat.Format64bppPArgb;
        }

        public static int CountSolidTiles(CollisionLayoutData layout)
        {
            if (layout == null || layout.Solid == null)
                return 0;
            return layout.Solid.Count(value => value);
        }

        private static TileLayer SelectForegroundTileLayer(MapDefinition map)
        {
            var layers = map.TileLayers ?? new List<TileLayer>();
            return layers.FirstOrDefault(layer => layer.ZIndex == 3)
                ?? layers.FirstOrDefault(layer => (layer.Name ?? string.Empty).IndexOf("Foreground", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? layers.OrderByDescending(layer => layer.ZIndex).FirstOrDefault();
        }

        private static PointF ComputeAnchorOffset(TextureAnchor anchor, float roomWidth, float roomHeight, float textureWidth, float textureHeight)
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

        private static void OffsetPolygons(List<List<GodotVector2Data>> polygons, float offsetX, float offsetY)
        {
            if (polygons == null || polygons.Count == 0)
                return;

            foreach (var polygon in polygons)
            {
                if (polygon == null)
                    continue;

                foreach (var point in polygon)
                {
                    point.X += offsetX;
                    point.Y += offsetY;
                }
            }
        }

        private static int SampleRectAverageAlpha(Bitmap bitmap, int x0, int y0, int x1, int y1)
        {
            var width = Math.Max(1, x1 - x0 + 1);
            var height = Math.Max(1, y1 - y0 + 1);
            var sampleXCount = width >= 8 ? 4 : 2;
            var sampleYCount = height >= 8 ? 4 : 2;

            var sum = 0;
            var count = 0;
            for (var yi = 0; yi < sampleYCount; yi++)
            {
                var y = y0 + RoundToInt(yi * (height - 1) / (float)Math.Max(1, sampleYCount - 1));
                for (var xi = 0; xi < sampleXCount; xi++)
                {
                    var x = x0 + RoundToInt(xi * (width - 1) / (float)Math.Max(1, sampleXCount - 1));
                    sum += bitmap.GetPixel(x, y).A;
                    count++;
                }
            }

            return count == 0 ? 0 : RoundToInt(sum / (float)count);
        }

        private static List<List<GodotVector2Data>> BuildCollisionPolygonsFromAlpha(Bitmap bitmap, int worldWidth, int worldHeight, int threshold)
        {
            worldWidth = Math.Max(1, worldWidth);
            worldHeight = Math.Max(1, worldHeight);
            if (bitmap.Width <= 0 || bitmap.Height <= 0)
                return new List<List<GodotVector2Data>>();

            threshold = Clamp(threshold, 0, 254);
            const int maxDim = 4096;
            var gridWidth = Math.Max(1, Math.Min(bitmap.Width, maxDim));
            var gridHeight = Math.Max(1, Math.Min(bitmap.Height, maxDim));
            var scaleX = bitmap.Width / (float)gridWidth;
            var scaleY = bitmap.Height / (float)gridHeight;
            var solidPixels = new bool[gridWidth * gridHeight];

            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = null;
            try
            {
                data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                var bytesPerPixel = Image.GetPixelFormatSize(data.PixelFormat) / 8;
                if (bytesPerPixel < 4)
                    return new List<List<GodotVector2Data>>();

                var stride = data.Stride;
                var absoluteStride = Math.Abs(stride);
                var totalBytes = absoluteStride * data.Height;
                var buffer = new byte[totalBytes];
                Marshal.Copy(data.Scan0, buffer, 0, totalBytes);

                var anySolid = false;
                var anyEmpty = false;
                for (var y = 0; y < gridHeight; y++)
                {
                    var sourceY = RoundToInt((y + 0.5f) * scaleY - 0.5f);
                    sourceY = Clamp(sourceY, 0, bitmap.Height - 1);
                    var rowY = stride > 0 ? sourceY : bitmap.Height - 1 - sourceY;
                    var row = rowY * absoluteStride;
                    for (var x = 0; x < gridWidth; x++)
                    {
                        var sourceX = RoundToInt((x + 0.5f) * scaleX - 0.5f);
                        sourceX = Clamp(sourceX, 0, bitmap.Width - 1);
                        var index = row + sourceX * bytesPerPixel;
                        var alpha = buffer[index + 3];
                        var solid = alpha > threshold;
                        solidPixels[y * gridWidth + x] = solid;
                        anySolid |= solid;
                        anyEmpty |= !solid;
                    }
                }

                if (!anySolid)
                    return new List<List<GodotVector2Data>>();
                if (!anyEmpty)
                {
                    return new List<List<GodotVector2Data>>
                    {
                        new List<GodotVector2Data>
                        {
                            new GodotVector2Data { X = 0, Y = 0 },
                            new GodotVector2Data { X = worldWidth, Y = 0 },
                            new GodotVector2Data { X = worldWidth, Y = worldHeight },
                            new GodotVector2Data { X = 0, Y = worldHeight }
                        }
                    };
                }
            }
            catch
            {
                return new List<List<GodotVector2Data>>();
            }
            finally
            {
                if (data != null)
                    bitmap.UnlockBits(data);
            }

            return BuildCollisionPolygonsFromSolidPixelEdges(solidPixels, gridWidth, gridHeight, worldWidth, worldHeight, 5f);
        }

        private static List<List<GodotVector2Data>> BuildCollisionPolygonsFromSolidPixelEdges(
            bool[] solidPixels,
            int gridWidth,
            int gridHeight,
            int worldWidth,
            int worldHeight,
            float stepPixels)
        {
            gridWidth = Math.Max(1, gridWidth);
            gridHeight = Math.Max(1, gridHeight);
            worldWidth = Math.Max(1, worldWidth);
            worldHeight = Math.Max(1, worldHeight);
            if (solidPixels.Length != gridWidth * gridHeight)
                return new List<List<GodotVector2Data>>();

            var edges = new List<Edge>(Math.Min(1000000, Math.Max(1, gridWidth * gridHeight / 2)));
            var outgoing = new Dictionary<int, List<int>>();
            Action<int, int, int, int, byte> addEdge = (sx, sy, ex, ey, dir) =>
            {
                var index = edges.Count;
                edges.Add(new Edge(sx, sy, ex, ey, dir));
                var key = VertexKey(sx, sy);
                List<int> list;
                if (!outgoing.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    outgoing[key] = list;
                }

                list.Add(index);
            };

            const int maxEdges = 6000000;
            for (var y = 0; y < gridHeight; y++)
            {
                var row = y * gridWidth;
                for (var x = 0; x < gridWidth; x++)
                {
                    if (!solidPixels[row + x])
                        continue;

                    if (y == 0 || !IsSolid(solidPixels, gridWidth, gridHeight, x, y - 1))
                        addEdge(x, y, x + 1, y, 0);
                    if (x == gridWidth - 1 || !IsSolid(solidPixels, gridWidth, gridHeight, x + 1, y))
                        addEdge(x + 1, y, x + 1, y + 1, 1);
                    if (y == gridHeight - 1 || !IsSolid(solidPixels, gridWidth, gridHeight, x, y + 1))
                        addEdge(x + 1, y + 1, x, y + 1, 2);
                    if (x == 0 || !IsSolid(solidPixels, gridWidth, gridHeight, x - 1, y))
                        addEdge(x, y + 1, x, y, 3);

                    if (edges.Count > maxEdges)
                        return new List<List<GodotVector2Data>>();
                }
            }

            if (edges.Count == 0)
                return new List<List<GodotVector2Data>>();

            var polygons = new List<List<GodotVector2Data>>();
            var nextStart = 0;
            var maxWalk = edges.Count + 16;
            while (true)
            {
                while (nextStart < edges.Count && edges[nextStart].Used)
                    nextStart++;
                if (nextStart >= edges.Count)
                    break;

                var startEdgeIndex = nextStart;
                var startEdge = edges[startEdgeIndex];
                startEdge.Used = true;
                edges[startEdgeIndex] = startEdge;

                var startX = startEdge.Sx;
                var startY = startEdge.Sy;
                var currentX = startEdge.Ex;
                var currentY = startEdge.Ey;
                var previousDirection = startEdge.Dir;
                var loop = new List<Point> { new Point(startX, startY), new Point(currentX, currentY) };

                for (var guard = 0; guard < maxWalk; guard++)
                {
                    if (currentX == startX && currentY == startY)
                        break;

                    List<int> candidates;
                    if (!outgoing.TryGetValue(VertexKey(currentX, currentY), out candidates))
                        break;

                    var best = -1;
                    var bestRank = int.MaxValue;
                    foreach (var edgeIndex in candidates)
                    {
                        var edge = edges[edgeIndex];
                        if (edge.Used)
                            continue;

                        var cost = (edge.Dir - previousDirection + 4) & 3;
                        var rank = cost == 1 ? 0 : cost == 0 ? 1 : cost == 3 ? 2 : 3;
                        if (rank < bestRank)
                        {
                            bestRank = rank;
                            best = edgeIndex;
                            if (rank == 0)
                                break;
                        }
                    }

                    if (best < 0)
                        break;

                    var chosen = edges[best];
                    chosen.Used = true;
                    edges[best] = chosen;
                    previousDirection = chosen.Dir;
                    currentX = chosen.Ex;
                    currentY = chosen.Ey;
                    loop.Add(new Point(currentX, currentY));
                }

                if (loop.Count < 4)
                    continue;
                if (loop[loop.Count - 1].X != startX || loop[loop.Count - 1].Y != startY)
                    continue;

                loop.RemoveAt(loop.Count - 1);
                loop = RemoveCollinear(loop);
                if (loop.Count < 3)
                    continue;

                var sampled = ResampleClosed(loop, Math.Max(1f, stepPixels));
                if (sampled.Count < 3)
                    continue;

                var worldPolygon = new List<GodotVector2Data>(sampled.Count);
                foreach (var point in sampled)
                {
                    worldPolygon.Add(new GodotVector2Data
                    {
                        X = point.X / gridWidth * worldWidth,
                        Y = point.Y / gridHeight * worldHeight
                    });
                }

                if (worldPolygon.Count >= 3)
                    polygons.Add(worldPolygon);
            }

            polygons.Sort((a, b) => Math.Abs(PolygonArea(b)).CompareTo(Math.Abs(PolygonArea(a))));
            return polygons;
        }

        private static List<List<GodotVector2Data>> BuildCollisionPolygonsFromSolidTiles(bool[] solidTiles, int roomWidth, int roomHeight, int worldWidth, int worldHeight)
        {
            worldWidth = Math.Max(1, worldWidth);
            worldHeight = Math.Max(1, worldHeight);
            roomWidth = Math.Max(1, roomWidth);
            roomHeight = Math.Max(1, roomHeight);
            if (solidTiles.Length != roomWidth * roomHeight)
                return new List<List<GodotVector2Data>>();

            var samplesWidth = worldWidth + 1;
            var samplesHeight = worldHeight + 1;
            var samples = new bool[samplesWidth * samplesHeight];
            for (var y = 0; y <= worldHeight; y++)
            {
                var cellY = Math.Min(roomHeight - 1, y / 32);
                var rowBase = cellY * roomWidth;
                for (var x = 0; x <= worldWidth; x++)
                {
                    var cellX = Math.Min(roomWidth - 1, x / 32);
                    samples[y * samplesWidth + x] = solidTiles[rowBase + cellX];
                }
            }

            return BuildCollisionPolygonsFromSamples(samples, worldWidth, worldHeight, worldWidth, worldHeight);
        }

        private static List<List<GodotVector2Data>> BuildCollisionPolygonsFromSamples(bool[] samples, int gridWidth, int gridHeight, int worldWidth, int worldHeight)
        {
            gridWidth = Math.Max(1, gridWidth);
            gridHeight = Math.Max(1, gridHeight);
            worldWidth = Math.Max(1, worldWidth);
            worldHeight = Math.Max(1, worldHeight);

            var samplesWidth = gridWidth + 1;
            var samplesHeight = gridHeight + 1;
            if (samples.Length != samplesWidth * samplesHeight)
                return new List<List<GodotVector2Data>>();

            var neighbors = new Dictionary<int, List<int>>();
            var edges = new HashSet<long>();
            Action<int, int> addEdge = (a, b) =>
            {
                var edgeKey = EdgeKey(a, b);
                if (!edges.Add(edgeKey))
                    return;

                AddNeighbor(neighbors, a, b);
                AddNeighbor(neighbors, b, a);
            };

            for (var y = 0; y < gridHeight; y++)
            {
                for (var x = 0; x < gridWidth; x++)
                {
                    var topLeft = samples[y * samplesWidth + x];
                    var topRight = samples[y * samplesWidth + (x + 1)];
                    var bottomRight = samples[(y + 1) * samplesWidth + (x + 1)];
                    var bottomLeft = samples[(y + 1) * samplesWidth + x];
                    var index = (topLeft ? 1 : 0) | (topRight ? 2 : 0) | (bottomRight ? 4 : 0) | (bottomLeft ? 8 : 0);
                    if (index == 0 || index == 15)
                        continue;

                    var top = VertexKey(2 * x + 1, 2 * y);
                    var right = VertexKey(2 * x + 2, 2 * y + 1);
                    var bottom = VertexKey(2 * x + 1, 2 * y + 2);
                    var left = VertexKey(2 * x, 2 * y + 1);

                    switch (index)
                    {
                        case 1:
                            addEdge(left, top);
                            break;
                        case 2:
                            addEdge(top, right);
                            break;
                        case 3:
                            addEdge(left, right);
                            break;
                        case 4:
                            addEdge(right, bottom);
                            break;
                        case 5:
                            addEdge(left, top);
                            addEdge(right, bottom);
                            break;
                        case 6:
                            addEdge(top, bottom);
                            break;
                        case 7:
                            addEdge(left, bottom);
                            break;
                        case 8:
                            addEdge(bottom, left);
                            break;
                        case 9:
                            addEdge(top, bottom);
                            break;
                        case 10:
                            addEdge(top, right);
                            addEdge(bottom, left);
                            break;
                        case 11:
                            addEdge(right, bottom);
                            break;
                        case 12:
                            addEdge(left, right);
                            break;
                        case 13:
                            addEdge(top, right);
                            break;
                        case 14:
                            addEdge(left, top);
                            break;
                    }
                }
            }

            if (edges.Count == 0)
                return new List<List<GodotVector2Data>>();

            var polygons = new List<List<GodotVector2Data>>();
            var maxWalk = edges.Count + 16;
            while (edges.Count > 0)
            {
                var first = edges.First();
                var a = (int)(first >> 32);
                var b = (int)(first & 0xFFFFFFFF);
                var loopKeys = new List<int> { a };
                var previous = a;
                var current = b;
                edges.Remove(EdgeKey(a, b));

                for (var guard = 0; guard < maxWalk; guard++)
                {
                    loopKeys.Add(current);
                    var next = PickNext(current, previous, neighbors, edges);
                    if (next < 0)
                        break;

                    previous = current;
                    current = next;
                    edges.Remove(EdgeKey(previous, current));
                    if (current == a)
                        break;
                }

                if (loopKeys.Count < 4 || loopKeys[loopKeys.Count - 1] != a)
                    continue;

                loopKeys.RemoveAt(loopKeys.Count - 1);
                var loop = new List<GodotVector2Data>(loopKeys.Count);
                foreach (var key in loopKeys)
                {
                    var decoded = DecodeVertexKey(key);
                    var gridX = decoded.X / 2f;
                    var gridY = decoded.Y / 2f;
                    loop.Add(new GodotVector2Data
                    {
                        X = gridX / gridWidth * worldWidth,
                        Y = gridY / gridHeight * worldHeight
                    });
                }

                loop = SimplifyPolygon(loop, 0.9f);
                loop = SimplifyPolygonRdp(loop, 6f);
                if (loop.Count < 3)
                    continue;

                var center = AveragePoint(loop);
                if (!SampleWorld(samples, gridWidth, gridHeight, worldWidth, worldHeight, center.X, center.Y))
                    continue;

                polygons.Add(loop);
            }

            return polygons;
        }

        private static List<GodotVector2Data> SimplifyPolygon(List<GodotVector2Data> points, float minSegmentLength)
        {
            if (points.Count < 4)
                return points;
            minSegmentLength = Math.Max(0.0001f, minSegmentLength);
            var minSegmentLengthSquared = minSegmentLength * minSegmentLength;
            var filtered = new List<GodotVector2Data>(points.Count) { ClonePoint(points[0]) };

            for (var i = 1; i < points.Count; i++)
            {
                var point = points[i];
                var last = filtered[filtered.Count - 1];
                var dx = point.X - last.X;
                var dy = point.Y - last.Y;
                if (dx * dx + dy * dy >= minSegmentLengthSquared)
                    filtered.Add(ClonePoint(point));
            }

            if (filtered.Count >= 3)
            {
                var first = filtered[0];
                var last = filtered[filtered.Count - 1];
                var dx = first.X - last.X;
                var dy = first.Y - last.Y;
                if (dx * dx + dy * dy < minSegmentLengthSquared)
                    filtered.RemoveAt(filtered.Count - 1);
            }

            if (filtered.Count < 4)
                return filtered;

            var simplified = new List<GodotVector2Data>(filtered.Count);
            for (var i = 0; i < filtered.Count; i++)
            {
                var previous = filtered[(i - 1 + filtered.Count) % filtered.Count];
                var current = filtered[i];
                var next = filtered[(i + 1) % filtered.Count];
                var ax = current.X - previous.X;
                var ay = current.Y - previous.Y;
                var bx = next.X - current.X;
                var by = next.Y - current.Y;
                var cross = ax * by - ay * bx;
                if (Math.Abs(cross) > 0.01f)
                    simplified.Add(ClonePoint(current));
            }

            return simplified.Count >= 3 ? simplified : filtered;
        }

        private static List<GodotVector2Data> SimplifyPolygonRdp(List<GodotVector2Data> points, float epsilon)
        {
            if (points.Count < 4)
                return points;
            epsilon = Math.Max(0.0001f, epsilon);
            var line = new List<GodotVector2Data>(points.Count + 1);
            line.AddRange(points.Select(ClonePoint));
            line.Add(ClonePoint(points[0]));

            var keep = new bool[line.Count];
            keep[0] = true;
            keep[keep.Length - 1] = true;
            var stack = new Stack<Tuple<int, int>>();
            stack.Push(Tuple.Create(0, line.Count - 1));
            var epsilonSquared = epsilon * epsilon;

            while (stack.Count > 0)
            {
                var range = stack.Pop();
                var a = range.Item1;
                var b = range.Item2;
                if (b <= a + 1)
                    continue;

                var maxDistanceSquared = -1f;
                var maxIndex = -1;
                for (var i = a + 1; i < b; i++)
                {
                    var distanceSquared = DistancePointToSegmentSquared(line[i], line[a], line[b]);
                    if (distanceSquared > maxDistanceSquared)
                    {
                        maxDistanceSquared = distanceSquared;
                        maxIndex = i;
                    }
                }

                if (maxIndex >= 0 && maxDistanceSquared > epsilonSquared)
                {
                    keep[maxIndex] = true;
                    stack.Push(Tuple.Create(a, maxIndex));
                    stack.Push(Tuple.Create(maxIndex, b));
                }
            }

            var simplified = new List<GodotVector2Data>(line.Count);
            for (var i = 0; i < line.Count; i++)
            {
                if (keep[i])
                    simplified.Add(ClonePoint(line[i]));
            }

            if (simplified.Count > 1)
                simplified.RemoveAt(simplified.Count - 1);

            return simplified.Count >= 3 ? simplified : points;
        }

        private static float DistancePointToSegmentSquared(GodotVector2Data point, GodotVector2Data a, GodotVector2Data b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;
            var denominator = vx * vx + vy * vy;
            if (denominator < 0.000001f)
            {
                var dx = point.X - a.X;
                var dy = point.Y - a.Y;
                return dx * dx + dy * dy;
            }

            var t = (wx * vx + wy * vy) / denominator;
            if (t <= 0f)
            {
                var dx = point.X - a.X;
                var dy = point.Y - a.Y;
                return dx * dx + dy * dy;
            }
            if (t >= 1f)
            {
                var dx = point.X - b.X;
                var dy = point.Y - b.Y;
                return dx * dx + dy * dy;
            }

            var projectedX = a.X + t * vx;
            var projectedY = a.Y + t * vy;
            var ddx = point.X - projectedX;
            var ddy = point.Y - projectedY;
            return ddx * ddx + ddy * ddy;
        }

        private static GodotVector2Data AveragePoint(List<GodotVector2Data> points)
        {
            if (points.Count == 0)
                return new GodotVector2Data();

            float sumX = 0;
            float sumY = 0;
            foreach (var point in points)
            {
                sumX += point.X;
                sumY += point.Y;
            }

            return new GodotVector2Data { X = sumX / points.Count, Y = sumY / points.Count };
        }

        private static List<Point> RemoveCollinear(List<Point> points)
        {
            if (points.Count < 4)
                return points;

            var output = new List<Point>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var previous = points[(i - 1 + points.Count) % points.Count];
                var current = points[i];
                var next = points[(i + 1) % points.Count];
                var dx1 = current.X - previous.X;
                var dy1 = current.Y - previous.Y;
                var dx2 = next.X - current.X;
                var dy2 = next.Y - current.Y;
                if (dx1 == dx2 && dy1 == dy2)
                    continue;
                output.Add(current);
            }

            return output.Count >= 3 ? output : points;
        }

        private static List<PointF> ResampleClosed(List<Point> points, float step)
        {
            if (points.Count < 3)
                return new List<PointF>();

            var input = points.Select(point => new PointF(point.X, point.Y)).ToList();
            var sampled = new List<PointF>();
            var first = input[0];
            sampled.Add(first);
            var accumulated = 0f;
            var lastAdded = first;

            for (var i = 0; i < input.Count; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Count];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var segmentLength = (float)Math.Sqrt(dx * dx + dy * dy);
                if (segmentLength < 0.0001f)
                    continue;

                var t0 = 0f;
                while (accumulated + segmentLength * (1f - t0) >= step)
                {
                    var need = step - accumulated;
                    var t = t0 + need / segmentLength;
                    var point = new PointF(a.X + dx * t, a.Y + dy * t);
                    if (Math.Abs(point.X - lastAdded.X) > 0.001f || Math.Abs(point.Y - lastAdded.Y) > 0.001f)
                    {
                        sampled.Add(point);
                        lastAdded = point;
                    }

                    t0 = t;
                    accumulated = 0f;
                }

                accumulated += segmentLength * (1f - t0);
            }

            if (sampled.Count >= 2)
            {
                var last = sampled[sampled.Count - 1];
                if (Math.Abs(last.X - first.X) < 0.001f && Math.Abs(last.Y - first.Y) < 0.001f)
                    sampled.RemoveAt(sampled.Count - 1);
            }

            return sampled;
        }

        private static float PolygonArea(List<GodotVector2Data> points)
        {
            if (points.Count < 3)
                return 0f;

            double sum = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                sum += a.X * b.Y - a.Y * b.X;
            }

            return (float)(sum * 0.5);
        }

        private static bool SampleWorld(bool[] samples, int gridWidth, int gridHeight, int worldWidth, int worldHeight, float worldX, float worldY)
        {
            var sampleX = RoundToInt(worldX / worldWidth * gridWidth);
            var sampleY = RoundToInt(worldY / worldHeight * gridHeight);
            sampleX = Clamp(sampleX, 0, gridWidth);
            sampleY = Clamp(sampleY, 0, gridHeight);
            return samples[sampleY * (gridWidth + 1) + sampleX];
        }

        private static int PickNext(int current, int previous, Dictionary<int, List<int>> neighbors, HashSet<long> edges)
        {
            List<int> list;
            if (!neighbors.TryGetValue(current, out list))
                return -1;

            foreach (var candidate in list)
            {
                if (candidate == previous)
                    continue;
                if (edges.Contains(EdgeKey(current, candidate)))
                    return candidate;
            }

            foreach (var candidate in list)
            {
                if (edges.Contains(EdgeKey(current, candidate)))
                    return candidate;
            }

            return -1;
        }

        private static void AddNeighbor(Dictionary<int, List<int>> map, int a, int b)
        {
            List<int> list;
            if (!map.TryGetValue(a, out list))
            {
                list = new List<int>();
                map[a] = list;
            }

            list.Add(b);
        }

        private static bool IsSolid(bool[] solidPixels, int gridWidth, int gridHeight, int x, int y)
        {
            return x >= 0 && y >= 0 && x < gridWidth && y < gridHeight && solidPixels[y * gridWidth + x];
        }

        private static int VertexKey(int x, int y)
        {
            return (x << 16) | (y & 0xFFFF);
        }

        private static Point DecodeVertexKey(int key)
        {
            return new Point(key >> 16, (short)(key & 0xFFFF));
        }

        private static long EdgeKey(int a, int b)
        {
            var low = Math.Min(a, b);
            var high = Math.Max(a, b);
            return ((long)low << 32) | (uint)high;
        }

        private static GodotVector2Data ClonePoint(GodotVector2Data point)
        {
            return new GodotVector2Data { X = point.X, Y = point.Y };
        }

        private static Bitmap Ensure32bppArgb(Bitmap source)
        {
            if (source.PixelFormat == PixelFormat.Format32bppArgb || source.PixelFormat == PixelFormat.Format32bppPArgb)
                return (Bitmap)source.Clone();

            var destination = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(destination))
            {
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            return destination;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static int FloorToInt(float value)
        {
            return (int)Math.Floor(value);
        }

        private static int RoundToInt(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private struct Edge
        {
            public Edge(int sx, int sy, int ex, int ey, byte dir)
            {
                Sx = sx;
                Sy = sy;
                Ex = ex;
                Ey = ey;
                Dir = dir;
                Used = false;
            }

            public int Sx;
            public int Sy;
            public int Ex;
            public int Ey;
            public byte Dir;
            public bool Used;
        }
    }
}
