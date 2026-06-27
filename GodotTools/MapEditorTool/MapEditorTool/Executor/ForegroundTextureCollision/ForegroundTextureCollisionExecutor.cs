using System;
using System.Drawing;
using System.IO;
using System.Linq;
using MapEditorTool.Executor.CollisionLayout;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.ForegroundTextureCollision
{
    public sealed class ForegroundTextureCollisionExecutor
    {
        private const int DefaultAlphaThreshold = 254;
        private readonly CollisionLayoutExecutor _collisionLayoutExecutor;

        public ForegroundTextureCollisionExecutor()
            : this(new CollisionLayoutExecutor())
        {
        }

        public ForegroundTextureCollisionExecutor(CollisionLayoutExecutor collisionLayoutExecutor)
        {
            _collisionLayoutExecutor = collisionLayoutExecutor ?? throw new ArgumentNullException("collisionLayoutExecutor");
        }

        public ForegroundTextureCollisionResult BuildLayout(string godotRoot, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var texturePath = ResolveOptionalTexturePath(godotRoot, map.ForegroundTexturePath);
            if (texturePath.Length == 0 || !File.Exists(texturePath))
                return BuildTileFallbackResult(map, texturePath, "Foreground texture was not found.");

            try
            {
                using (var bitmap = new Bitmap(texturePath))
                {
                    var hasAlpha = ForegroundTextureCollisionHelper.BitmapHasAlphaChannel(bitmap);
                    var layout = ForegroundTextureCollisionHelper.BuildFromForegroundTexture(map, bitmap, DefaultAlphaThreshold);
                    var polygonCount = layout.Polygons == null ? 0 : layout.Polygons.Count;
                    var solidCount = ForegroundTextureCollisionHelper.CountSolidTiles(layout);
                    var usedTextureAlpha = polygonCount > 0 || solidCount > 0;

                    if (!usedTextureAlpha)
                        return BuildTileFallbackResult(map, texturePath, "Foreground texture produced no collision area.");

                    return new ForegroundTextureCollisionResult
                    {
                        TextureFilePath = Path.GetFullPath(texturePath),
                        Layout = layout,
                        UsedTextureAlpha = true,
                        UsedTileFallback = false,
                        TextureHasAlphaChannel = hasAlpha,
                        SolidTileCount = solidCount,
                        PolygonCount = polygonCount,
                        Summary = "usedTextureAlpha=true; polygons=" + polygonCount + "; solidTiles=" + solidCount
                    };
                }
            }
            catch
            {
                return BuildTileFallbackResult(map, texturePath, "Foreground texture could not be read.");
            }
        }

        public ForegroundTextureCollisionResult BuildAndWriteLayout(string godotRoot, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var result = BuildLayout(godotRoot, map);
            var save = _collisionLayoutExecutor.SaveLayout(godotRoot, map, CollisionLayoutTarget.ForegroundTexture, result.Layout);

            result.CollisionResPath = save.CollisionResPath;
            result.CollisionFilePath = save.CollisionFilePath;
            result.Layout = save.Layout;
            result.WroteCollisionFile = save.WroteFile;
            result.Summary += "; wroteCollisionFile=" + save.WroteFile + "; collisionPath=" + save.CollisionResPath;
            return result;
        }

        public bool ValidateForegroundTextureHasAlpha(string godotRoot, string foregroundTexturePath)
        {
            var textureFilePath = ResolveOptionalTexturePath(godotRoot, foregroundTexturePath);
            if (textureFilePath.Length == 0 || !File.Exists(textureFilePath))
                return false;

            try
            {
                using (var bitmap = new Bitmap(textureFilePath))
                {
                    return ForegroundTextureCollisionHelper.BitmapHasAlphaChannel(bitmap);
                }
            }
            catch
            {
                return false;
            }
        }

        private static ForegroundTextureCollisionResult BuildTileFallbackResult(MapDefinition map, string textureFilePath, string reason)
        {
            var layout = ForegroundTextureCollisionHelper.BuildFromTileLayers(map);
            var solidCount = ForegroundTextureCollisionHelper.CountSolidTiles(layout);
            var polygonCount = layout.Polygons == null ? 0 : layout.Polygons.Count;
            return new ForegroundTextureCollisionResult
            {
                TextureFilePath = string.IsNullOrWhiteSpace(textureFilePath) ? string.Empty : Path.GetFullPath(textureFilePath),
                Layout = layout,
                UsedTextureAlpha = false,
                UsedTileFallback = true,
                TextureHasAlphaChannel = false,
                SolidTileCount = solidCount,
                PolygonCount = polygonCount,
                Summary = "usedTileFallback=true; reason=" + reason + "; polygons=" + polygonCount + "; solidTiles=" + solidCount
            };
        }

        private static string ResolveOptionalTexturePath(string godotRoot, string texturePath)
        {
            texturePath = (texturePath ?? string.Empty).Trim();
            if (texturePath.Length == 0)
                return string.Empty;
            if (texturePath.StartsWith("res://", StringComparison.Ordinal))
                return ToAbsoluteGodotPath(godotRoot, texturePath);
            return Path.GetFullPath(texturePath);
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");
            if (string.IsNullOrWhiteSpace(resPath))
                throw new FileNotFoundException("Godot resource path is empty.");

            var relative = resPath.StartsWith("res://", StringComparison.Ordinal)
                ? resPath.Substring("res://".Length)
                : resPath;
            relative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, relative);
        }

    }
}
