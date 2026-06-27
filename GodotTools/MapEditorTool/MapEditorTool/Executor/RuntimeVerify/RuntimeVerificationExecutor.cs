using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MapEditorTool.Executor.MapImport;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.RuntimeVerify
{
    public sealed class RuntimeVerificationExecutor
    {
        private readonly MapImportExecutor _importExecutor;

        public RuntimeVerificationExecutor()
            : this(new MapImportExecutor())
        {
        }

        public RuntimeVerificationExecutor(MapImportExecutor importExecutor)
        {
            _importExecutor = importExecutor;
        }

        public MapRuntimeVerificationReport BuildRuntimeVerificationReport(string godotRoot)
        {
            godotRoot = Path.GetFullPath(godotRoot);
            var project = _importExecutor.ImportFromGodotRoot(godotRoot);
            var mapScenes = project.Maps
                .Where(x => !string.IsNullOrWhiteSpace(x.ScenePath) && x.ScenePath.StartsWith("res://", StringComparison.Ordinal))
                .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var mapSceneSet = new HashSet<string>(mapScenes.Select(x => x.ScenePath), StringComparer.OrdinalIgnoreCase);
            var uidIndex = BuildSceneUidIndex(godotRoot);

            var checks = new List<MapRuntimeCheck>();
            AddCoreRuntimeChecks(checks, godotRoot);
            AddMapEditorToolChecks(checks, godotRoot);
            AddMapEditorDataLocationChecks(checks, godotRoot, project);

            var entryRooms = BuildRuntimeEntryRooms(godotRoot, uidIndex, mapSceneSet);
            foreach (var entry in entryRooms)
            {
                if (!entry.Exists || !entry.InImportedMapGraph)
                {
                    checks.Add(new MapRuntimeCheck
                    {
                        Id = "entry-room-" + SanitizeCheckId(entry.Source),
                        Passed = false,
                        Path = entry.ResolvedPath,
                        Detail = entry.Source + " entry room does not resolve to an imported map scene."
                    });
                }
            }

            var portalTargets = BuildRuntimePortalTargets(project, uidIndex, mapSceneSet);
            foreach (var target in portalTargets.Where(x => !x.ResolvesToImportedMap))
            {
                checks.Add(new MapRuntimeCheck
                {
                    Id = "portal-target-" + SanitizeCheckId(target.FromMapPath + "-" + target.PortalId),
                    Passed = false,
                    Path = target.FromMapPath,
                    Detail = "Portal target does not resolve to an imported map: " + target.RawTargetMap
                });
            }

            var issues = checks.Where(x => !x.Passed).ToList();
            return new MapRuntimeVerificationReport
            {
                ProjectRoot = godotRoot,
                ProjectFileExists = File.Exists(Path.Combine(godotRoot, "project.godot")),
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                VerificationKind = "static-game-effect",
                ProofScope = "Static verifier: checks imported MapEditorTool map and portal data against CoreEngine script surfaces that consume target_map, target_area, collision JSON, texture metadata, and generated rooms. It does not execute a live player transition.",
                MapCount = mapScenes.Count,
                PortalCount = project.Maps.Sum(x => x.Portals.Count),
                LinkCount = project.Links.Count,
                PortalTargetCount = portalTargets.Count,
                ResolvedPortalTargetCount = portalTargets.Count(x => x.ResolvesToImportedMap),
                EntryRoomCount = entryRooms.Count,
                ResolvedEntryRoomCount = entryRooms.Count(x => x.Exists && x.InImportedMapGraph),
                CheckCount = checks.Count,
                IssueCount = issues.Count,
                Ok = File.Exists(Path.Combine(godotRoot, "project.godot")) && issues.Count == 0,
                Checks = checks,
                EntryRooms = entryRooms,
                PortalTargets = portalTargets.Take(100).ToList(),
                Issues = issues.Select(x => x.Detail).Take(50).ToList()
            };
        }

        public string FormatRuntimeVerificationSummary(MapRuntimeVerificationReport report)
        {
            var lines = new List<string>
            {
                "MapEditorTool runtime verify",
                "Project: " + report.ProjectRoot,
                "Generated UTC: " + report.GeneratedAtUtc,
                "Kind: " + report.VerificationKind,
                "Overall: " + (report.Ok ? "OK" : "FAILED") + " issues=" + report.IssueCount,
                "Counts: maps=" + report.MapCount +
                    " links=" + report.LinkCount +
                    " portals=" + report.PortalCount +
                    " portalTargets=" + report.ResolvedPortalTargetCount + "/" + report.PortalTargetCount +
                    " entryRooms=" + report.ResolvedEntryRoomCount + "/" + report.EntryRoomCount +
                    " checks=" + report.CheckCount,
                "Scope: " + report.ProofScope,
                "Runtime checks:"
            };

            foreach (var check in report.Checks)
                lines.Add("  " + (check.Passed ? "OK" : "FAIL") + " " + check.Id + " - " + check.Detail);

            lines.Add("Entry rooms:");
            if (report.EntryRooms.Count == 0)
                lines.Add("  none");
            foreach (var entry in report.EntryRooms)
                lines.Add("  " + (entry.Exists && entry.InImportedMapGraph ? "OK" : "FAIL") + " " + entry.Source + ": " + entry.RawValue + " -> " + entry.ResolvedPath);

            lines.Add("Issues:");
            if (report.Issues.Count == 0)
                lines.Add("  none");
            foreach (var issue in report.Issues)
                lines.Add("  " + issue);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static void AddCoreRuntimeChecks(List<MapRuntimeCheck> checks, string godotRoot)
        {
            AddTextCheck(checks, godotRoot, "portal-script-exists", "CoreEngine/Scripts/World/Portal.gd", text => true, "Portal script exists.");
            AddTextCheck(checks, godotRoot, "portal-exports-target-map", "CoreEngine/Scripts/World/Portal.gd",
                text => text.Contains("@export_file(\"room_link\") var target_map"),
                "Portal exports target_map with room_link picker.");
            AddTextCheck(checks, godotRoot, "portal-sends-load-room-request", "CoreEngine/Scripts/World/Portal.gd",
                text => text.Contains("TYPE_LOAD_ROOM_REQUEST") && text.Contains("\"target_map\": target_map"),
                "Portal sends TYPE_LOAD_ROOM_REQUEST with target_map.");
            AddTextCheck(checks, godotRoot, "room-flow-actor-registers-load-room-request", "CoreEngine/Scripts/Actor/RoomFlowActor.gd",
                text => text.Contains("TYPE_LOAD_ROOM_REQUEST") && text.Contains("RoomFlowRouterScript.route"),
                "RoomFlowActor registers load-room messages and delegates them to MapFlow.");
            AddTextCheck(checks, godotRoot, "room-flow-router-reads-target-map", "CoreEngine/Scripts/Signal/MapFlow/RoomFlowRouter.gd",
                text => text.Contains("TYPE_LOAD_ROOM_REQUEST") && text.Contains("target_map"),
                "RoomFlowRouter reads target_map from TYPE_LOAD_ROOM_REQUEST.");
            AddTextCheck(checks, godotRoot, "room-flow-executor-calls-game-load-room", "CoreEngine/Scripts/Actor/RoomFlowIntentExecutor.gd",
                text => text.Contains("game.load_room(target)"),
                "RoomFlowIntentExecutor calls game.load_room(target).");
            AddTextCheck(checks, godotRoot, "metsys-load-room-exists", "addons/MetroidvaniaSystem/Template/Scripts/MetSysGame.gd",
                text => text.Contains("func load_room(path"),
                "MetSysGame exposes load_room(path).");
            AddTextCheck(checks, godotRoot, "game-declares-initial-room", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("INITIAL_ROOM_PATH"),
                "Game.gd declares INITIAL_ROOM_PATH.");
            AddTextCheck(checks, godotRoot, "game-exports-starting-map", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("@export_file(\"room_link\") var starting_map"),
                "Game.gd exports starting_map with room_link picker.");
            AddTextCheck(checks, godotRoot, "area-catalog-starting-rooms", "CoreEngine/Scripts/World/AreaCatalog.gd",
                text => ExtractResPaths(text).Any(x => x.StartsWith("res://CoreEngine/Maps/", StringComparison.Ordinal)),
                "AreaCatalog.gd references area starting rooms.");
            AddTextCheck(checks, godotRoot, "portal-runtime-consumes-mapeditor-portal-fields", "CoreEngine/Scripts/World/Portal.gd",
                text => text.Contains("@export_file(\"room_link\") var target_map")
                    && text.Contains("@export var target_area")
                    && text.Contains("@export_dir var portal_anim_dir")
                    && text.Contains("@export var portal_anim_fps")
                    && text.Contains("@export var portal_upscale")
                    && text.Contains("TYPE_LOAD_ROOM_REQUEST"),
                "Portal runtime consumes MapEditor portal targets and custom portal animation fields.");
            AddTextCheck(checks, godotRoot, "template-room-map-consumes-texture-fields", "CoreEngine/Scripts/World/TemplateRoomMap.gd",
                text => text.Contains("@export var template")
                    && text.Contains("@export var foreground_texture")
                    && text.Contains("@export var background_texture"),
                "TemplateRoomMap consumes MapEditor-authored template and texture fields.");
            AddTextCheck(checks, godotRoot, "map-runtime-surface-consumes-collision-metadata", "CoreEngine/Scripts/Actor/MapRuntimeSurface.gd",
                text => text.Contains("collision_fgtex_path")
                    && text.Contains("CollisionFromJson")
                    && text.Contains("selected_path_from_metadata"),
                "MapRuntimeSurface consumes collision metadata and selects MapEditor collision JSON paths.");
            AddTextCheck(checks, godotRoot, "map-room-load-orchestrator-loads-map-scenes", "CoreEngine/Scripts/Actor/MapRoomLoadOrchestrator.gd",
                text => text.Contains("load_room_with_progress")
                    && text.Contains("load_packed_scene_threaded")
                    && text.Contains("game.map = new_map"),
                "MapRoomLoadOrchestrator owns threaded map scene replacement.");
            AddTextCheck(checks, godotRoot, "generated-room-factory-handles-gen-rooms", "CoreEngine/Scripts/Actor/GeneratedRoomFactory.gd",
                text => text.Contains("path.begins_with(\"GEN\")")
                    && text.Contains("CoreEngine/Maps/Junction.tscn")
                    && text.Contains("apply_config"),
                "GeneratedRoomFactory owns runtime-only GEN room construction from Junction.tscn.");
        }

        private static void AddMapEditorToolChecks(List<MapRuntimeCheck> checks, string godotRoot)
        {
            AddTextCheck(checks, godotRoot, "mapeditortool-imports-portal-targets", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapImport/GodotMapImporter.cs",
                text => text.Contains("\"target_map\"") && text.Contains("\"target_area\""),
                "MapEditorTool importer reads Portal target_map/target_area from map scenes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-imports-collision-metadata", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapImport/GodotMapImporter.cs",
                text => text.Contains("metadata/collision_mode")
                    && text.Contains("metadata/collision_tile_path")
                    && text.Contains("metadata/collision_fgtex_path"),
                "MapEditorTool importer reads collision metadata from map scenes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-imports-tile-map-data", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapImport/GodotMapImporter.cs",
                text => text.Contains("TileMapLayer")
                    && text.Contains("tile_set")
                    && text.Contains("tile_map_data")
                    && text.Contains("DecodeTileMapData"),
                "MapEditorTool importer reads TileMapLayer tile_set and tile_map_data from map scenes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-writes-background-tile-layer-visibility", "GodotTools/MapEditorTool/MapEditorTool/Executor/ScenePatch/ScenePatchExecutor.cs",
                text => text.Contains("PatchBackgroundTileLayerVisibility")
                    && text.Contains("TileMapLayer")
                    && text.Contains("\"visible\"")
                    && text.Contains("IsBackgroundTileLayerName"),
                "MapEditorTool writes background TileMapLayer visibility for imported map scenes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-builds-foreground-texture-collision", "GodotTools/MapEditorTool/MapEditorTool/Executor/ForegroundTextureCollision/ForegroundTextureCollisionExecutor.cs",
                text => text.Contains("BuildAndWriteLayout")
                    && text.Contains("ValidateForegroundTextureHasAlpha")
                    && text.Contains("CollisionLayoutTarget.ForegroundTexture"),
                "MapEditorTool can generate foreground texture collision layouts from alpha textures.");
            AddTextCheck(checks, godotRoot, "mapeditortool-loads-saves-collision-layout-json", "GodotTools/MapEditorTool/MapEditorTool/Executor/CollisionLayout/CollisionLayoutExecutor.cs",
                text => text.Contains("LoadLayout")
                    && text.Contains("SaveLayout")
                    && text.Contains("NormalizeLayout")
                    && text.Contains("collision_tile.json")
                    && text.Contains("collision_fgtex.json"),
                "MapEditorTool can load, normalize, and save collision layout JSON files.");
            AddTextCheck(checks, godotRoot, "mapeditortool-applies-map-state-to-godot", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapApply/MapApplyExecutor.cs",
                text => text.Contains("ApplyMapToGodot")
                    && text.Contains("PatchMapRuntimeNodes")
                    && text.Contains("PatchMapTextures")
                    && text.Contains("PatchCollisionMetadata"),
                "MapEditorTool can apply core MapDefinition state back into Godot scene resources.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-pins-starting-map", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("PinSelectedMapAsStartingMap")
                    && text.Contains("GameSettingsExecutor")
                    && text.Contains("WriteStartingMap"),
                "MapEditorTool UI can pin the selected map as CoreEngine/Game.tscn starting_map.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-loads-saves-collision-layouts", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("InitializeSelectedMapCollision")
                    && text.Contains("LoadSelectedMapCollision")
                    && text.Contains("SaveSelectedMapCollision")
                    && text.Contains("CollisionLayoutExecutor")
                    && text.Contains("ForegroundTextureCollisionExecutor"),
                "MapEditorTool UI can initialize, load, and save selected map collision layouts through executors.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-opens-developer-comment-log", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("OpenDeveloperCommentLog")
                    && text.Contains("OpenCommentLog")
                    && text.Contains("menu.developer.openLog"),
                "MapEditorTool UI can open the developer comment log through an executor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-resource-path-executor", "GodotTools/MapEditorTool/MapEditorTool/Executor/ResourcePath/ResourcePathExecutor.cs",
                text => text.Contains("ConvertToProjectResourcePath")
                    && text.Contains("EnsurePreferredProjectResourceDirectory")
                    && text.Contains("ImportFileToDirectory")
                    && text.Contains("ImportDirectoryToDirectory")
                    && text.Contains("res://"),
                "MapEditorTool can import external files or folders into the Godot project and return res:// paths.");
            AddTextCheck(checks, godotRoot, "mapeditortool-property-grid-resource-path-editor", "GodotTools/MapEditorTool/MapEditorTool/UI/GodotResPathEditor.cs",
                text => text.Contains("UITypeEditor")
                    && text.Contains("OpenFileDialog")
                    && text.Contains("FolderBrowserDialog")
                    && text.Contains("ResourcePathExecutor")
                    && text.Contains("AutoResPathEditorTypeDescriptionProvider"),
                "MapEditorTool UI attaches a resource path editor to PropertyGrid path fields while keeping import side effects in an executor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-portal-editing-executor", "GodotTools/MapEditorTool/MapEditorTool/Executor/PortalEditing/PortalEditingExecutor.cs",
                text => text.Contains("CreatePortal")
                    && text.Contains("float x, float y")
                    && text.Contains("ApplyPortalPropertyChange")
                    && text.Contains("BuildTargetMapChoices")
                    && text.Contains("BuildTargetAreaChoices")
                    && text.Contains("ScenePatchExecutor")
                    && text.Contains("PortalAnimationExecutor"),
                "MapEditorTool can create portal nodes and write portal property changes through executors.");
            AddTextCheck(checks, godotRoot, "mapeditortool-portal-property-grid-editor", "GodotTools/MapEditorTool/MapEditorTool/UI/PortalPropertyEditors.cs",
                text => text.Contains("PortalCollectionEditor")
                    && text.Contains("PortalTargetMapEditor")
                    && text.Contains("PortalTargetAreaEditor")
                    && text.Contains("PortalEditingExecutor")
                    && text.Contains("PropertyValueChanged"),
                "MapEditorTool UI restores Portal PropertyGrid collection editing while delegating side effects to PortalEditingExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-canvas", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("MapPreviewCanvas")
                    && text.Contains("DrawTileLayers")
                    && text.Contains("DrawPortals")
                    && text.Contains("DrawCollisionOverlay")
                    && text.Contains("GodotTileSetLoader")
                    && text.Contains("SetData"),
                "MapEditorTool UI has a real read-only map preview canvas for imported maps, tile layers, textures, portals, and collision overlays.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-portal-drag", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("PortalMoveCommitted")
                    && text.Contains("PortalAddRequested")
                    && text.Contains("PortalContextRequested")
                    && text.Contains("HitTestPortal")
                    && text.Contains("ScreenToWorld")
                    && text.Contains("OnMouseDown")
                    && text.Contains("OnMouseMove")
                    && text.Contains("OnMouseUp"),
                "MapEditorTool map preview supports dragging existing Portal markers, adding Portals, and requesting Portal context actions.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-commits-portal-drag-through-executor", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasPortalMoveCommitted")
                    && text.Contains("PortalMoveCommitted")
                    && text.Contains("MapPreviewCanvasPortalAddRequested")
                    && text.Contains("MapPreviewCanvasPortalContextRequested")
                    && text.Contains("OpenPortalLink")
                    && text.Contains("JumpToPortalTarget")
                    && text.Contains("AddPortalAtWorld")
                    && text.Contains("ApplyPortalPropertyChange")
                    && text.Contains("CreatePortal")
                    && text.Contains("Portal position"),
                "MapEditorTool UI commits Portal drag/add changes through PortalEditingExecutor and exposes Portal context navigation.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-drives-collision-overlay", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("RefreshCollisionOverlayFromToolbar")
                    && text.Contains("SetCollisionOverlay")
                    && text.Contains("ApplyCollisionModeSelectionToMap")
                    && text.Contains("viewModeCombo")
                    && text.Contains("collisionTargetCombo"),
                "MapEditorTool toolbar can drive collision overlay loading and active collision mode selection.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-drives-collision-editor-state", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("UpdateCollisionEditorState")
                    && text.Contains("GetSelectedCollisionEditorMode")
                    && text.Contains("GetSelectedCollisionEditorTool")
                    && text.Contains("ApplyCollisionToolButtonSelection")
                    && text.Contains("SetCollisionEditorState"),
                "MapEditorTool toolbar drives collision editor mode/tool state into the map preview canvas.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-collision-editor-state", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("CollisionEditorMode")
                    && text.Contains("CollisionEditorTool")
                    && text.Contains("SetCollisionEditorState")
                    && text.Contains("DrawEditorInfo"),
                "MapEditorTool map preview can display the active collision editor mode and tool state.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-layout-collision-paint", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("CollisionLayoutEdited")
                    && text.Contains("ApplyCollisionPaintAt")
                    && text.Contains("TryGetCollisionCell")
                    && text.Contains("CollisionEditorTool.AddBox")
                    && text.Contains("CollisionEditorTool.Remove"),
                "MapEditorTool map preview can paint and erase solid cells in collision layout edit mode.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-consumes-layout-collision-paint", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasCollisionLayoutEdited")
                    && text.Contains("CollisionLayoutEdited")
                    && text.Contains("Use Save to write the collision file")
                    && text.Contains("layoutToSave")
                    && text.Contains("_currentCollisionOverlay")
                    && text.Contains("Collision layout"),
                "MapEditorTool UI consumes collision layout paint edits, marks the project dirty, and saves the current edited overlay through the Save collision action.");
            AddTextCheck(checks, godotRoot, "mapeditortool-links-preview-canvas", "GodotTools/MapEditorTool/MapEditorTool/UI/LinksPreviewCanvas.cs",
                text => text.Contains("LinksPreviewCanvas")
                    && text.Contains("DrawEdges")
                    && text.Contains("DrawNodes")
                    && text.Contains("MapLink")
                    && text.Contains("GraphNode")
                    && text.Contains("SetData"),
                "MapEditorTool UI has a read-only links preview graph for imported map connections.");
            AddTextCheck(checks, godotRoot, "mapeditortool-links-preview-navigation", "GodotTools/MapEditorTool/MapEditorTool/UI/LinksPreviewCanvas.cs",
                text => text.Contains("MapSelected")
                    && text.Contains("LinkSelected")
                    && text.Contains("PortalSelected")
                    && text.Contains("PortalTargetRequested")
                    && text.Contains("HitTestNode")
                    && text.Contains("HitTestEdge")
                    && text.Contains("ShowPortalTargetMenu")
                    && text.Contains("OnMouseDown"),
                "MapEditorTool links preview graph can select map nodes, select link edges, and request portal target changes from mouse actions.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-consumes-links-preview-navigation", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("LinksPreviewCanvasMapSelected")
                    && text.Contains("LinksPreviewCanvasLinkSelected")
                    && text.Contains("LinksPreviewCanvasPortalSelected")
                    && text.Contains("LinksPreviewCanvasPortalTargetRequested")
                    && text.Contains("SetPortalLinkTarget")
                    && text.Contains("RestoreLinkForPortal")
                    && text.Contains("ApplyPortalPropertyChange")
                    && text.Contains("SelectMapById")
                    && text.Contains("SelectLink(e.Link)"),
                "MapEditorTool UI consumes links preview navigation and portal target events, writes portal targets through the executor, and rolls back model state on write failure.");
            AddTextCheck(checks, godotRoot, "mapeditortool-viewmodel-link-navigation-state", "GodotTools/MapEditorTool/MapEditorTool/ViewModel/MapEditorShellViewModel.cs",
                text => text.Contains("SelectMapById")
                    && text.Contains("SelectLink(MapLink link)")
                    && text.Contains("ReferenceEquals(item, link)"),
                "MapEditorTool ViewModel exposes pure selection methods for links preview navigation.");
            AddTextCheck(checks, godotRoot, "mapeditortool-project-file-executor", "GodotTools/MapEditorTool/MapEditorTool/Executor/ProjectFile/ProjectFileExecutor.cs",
                text => text.Contains("LoadProject") && text.Contains("SaveProject"),
                "MapEditorTool project file executor can load and save MapProject JSON.");
            AddTextCheck(checks, godotRoot, "mapeditortool-mapproject-removes-link-identities", "GodotTools/MapEditorTool/MapEditorTool/Models/MapProject.cs",
                text => text.Contains("RemoveMapById")
                    && text.Contains("AddMapIdentity")
                    && text.Contains("ScenePath")
                    && text.Contains("Links.RemoveAll"),
                "MapEditorTool model removes links by both map Id and scene path identities when deleting a map.");
        }

        private static void AddTextCheck(
            List<MapRuntimeCheck> checks,
            string godotRoot,
            string id,
            string relativePath,
            Func<string, bool> predicate,
            string detail)
        {
            var path = Path.Combine(godotRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                checks.Add(new MapRuntimeCheck
                {
                    Id = id,
                    Passed = false,
                    Path = relativePath,
                    Detail = detail + " File missing."
                });
                return;
            }

            var text = File.ReadAllText(path);
            checks.Add(new MapRuntimeCheck
            {
                Id = id,
                Passed = predicate(text),
                Path = relativePath,
                Detail = detail
            });
        }

        private static void AddMapEditorDataLocationChecks(List<MapRuntimeCheck> checks, string godotRoot, MapProject project)
        {
            foreach (var map in project.Maps
                .Where(x => !string.IsNullOrWhiteSpace(x.ScenePath))
                .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase))
            {
                checks.Add(new MapRuntimeCheck
                {
                    Id = "map-scene-location-" + SanitizeCheckId(map.ScenePath),
                    Passed = map.ScenePath.StartsWith("res://CoreEngine/Maps/", StringComparison.Ordinal)
                        && File.Exists(ToAbsoluteGodotPath(godotRoot, map.ScenePath)),
                    Path = map.ScenePath,
                    Detail = "Imported map scene must stay under CoreEngine/Maps and exist: " + map.ScenePath
                });

                if (map.CollisionUsed == CollisionMode.ForegroundTexture && string.IsNullOrWhiteSpace(map.ForegroundTextureCollisionDataPath))
                {
                    checks.Add(new MapRuntimeCheck
                    {
                        Id = "map-fg-collision-path-required-" + SanitizeCheckId(map.ScenePath),
                        Passed = false,
                        Path = map.ScenePath,
                        Detail = "Foreground-texture collision mode requires collision_fgtex_path on " + map.ScenePath + "."
                    });
                }
            }

            var collisionPaths = project.Maps
                .SelectMany(map => new[]
                {
                    new CollisionPathItem(map.TileCollisionDataPath, "collision_tile.json", map.ScenePath),
                    new CollisionPathItem(map.ForegroundTextureCollisionDataPath, "collision_fgtex.json", map.ScenePath)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Path))
                .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in collisionPaths)
            {
                var resPath = item.Path.Trim();
                var absPath = ToAbsoluteGodotPath(godotRoot, resPath);
                var locationOk = resPath.StartsWith("res://CoreEngine/Maps/Resources/", StringComparison.Ordinal)
                    && resPath.EndsWith("/" + item.ExpectedFileName, StringComparison.OrdinalIgnoreCase);
                checks.Add(new MapRuntimeCheck
                {
                    Id = "mapeditor-collision-path-location-" + SanitizeCheckId(resPath),
                    Passed = locationOk,
                    Path = resPath,
                    Detail = "MapEditor collision JSON should stay under CoreEngine/Maps/Resources/<Map>/" + item.ExpectedFileName + ": " + resPath
                });
                checks.Add(new MapRuntimeCheck
                {
                    Id = "mapeditor-collision-json-exists-" + SanitizeCheckId(resPath),
                    Passed = File.Exists(absPath),
                    Path = resPath,
                    Detail = "MapEditor collision JSON exists for imported metadata path: " + resPath
                });
                checks.Add(new MapRuntimeCheck
                {
                    Id = "mapeditor-collision-json-shape-" + SanitizeCheckId(resPath),
                    Passed = File.Exists(absPath) && LooksLikeCollisionLayoutJson(absPath),
                    Path = resPath,
                    Detail = "MapEditor collision JSON has room dimensions plus solid grid or polygon data: " + resPath
                });
            }
        }

        private static bool LooksLikeCollisionLayoutJson(string absPath)
        {
            try
            {
                var text = File.ReadAllText(absPath);
                var hasWidth = text.Contains("\"RoomWidth\"") || text.Contains("\"roomWidth\"");
                var hasHeight = text.Contains("\"RoomHeight\"") || text.Contains("\"roomHeight\"");
                var hasSolid = text.Contains("\"Solid\"") || text.Contains("\"solid\"");
                var hasPolygons = text.Contains("\"Polygons\"") || text.Contains("\"polygons\"");
                return hasWidth && hasHeight && (hasSolid || hasPolygons);
            }
            catch
            {
                return false;
            }
        }

        private static List<MapRuntimeEntryRoom> BuildRuntimeEntryRooms(string godotRoot, Dictionary<string, string> uidIndex, HashSet<string> mapSceneSet)
        {
            var entries = new List<MapRuntimeEntryRoom>();
            var gamePath = Path.Combine(godotRoot, "CoreEngine", "Game.tscn");
            if (File.Exists(gamePath))
            {
                var scene = TscnParser.ParseFile(gamePath);
                var gameNode = scene.Nodes.FirstOrDefault(x => string.Equals(x.Name, "Game", StringComparison.Ordinal));
                string startingMap;
                if (gameNode != null && gameNode.RawProps.TryGetValue("starting_map", out startingMap))
                    entries.Add(BuildRuntimeEntryRoom("Game.tscn starting_map", UnquoteGodotValue(startingMap), godotRoot, uidIndex, mapSceneSet));
            }

            var gameScriptPath = Path.Combine(godotRoot, "CoreEngine", "Scripts", "Systems", "Game.gd");
            if (File.Exists(gameScriptPath))
            {
                var initialRoom = ExtractConstString(File.ReadAllText(gameScriptPath), "INITIAL_ROOM_PATH");
                if (!string.IsNullOrWhiteSpace(initialRoom))
                    entries.Add(BuildRuntimeEntryRoom("Game.gd INITIAL_ROOM_PATH", initialRoom, godotRoot, uidIndex, mapSceneSet));
            }

            var areaCatalogPath = Path.Combine(godotRoot, "CoreEngine", "Scripts", "World", "AreaCatalog.gd");
            if (File.Exists(areaCatalogPath))
            {
                foreach (var path in ExtractResPaths(File.ReadAllText(areaCatalogPath))
                    .Where(x => x.StartsWith("res://CoreEngine/Maps/", StringComparison.Ordinal))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    entries.Add(BuildRuntimeEntryRoom("AreaCatalog.gd starting_room", path, godotRoot, uidIndex, mapSceneSet));
                }
            }

            return entries;
        }

        private static MapRuntimeEntryRoom BuildRuntimeEntryRoom(string source, string rawValue, string godotRoot, Dictionary<string, string> uidIndex, HashSet<string> mapSceneSet)
        {
            var resolved = ResolveRuntimeResPath(rawValue, uidIndex);
            return new MapRuntimeEntryRoom
            {
                Source = source,
                RawValue = rawValue,
                ResolvedPath = resolved,
                Exists = !string.IsNullOrWhiteSpace(resolved) && File.Exists(ToAbsoluteGodotPath(godotRoot, resolved)),
                InImportedMapGraph = !string.IsNullOrWhiteSpace(resolved) && mapSceneSet.Contains(resolved)
            };
        }

        private static List<MapRuntimePortalTarget> BuildRuntimePortalTargets(MapProject project, Dictionary<string, string> uidIndex, HashSet<string> mapSceneSet)
        {
            return project.Maps
                .SelectMany(map => map.Portals.Select(portal =>
                {
                    var raw = portal.TargetMapId == null ? string.Empty : portal.TargetMapId.Trim();
                    var resolved = ResolveRuntimeResPath(raw, uidIndex);
                    return new MapRuntimePortalTarget
                    {
                        FromMapPath = map.ScenePath,
                        PortalId = portal.Id,
                        PortalName = portal.Name,
                        RawTargetMap = raw,
                        ResolvedTargetMap = resolved,
                        ResolvesToImportedMap = !string.IsNullOrWhiteSpace(resolved) && mapSceneSet.Contains(resolved)
                    };
                }))
                .Where(x => !string.IsNullOrWhiteSpace(x.RawTargetMap))
                .OrderBy(x => x.FromMapPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PortalName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, string> BuildSceneUidIndex(string godotRoot)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(godotRoot, "*.tscn", SearchOption.AllDirectories))
            {
                if (IsIgnoredScanPath(file))
                    continue;

                try
                {
                    var scene = TscnParser.ParseFile(file);
                    if (string.IsNullOrWhiteSpace(scene.SceneUid))
                        continue;
                    var rel = GetRelativePath(godotRoot, file).Replace('\\', '/');
                    index[scene.SceneUid] = "res://" + rel;
                }
                catch
                {
                    // Ignore malformed or transient files in generated folders.
                }
            }
            return index;
        }

        private static bool IsIgnoredScanPath(string path)
        {
            var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return normalized.IndexOf(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveRuntimeResPath(string rawValue, Dictionary<string, string> uidIndex)
        {
            var value = (rawValue ?? string.Empty).Trim();
            if (value.StartsWith("uid://", StringComparison.OrdinalIgnoreCase))
            {
                string resolved;
                return uidIndex.TryGetValue(value, out resolved) ? resolved : string.Empty;
            }
            if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return value;
            return string.Empty;
        }

        private static string ExtractConstString(string text, string constName)
        {
            var match = Regex.Match(text, "const\\s+" + Regex.Escape(constName) + "\\s*:\\s*\\w+\\s*=\\s*\"(?<value>[^\"]+)\"");
            return match.Success ? match.Groups["value"].Value : string.Empty;
        }

        private static List<string> ExtractResPaths(string text)
        {
            return Regex.Matches(text, "\"(?<path>res://[^\"]+)\"")
                .Cast<Match>()
                .Select(x => x.Groups["path"].Value)
                .ToList();
        }

        private static string UnquoteGodotValue(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }

        private static string SanitizeCheckId(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
                .ToArray();
            return string.Join("-", new string(chars).Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries)).Trim('-');
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath.Substring("res://".Length) : resPath;
            rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, rel);
        }

        private static string GetRelativePath(string rootDir, string path)
        {
            var rootUri = new Uri(EnsureTrailingSeparator(Path.GetFullPath(rootDir)));
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private sealed class CollisionPathItem
        {
            public CollisionPathItem(string path, string expectedFileName, string source)
            {
                Path = path ?? string.Empty;
                ExpectedFileName = expectedFileName ?? string.Empty;
                Source = source ?? string.Empty;
            }

            public string Path { get; private set; }
            public string ExpectedFileName { get; private set; }
            public string Source { get; private set; }
        }
    }
}
