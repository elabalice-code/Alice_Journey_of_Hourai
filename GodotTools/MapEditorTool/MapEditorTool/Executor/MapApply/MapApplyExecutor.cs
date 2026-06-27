using System;
using System.IO;
using MapEditorTool.Executor.CollisionLayout;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Executor.MapTexture;
using MapEditorTool.Executor.ScenePatch;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapApply
{
    public sealed class MapApplyExecutor
    {
        private readonly MapCreationExecutor _mapCreationExecutor;
        private readonly ScenePatchExecutor _scenePatchExecutor;
        private readonly MapTextureExecutor _mapTextureExecutor;
        private readonly CollisionLayoutExecutor _collisionLayoutExecutor;

        public MapApplyExecutor()
            : this(new MapCreationExecutor(), new ScenePatchExecutor(), new MapTextureExecutor(), new CollisionLayoutExecutor())
        {
        }

        public MapApplyExecutor(
            MapCreationExecutor mapCreationExecutor,
            ScenePatchExecutor scenePatchExecutor,
            MapTextureExecutor mapTextureExecutor,
            CollisionLayoutExecutor collisionLayoutExecutor)
        {
            _mapCreationExecutor = mapCreationExecutor ?? throw new ArgumentNullException("mapCreationExecutor");
            _scenePatchExecutor = scenePatchExecutor ?? throw new ArgumentNullException("scenePatchExecutor");
            _mapTextureExecutor = mapTextureExecutor ?? throw new ArgumentNullException("mapTextureExecutor");
            _collisionLayoutExecutor = collisionLayoutExecutor ?? throw new ArgumentNullException("collisionLayoutExecutor");
        }

        public MapApplyResult ApplyMapToGodot(string godotRoot, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");
            if (string.IsNullOrWhiteSpace(map.ScenePath))
                throw new FileNotFoundException("Map scene path is empty.");

            var ensure = _mapCreationExecutor.EnsureMapSceneExists(godotRoot, map);
            var sceneFilePath = ensure.SceneFilePath;
            if (string.IsNullOrWhiteSpace(sceneFilePath) || !File.Exists(sceneFilePath))
                sceneFilePath = ToAbsoluteGodotPath(godotRoot, map.ScenePath);

            var result = new MapApplyResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                CreatedScene = ensure.CreatedScene,
                CreatedCollisionFiles = ensure.CreatedTileCollisionFile || ensure.CreatedForegroundTextureCollisionFile
            };
            result.Steps.Add(ensure.Summary);

            var runtimeNodes = _scenePatchExecutor.PatchMapRuntimeNodes(sceneFilePath, map);
            result.PatchedRuntimeNodes = runtimeNodes.Patched;
            result.Steps.Add("runtimeNodes: " + runtimeNodes.NewRawValue);

            var textures = _mapTextureExecutor.PatchMapTextures(sceneFilePath, map);
            result.PatchedTextures = textures.Patched;
            result.Steps.Add("textures: " + textures.Summary);

            var textureMetadata = _mapTextureExecutor.PatchTextureMetadata(sceneFilePath, map);
            result.PatchedTextureMetadata = textureMetadata.Patched;
            result.Steps.Add("textureMetadata: " + textureMetadata.Summary);

            var backgroundVisibility = _scenePatchExecutor.PatchBackgroundTileLayerVisibility(sceneFilePath, map);
            result.PatchedBackgroundTileLayerVisibility = backgroundVisibility.Patched;
            result.Steps.Add("backgroundTileLayerVisibility: " + backgroundVisibility.NewRawValue);

            var tileCollisionPath = _collisionLayoutExecutor.ResolveCollisionDataResPath(map, CollisionLayoutTarget.Tile, true);
            var foregroundCollisionPath = _collisionLayoutExecutor.ResolveCollisionDataResPath(map, CollisionLayoutTarget.ForegroundTexture, true);
            var collisionMetadata = _scenePatchExecutor.PatchCollisionMetadata(
                sceneFilePath,
                map.CollisionUsed,
                tileCollisionPath,
                foregroundCollisionPath);
            result.PatchedCollisionMetadata = collisionMetadata.Patched;
            result.Steps.Add("collisionMetadata: " + collisionMetadata.NewRawValue);

            result.Summary =
                "createdScene=" + result.CreatedScene +
                "; createdCollisionFiles=" + result.CreatedCollisionFiles +
                "; patchedRuntimeNodes=" + result.PatchedRuntimeNodes +
                "; patchedTextures=" + result.PatchedTextures +
                "; patchedTextureMetadata=" + result.PatchedTextureMetadata +
                "; patchedBackgroundTileLayerVisibility=" + result.PatchedBackgroundTileLayerVisibility +
                "; patchedCollisionMetadata=" + result.PatchedCollisionMetadata;
            return result;
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
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
