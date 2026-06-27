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
            AddTextCheck(checks, godotRoot, "room-flow-executor-owns-map-window-reset", "CoreEngine/Scripts/Actor/RoomFlowIntentExecutor.gd",
                text => text.Contains("KIND_RESET_MAP_STARTING_COORDS")
                    && text.Contains("_reset_map_starting_coords")
                    && text.Contains("UI/MapWindow")
                    && text.Contains("reset_starting_coords"),
                "RoomFlowIntentExecutor owns reset_map_starting_coords UI/MapWindow side effect.");
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
            AddTextCheck(checks, godotRoot, "map-area-state-actor-applies-area-catalog", "CoreEngine/Scripts/Actor/MapAreaStateActor.gd",
                text => text.Contains("AreaCatalog.get_initial_area_id")
                    && text.Contains("AreaCatalog.get_area_def")
                    && text.Contains("KEY_CURRENT_AREA_ID")
                    && text.Contains("TYPE_INPUT_MODE_CHANGE_REQUEST")
                    && text.Contains("MapInputModeNameScript.from_area_input_mode"),
                "MapAreaStateActor owns applying AreaCatalog state into workplace and input mode requests.");
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
            AddTextCheck(checks, godotRoot, "map-runtime-surface-owns-foreground-layer-adapter", "CoreEngine/Scripts/Actor/MapRuntimeSurface.gd",
                text => text.Contains("ensure_world_foreground_texture_sprite")
                    && text.Contains("ForegroundTextureLayer/ForegroundTexture")
                    && text.Contains("Sprite2D.new"),
                "MapRuntimeSurface adapts ForegroundTextureLayer/ForegroundTexture into a world Sprite2D.");
            AddTextCheck(checks, godotRoot, "map-runtime-surface-consumes-texture-transform-metadata", "CoreEngine/Scripts/Actor/MapRuntimeSurface.gd",
                text => text.Contains("foreground_texture_anchor")
                    && text.Contains("foreground_texture_upscale")
                    && text.Contains("background_texture_upscale")
                    && text.Contains("BACKGROUND_PERSPECTIVE_SHADER_CODE")
                    && text.Contains("update_background_texture_focus"),
                "MapRuntimeSurface consumes foreground/background texture transform metadata.");
            AddTextCheck(checks, godotRoot, "map-runtime-surface-owns-background-diagnostics", "CoreEngine/Scripts/Actor/MapRuntimeSurface.gd",
                text => text.Contains("print_background_diagnostics")
                    && text.Contains("BackgroundLayer/BackgroundTexture")
                    && text.Contains("viewport_size"),
                "MapRuntimeSurface owns BackgroundLayer/BackgroundTexture diagnostics.");
            AddTextCheck(checks, godotRoot, "map-runtime-surface-applies-camera-bounds", "CoreEngine/Scripts/Actor/MapRuntimeSurface.gd",
                text => text.Contains("apply_camera_limits_from_metadata")
                    && text.Contains("map_world_bounds_rect")
                    && text.Contains("camera.limit_left")
                    && text.Contains("camera.limit_bottom"),
                "MapRuntimeSurface applies camera bounds from MapEditor collision/texture metadata.");
            AddTextCheck(checks, godotRoot, "map-runtime-guard-resets-player-inside-bounds", "CoreEngine/Scripts/Actor/MapRuntimeGuard.gd",
                text => text.Contains("map_world_bounds_rect")
                    && text.Contains("reset_entry")
                    && text.Contains("global_position")
                    && text.Contains("IsTransferred"),
                "MapRuntimeGuard owns player fall-out reset against MapEditor-derived map bounds.");
            AddTextCheck(checks, godotRoot, "game-delegates-map-runtime-surface", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("MapRuntimeSurfaceScript.apply_surface_metadata(map, player)")
                    && text.Contains("MapRuntimeSurfaceScript.update_background_texture_focus")
                    && text.Contains("MapRuntimeSurfaceScript.apply_camera_limits_from_metadata")
                    && text.Contains("MapRuntimeSurfaceScript.print_background_diagnostics"),
                "Game.gd delegates MapEditor collision, texture, and camera runtime work to MapRuntimeSurface.");
            AddTextCheck(checks, godotRoot, "game-delegates-map-runtime-guard", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("MapRuntimeGuardScript")
                    && text.Contains("_map_runtime_guard.reset_entry(player)")
                    && text.Contains("_map_runtime_guard.tick(delta, map, player, map_changing)")
                    && text.Contains("MetSys.set_player_position(player.position)"),
                "Game.gd delegates fall-out bounds guard to MapRuntimeGuard and keeps the MetSys sync boundary.");
            AddTextCheck(checks, godotRoot, "map-room-load-orchestrator-loads-map-scenes", "CoreEngine/Scripts/Actor/MapRoomLoadOrchestrator.gd",
                text => text.Contains("load_room_with_progress")
                    && text.Contains("load_packed_scene_threaded")
                    && text.Contains("game.map = new_map")
                    && text.Contains("MetSys.current_layer")
                    && text.Contains("room_loaded.emit"),
                "MapRoomLoadOrchestrator owns threaded map scene replacement and MetSys layer sync.");
            AddTextCheck(checks, godotRoot, "map-room-load-orchestrator-handles-generated-and-loop-rooms", "CoreEngine/Scripts/Actor/MapRoomLoadOrchestrator.gd",
                text => text.Contains("loop_path")
                    && text.Contains("consume_loop_path")
                    && text.Contains("instantiate_room")
                    && text.Contains("GeneratedRoomFactoryScript.create"),
                "MapRoomLoadOrchestrator owns loop redirects and generated-room instantiation.");
            AddTextCheck(checks, godotRoot, "map-room-lifecycle-publishes-room-events", "CoreEngine/Scripts/Actor/MapRoomLifecycleActor.gd",
                text => text.Contains("TYPE_ROOM_LOADED")
                    && text.Contains("TYPE_LEVEL_EVENT_REQUEST")
                    && text.Contains("enter_random_level")
                    && text.Contains("exit_random_level")
                    && text.Contains("MetSys.last_player_position")
                    && text.Contains("MetSys.set_player_position"),
                "MapRoomLifecycleActor owns room-loaded publication, random-level transitions, and initial MetSys player-position sync.");
            AddTextCheck(checks, godotRoot, "map-spawn-actor-owns-room-spawn-nodes", "CoreEngine/Scripts/Actor/MapSpawnActor.gd",
                text => text.Contains("teleport_player_to_save_point_if_any")
                    && text.Contains("SavePoint")
                    && text.Contains("ensure_alice")
                    && text.Contains("AliceSpawn")
                    && text.Contains("AliceNPCScene"),
                "MapSpawnActor owns SavePoint and Alice/AliceSpawn room-node conventions.");
            AddTextCheck(checks, godotRoot, "generated-room-factory-handles-gen-rooms", "CoreEngine/Scripts/Actor/GeneratedRoomFactory.gd",
                text => text.Contains("path.begins_with(\"GEN\")")
                    && text.Contains("CoreEngine/Maps/Junction.tscn")
                    && text.Contains("apply_config"),
                "GeneratedRoomFactory owns runtime-only GEN room construction from Junction.tscn.");
            AddTextCheck(checks, godotRoot, "room-load-orchestrator-delegates-generated-room-factory", "CoreEngine/Scripts/Actor/MapRoomLoadOrchestrator.gd",
                text => text.Contains("GeneratedRoomFactoryScript.can_create(effective)")
                    && text.Contains("GeneratedRoomFactoryScript.create(effective)"),
                "MapRoomLoadOrchestrator delegates generated room creation to GeneratedRoomFactory.");
            AddTextCheck(checks, godotRoot, "game-delegates-map-room-loading", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("MapRoomLoadOrchestratorScript")
                    && text.Contains("_map_room_loader.load_room_with_progress")
                    && text.Contains("_map_room_loader.instantiate_room")
                    && text.Contains("super._load_room(effective)")
                    && !text.Contains("func reset_map_starting_coords"),
                "Game.gd delegates map room loading and generated/loop room instantiation to MapRoomLoadOrchestrator.");
            AddTextCheck(checks, godotRoot, "game-delegates-map-spawn-nodes", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("MapSpawnActorScript")
                    && text.Contains("MapSpawnActorScript.teleport_player_to_save_point_if_any")
                    && text.Contains("MapSpawnActorScript.ensure_alice"),
                "Game.gd delegates SavePoint and Alice room-node conventions to MapSpawnActor.");
            AddTextCheck(checks, godotRoot, "game-delegates-map-area-state", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("MapAreaStateActorScript.apply_defaults")
                    && text.Contains("MapAreaStateActorScript.apply_initial_area")
                    && text.Contains("MapAreaStateActorScript.apply_area")
                    && text.Contains("MapAreaStateActorScript.current_area_id")
                    && !text.Contains("AreaCatalog")
                    && !text.Contains("AreaDef"),
                "Game.gd delegates area state/catalog application to MapAreaStateActor.");
            AddTextCheck(checks, godotRoot, "game-delegates-map-room-lifecycle", "CoreEngine/Scripts/Systems/Game.gd",
                text => text.Contains("MapRoomLifecycleActorScript")
                    && text.Contains("_map_room_lifecycle.reset()")
                    && text.Contains("_map_room_lifecycle.publish_room_entered")
                    && !text.Contains("enter_random_level")
                    && !text.Contains("exit_random_level")
                    && !text.Contains("MetSys.last_player_position"),
                "Game.gd delegates room lifecycle publication and random-level state to MapRoomLifecycleActor.");
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
            AddTextCheck(checks, godotRoot, "mapeditortool-cli-executor-entrypoints", "GodotTools/MapEditorTool/MapEditorTool/Cli/CliEntry.cs",
                text => text.Contains("RunStatus")
                    && text.Contains("RunPortalReview")
                    && text.Contains("RunRuntimeVerify")
                    && text.Contains("RunUxAudit")
                    && text.Contains("RunUxWalkthrough")
                    && text.Contains("RunUxReview")
                    && text.Contains("RunImport")
                    && text.Contains("RunValidate")
                    && text.Contains("RunPatchPosition")
                    && text.Contains("MapReportExecutor")
                    && text.Contains("ProjectFileExecutor")
                    && text.Contains("ScenePatchExecutor"),
                "MapEditorTool restores non-interactive CLI entrypoints while routing business work through executors.");
            AddTextCheck(checks, godotRoot, "mapeditortool-program-dispatches-cli", "GodotTools/MapEditorTool/MapEditorTool/Program.cs",
                text => text.Contains("static void Main(string[] args)")
                    && text.Contains("NativeConsole.EnsureConsole")
                    && text.Contains("CliEntry.Run(args)")
                    && text.Contains("Application.Run(new Form1())"),
                "MapEditorTool program entry dispatches command-line invocations to CLI and otherwise launches the WinForms UI.");
            AddTextCheck(checks, godotRoot, "mapeditortool-writes-texture-transform-metadata", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapTexture/MapTextureExecutor.cs",
                text => text.Contains("PatchTextureMetadata")
                    && text.Contains("metadata/foreground_texture_anchor")
                    && text.Contains("metadata/foreground_texture_upscale")
                    && text.Contains("metadata/background_texture_anchor")
                    && text.Contains("metadata/background_texture_upscale")
                    && text.Contains("TscnWriter.PatchFile"),
                "MapEditorTool writes foreground/background texture transform metadata through MapTextureExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-writes-template-texture-fields", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapTexture/MapTextureExecutor.cs",
                text => text.Contains("PatchMapTextures")
                    && text.Contains("IsTemplateRoomMap")
                    && text.Contains("\"background_texture\"")
                    && text.Contains("\"foreground_texture\"")
                    && text.Contains("\"template\"")
                    && text.Contains("TscnWriter.PatchFileWithExtResources"),
                "MapEditorTool writes TemplateRoomMap template, foreground_texture, and background_texture fields through MapTextureExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-writes-room-texture-nodes", "GodotTools/MapEditorTool/MapEditorTool/Executor/MapTexture/MapTextureExecutor.cs",
                text => text.Contains("EnsureBackgroundLayerNodes")
                    && text.Contains("EnsureForegroundTextureWorldNodes")
                    && text.Contains("BackgroundLayer/BackgroundTexture")
                    && text.Contains("ForegroundTextureLayer/ForegroundTexture")
                    && text.Contains("ApplyTextureNodePatch"),
                "MapEditorTool writes ordinary-room background and foreground texture nodes in the expected runtime node paths.");
            AddTextCheck(checks, godotRoot, "mapeditortool-writes-tile-map-data", "GodotTools/MapEditorTool/MapEditorTool/Executor/TileCollision/TileCollisionExecutor.cs",
                text => text.Contains("ApplyTileCollisionEdits")
                    && text.Contains("ApplyTileCollisionAlternativeEdits")
                    && text.Contains("PatchTileMapDataAlternative")
                    && text.Contains("tile_map_data")
                    && text.Contains("TscnWriter.PatchFile"),
                "MapEditorTool writes edited TileMapLayer tile_map_data back into map scenes through TileCollisionExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-writes-background-tile-layer-visibility", "GodotTools/MapEditorTool/MapEditorTool/Executor/ScenePatch/ScenePatchExecutor.cs",
                text => text.Contains("PatchBackgroundTileLayerVisibility")
                    && text.Contains("TileMapLayer")
                    && text.Contains("\"visible\"")
                    && text.Contains("IsBackgroundTileLayerName"),
                "MapEditorTool writes background TileMapLayer visibility for imported map scenes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-builds-foreground-texture-collision", "GodotTools/MapEditorTool/MapEditorTool/Executor/ForegroundTextureCollision/ForegroundTextureCollisionExecutor.cs",
                text => text.Contains("BuildAndWriteLayout")
                    && text.Contains("ValidateForegroundTextureHasAlpha")
                    && text.Contains("CollisionLayoutTarget.ForegroundTexture")
                    && text.Contains("TraceAlpha"),
                "MapEditorTool can generate foreground texture collision layouts from alpha textures and trace alpha polygons for CLI diagnostics.");
            AddTextCheck(checks, godotRoot, "mapeditortool-cli-tracealpha", "GodotTools/MapEditorTool/MapEditorTool/Cli/CliEntry.cs",
                text => text.Contains("case \"tracealpha\"")
                    && text.Contains("RunTraceAlpha")
                    && text.Contains("ForegroundTextureCollisionExecutor")
                    && text.Contains("FormatTraceAlphaSummary"),
                "MapEditorTool restores the tracealpha CLI diagnostic through ForegroundTextureCollisionExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-cli-portalanim", "GodotTools/MapEditorTool/MapEditorTool/Cli/CliEntry.cs",
                text => text.Contains("case \"portalanim\"")
                    && text.Contains("RunPortalAnimation")
                    && text.Contains("PortalAnimationExecutor")
                    && text.Contains("FormatPortalAnimationSummary"),
                "MapEditorTool restores the portalanim CLI extractor through PortalAnimationExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-cli-ux-audit", "GodotTools/MapEditorTool/MapEditorTool/Cli/CliEntry.cs",
                text => text.Contains("case \"ux-audit\"")
                    && text.Contains("RunUxAudit")
                    && text.Contains("BuildUxAudit")
                    && text.Contains("FormatUxAuditSummary"),
                "MapEditorTool restores the ux-audit CLI mirror through MapReportExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-cli-ux-walkthrough", "GodotTools/MapEditorTool/MapEditorTool/Cli/CliEntry.cs",
                text => text.Contains("case \"ux-walkthrough\"")
                    && text.Contains("RunUxWalkthrough")
                    && text.Contains("BuildUxWalkthrough")
                    && text.Contains("FormatUxWalkthroughSummary"),
                "MapEditorTool restores the ux-walkthrough CLI review script through MapReportExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-cli-ux-review", "GodotTools/MapEditorTool/MapEditorTool/Cli/CliEntry.cs",
                text => text.Contains("case \"ux-review\"")
                    && text.Contains("RunUxReview")
                    && text.Contains("BuildUxReview")
                    && text.Contains("FormatUxReviewSummary"),
                "MapEditorTool restores the ux-review CLI result recorder through MapReportExecutor.");
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
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-writes-map-property-changes", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("TryWriteBackMapPropertyChange")
                    && text.Contains("IsMapTextureProperty")
                    && text.Contains("IsMapTextureMetadataProperty")
                    && text.Contains("IsMapCollisionMetadataProperty")
                    && text.Contains("PatchMapTextures")
                    && text.Contains("PatchTextureMetadata")
                    && text.Contains("PatchBackgroundTileLayerVisibility")
                    && text.Contains("PatchCollisionMetadata"),
                "MapEditorTool writes map texture, background visibility, and collision metadata property edits back through executors.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-validates-foreground-texture-alpha", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("ValidateForegroundTexturePropertyChange")
                    && text.Contains("ValidateForegroundTextureHasAlpha")
                    && text.Contains("Foreground texture rejected")
                    && text.Contains("Foreground texture disabled")
                    && text.Contains("ForegroundTextureEnabled = false"),
                "MapEditorTool rejects foreground textures without alpha before writing texture property edits.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-pins-starting-map", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("PinSelectedMapAsStartingMap")
                    && text.Contains("GameSettingsExecutor")
                    && text.Contains("WriteStartingMap")
                    && text.Contains("RefreshPinnedStartingMapFromGodot")
                    && text.Contains("IsPinnedStartingMap"),
                "MapEditorTool UI can pin the selected map as CoreEngine/Game.tscn starting_map and refresh pinned map indicators.");
            AddTextCheck(checks, godotRoot, "mapeditortool-viewmodel-pinned-map-label", "GodotTools/MapEditorTool/MapEditorTool/ViewModel/MapEditorShellViewModel.cs",
                text => text.Contains("SetPinnedStartingMapPath")
                    && text.Contains("PinnedStartingMapPath")
                    && text.Contains("[Pinned]")
                    && text.Contains("IsPinnedStartingMap")
                    && text.Contains("NormalizeResPath"),
                "MapEditorTool ViewModel marks the pinned starting map in the map list snapshot.");
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
                    && text.Contains("AutoResPathEditorTypeDescriptionProvider")
                    && text.Contains("internal static bool IsDirectoryProperty")
                    && text.Contains("internal static string BuildFilter"),
                "MapEditorTool UI attaches a resource path editor to PropertyGrid path fields while keeping import side effects in an executor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-property-grid-resource-browse-shortcuts", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("HookResourceBrowse(mapPropertyGrid)")
                    && text.Contains("HookResourceBrowse(linkPropertyGrid)")
                    && text.Contains("MouseDoubleClick")
                    && text.Contains("Keys.Enter")
                    && text.Contains("BrowseAndAssignResourcePath")
                    && text.Contains("ChooseResourcePath")
                    && text.Contains("ConvertToProjectResourcePath")
                    && text.Contains("TryWriteBackMapPropertyChange"),
                "MapEditorTool PropertyGrid supports double-click and Enter resource browsing while routing import/copy side effects through ResourcePathExecutor.");
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
                    && text.Contains("PropertyValueChanged")
                    && text.Contains("Task.Run")
                    && text.Contains("RunOnUiThread")
                    && text.Contains("RefreshEditorUi"),
                "MapEditorTool UI restores Portal PropertyGrid collection editing, runs long portal writebacks off the UI thread, and delegates side effects to PortalEditingExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-refreshes-after-portal-property-edit", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("RefreshUi = RefreshAfterPortalPropertyEdit")
                    && text.Contains("RefreshAfterPortalPropertyEdit")
                    && text.Contains("MarkSelectedMapEdited(\"Portal property\")")
                    && text.Contains("EvictImageCache")
                    && text.Contains("ApplySnapshotToUi"),
                "MapEditorTool UI refreshes project dirty state, preview image cache, and snapshots after asynchronous portal property writes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-canvas", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("MapPreviewCanvas")
                    && text.Contains("DrawTileLayers")
                    && text.Contains("DrawPortals")
                    && text.Contains("DrawCollisionOverlay")
                    && text.Contains("GodotTileSetLoader")
                    && text.Contains("EvictImageCache")
                    && text.Contains("SetData"),
                "MapEditorTool UI has a real read-only map preview canvas for imported maps, tile layers, textures, portals, and collision overlays.");
            AddTextCheck(checks, godotRoot, "mapeditortool-canvas-hover-tooltips", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("HoverHintRequested += CanvasHoverHintRequested")
                    && text.Contains("BuildPortalHoverText")
                    && text.Contains("BuildEntityHoverText")
                    && text.Contains("CanvasHoverHintRequested")
                    && text.Contains("_lastHoverHintText")
                    && text.Contains("_toolTip.Show"),
                "MapEditorTool UI restores hover tooltips for map preview markers and the links preview graph.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-hover-events", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("HoverHintRequested")
                    && text.Contains("GetPortalHoverText")
                    && text.Contains("GetEntityHoverText")
                    && text.Contains("UpdateHoverHint")
                    && text.Contains("HitTestPortal")
                    && text.Contains("HitTestEntity"),
                "MapEditorTool map preview publishes hover hints for portal and entity markers.");
            AddTextCheck(checks, godotRoot, "mapeditortool-links-preview-hover-events", "GodotTools/MapEditorTool/MapEditorTool/UI/LinksPreviewCanvas.cs",
                text => text.Contains("HoverHintRequested")
                    && text.Contains("BuildNodeHoverText")
                    && text.Contains("BuildEdgeHoverText")
                    && text.Contains("ResolvePortalLabel")
                    && text.Contains("FormatPortalLabel")
                    && text.Contains("HitTestNode")
                    && text.Contains("HitTestEdge"),
                "MapEditorTool links preview publishes hover hints for graph nodes and edges, resolving portal ids to readable portal labels.");
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
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-tileset-collision-selection", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("DrawTileCollisionPolygons")
                    && text.Contains("HitTestTileCollisionPolygon")
                    && text.Contains("TileCollisionSelected")
                    && text.Contains("TileCollisionSelection")
                    && text.Contains("BuildTilePhysicsPolygonKey")
                    && text.Contains("BuildTileCollisionScreenPoints"),
                "MapEditorTool map preview can draw and select TileSet collision polygons for imported tile layers.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-tileset-collision-vertex-drag", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("BeginTileCollisionVertexDrag")
                    && text.Contains("ApplyTileCollisionVertexDrag")
                    && text.Contains("HitTestSelectedTileCollisionVertex")
                    && text.Contains("TileCollisionEditCommitted")
                    && text.Contains("WorldToTileLocal")
                    && text.Contains("EvictTileSetCacheForResPath"),
                "MapEditorTool map preview can drag selected TileSet collision vertices and publish commit requests.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-tileset-collision-add-remove", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("TileCollisionAddBoxRequested")
                    && text.Contains("TileCollisionRemoveRequested")
                    && text.Contains("TryHandleTileCollisionAddRemove")
                    && text.Contains("HitTestTileCell")
                    && text.Contains("CreateDefaultTileCollisionSquare")
                    && text.Contains("TileCollisionCellHit"),
                "MapEditorTool map preview can request Add Box and Remove Collision actions for TileSet collision cells.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-tileset-collision-context", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("TileCollisionContextRequested")
                    && text.Contains("RequestTileCollisionContext")
                    && text.Contains("TileCollisionContextRequestedEventArgs"),
                "MapEditorTool map preview can request TileSet collision context actions from right-click selection.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-collision-tool-shortcuts", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("CollisionToolShortcutRequested")
                    && text.Contains("TryRequestCollisionToolShortcut")
                    && text.Contains("Keys.Q")
                    && text.Contains("Keys.S")
                    && text.Contains("Keys.W")
                    && text.Contains("Keys.E")
                    && text.Contains("Keys.R")
                    && text.Contains("Keys.A")
                    && text.Contains("Keys.D"),
                "MapEditorTool map preview publishes collision tool shortcut requests for Select, Vertex, Move, Rotate, Scale, Add Box, and Remove Collision.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-consumes-collision-tool-shortcuts", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("CollisionToolShortcutRequested += MapPreviewCanvasCollisionToolShortcutRequested")
                    && text.Contains("MapPreviewCanvasCollisionToolShortcutRequested")
                    && text.Contains("GetCollisionToolButtonName")
                    && text.Contains("GetCollisionToolDisplayName")
                    && text.Contains("ApplyCollisionToolButtonSelection")
                    && text.Contains("Collision tool selected"),
                "MapEditorTool UI consumes collision tool shortcut requests and updates toolbar/editor state.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-consumes-tileset-collision-selection", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasTileCollisionSelected")
                    && text.Contains("TileCollisionSelected")
                    && text.Contains("Tile collision selected")
                    && text.Contains("Tile collision selection cleared")
                    && text.Contains("GetSelectedCollisionEditorMode() == CollisionEditorMode.TileSetCollision"),
                "MapEditorTool UI consumes TileSet collision selection events and can show TileSet collision overlay without requiring a collision layout file.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-commits-tileset-collision-edits", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasTileCollisionEditCommitted")
                    && text.Contains("TileCollisionExecutor")
                    && text.Contains("ApplyTileCollisionEdits")
                    && text.Contains("TileCollisionCommit")
                    && text.Contains("Tile collision edit saved")
                    && text.Contains("EvictTileSetCacheForResPath")
                    && text.Contains("ClearTileCollisionSelection"),
                "MapEditorTool UI routes TileSet collision vertex edits through TileCollisionExecutor and refreshes preview cache.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-commits-tileset-collision-add-remove", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasTileCollisionAddBoxRequested")
                    && text.Contains("MapPreviewCanvasTileCollisionRemoveRequested")
                    && text.Contains("ApplyTileCollisionEdits")
                    && text.Contains("ApplyTileCollisionAlternativeEdits")
                    && text.Contains("Tile collision box added")
                    && text.Contains("Tile collision removed"),
                "MapEditorTool UI routes TileSet Add Box and Remove Collision requests through TileCollisionExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-commits-tileset-collision-one-way", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasTileCollisionContextRequested")
                    && text.Contains("SetTileCollisionOneWay")
                    && text.Contains("Set One-Way")
                    && text.Contains("Set Solid")
                    && text.Contains("ApplyTileCollisionEdits")
                    && text.Contains("Tile collision set"),
                "MapEditorTool UI routes TileSet collision one-way/solid context actions through TileCollisionExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-tileset-collision-group-edit", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("TileCollisionMarquee")
                    && text.Contains("HitTestTileCollisionsInRectangle")
                    && text.Contains("BeginTileCollisionGroupTransform")
                    && text.Contains("ApplyTileCollisionGroupTransformDrag")
                    && text.Contains("DrawTileCollisionGroupTransformGizmo")
                    && text.Contains("TileCollisionGroupTransformDrag"),
                "MapEditorTool map preview can multi-select TileSet collision polygons and move, rotate, or scale them as a group.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-commits-tileset-collision-group-edit", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("e.Edits")
                    && text.Contains("new List<TileCollisionCommit>")
                    && text.Contains("Tile collision group edit saved")
                    && text.Contains("HashSet<string>")
                    && text.Contains("SetTileCollisionOneWay(List<TileCollisionSelection>")
                    && text.Contains("selections.All"),
                "MapEditorTool UI batches TileSet collision group edits and one-way/solid updates through TileCollisionExecutor.");
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
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-layout-polygon-edit", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("CollisionLayoutPolygonSelected")
                    && text.Contains("CollisionLayoutPolygonEdited")
                    && text.Contains("HitTestCollisionPolygonVertex")
                    && text.Contains("ApplyCollisionPolygonVertexDrag")
                    && text.Contains("RemoveSelectedCollisionPolygon")
                    && text.Contains("PointInPolygon"),
                "MapEditorTool map preview can select, vertex-drag, and remove collision layout polygons in memory.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-layout-polygon-transform", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("BeginCollisionPolygonTransform")
                    && text.Contains("ApplyCollisionPolygonTransformDrag")
                    && text.Contains("ApplyCollisionPolygonMove")
                    && text.Contains("ApplyCollisionPolygonRotate")
                    && text.Contains("ApplyCollisionPolygonScale")
                    && text.Contains("DrawCollisionPolygonTransformGizmo")
                    && text.Contains("CollisionPolygonTransformDrag"),
                "MapEditorTool map preview can move, rotate, and scale selected collision layout polygons in memory.");
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-collision-edit-snapshots", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("BeforeLayout")
                    && text.Contains("AfterLayout")
                    && text.Contains("CloneCollisionLayoutData")
                    && text.Contains("CollisionLayoutEditedEventArgs")
                    && text.Contains("CollisionLayoutPolygonEditedEventArgs"),
                "MapEditorTool map preview publishes before/after collision layout snapshots for UI undo handling.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-consumes-layout-polygon-edit", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasCollisionLayoutPolygonSelected")
                    && text.Contains("MapPreviewCanvasCollisionLayoutPolygonEdited")
                    && text.Contains("Collision polygon")
                    && text.Contains("Use Save to write the collision file"),
                "MapEditorTool UI consumes collision layout polygon edits, keeps the current overlay hot, and defers disk writes to the Save collision action.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-collision-layout-undo-redo", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("UndoManager")
                    && text.Contains("CollisionLayoutUndoAction")
                    && text.Contains("UndoLastAction")
                    && text.Contains("RedoLastAction")
                    && text.Contains("PushCollisionLayoutUndo")
                    && text.Contains("ApplyCollisionLayoutUndoSnapshot"),
                "MapEditorTool UI can undo and redo current in-memory collision layout edits without writing external files.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-global-undo-redo-shortcuts", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("protected override bool ProcessCmdKey")
                    && text.Contains("Keys.Control | Keys.Z")
                    && text.Contains("Keys.Control | Keys.Y")
                    && text.Contains("UndoLastAction")
                    && text.Contains("RedoLastAction"),
                "MapEditorTool captures Ctrl+Z and Ctrl+Y at the form level so undo/redo still work when child controls own focus.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-node-position-undo-redo", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("NodePositionUndoAction")
                    && text.Contains("PushNodePositionUndo")
                    && text.Contains("ApplyNodePositionUndoSnapshot")
                    && text.Contains("PatchNodePosition")
                    && text.Contains("Scene file updated"),
                "MapEditorTool UI can undo and redo portal/entity position edits while patching the scene file through ScenePatchExecutor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-tileset-collision-undo-redo", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("TileCollisionUndoAction")
                    && text.Contains("PushTileCollisionUndo")
                    && text.Contains("CaptureTileCollisionAlternatives")
                    && text.Contains("ApplyTileCollisionUndoSnapshot")
                    && text.Contains("TileSet and scene files restored"),
                "MapEditorTool UI can undo and redo TileSet collision edits while restoring scene/model alternatives.");
            AddTextCheck(checks, godotRoot, "mapeditortool-executor-tileset-collision-snapshots", "GodotTools/MapEditorTool/MapEditorTool/Executor/TileCollision/TileCollisionSnapshotExecutor.cs",
                text => text.Contains("TileCollisionSnapshotExecutor")
                    && text.Contains("CaptureFiles")
                    && text.Contains("RestoreFiles")
                    && text.Contains("File.WriteAllText")
                    && text.Contains("ResolveGodotResourcePath"),
                "MapEditorTool TileCollision executor owns external file snapshots used by TileSet collision undo/redo.");
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
            AddTextCheck(checks, godotRoot, "mapeditortool-map-preview-entity-markers", "GodotTools/MapEditorTool/MapEditorTool/UI/MapPreviewCanvas.cs",
                text => text.Contains("DrawEntities")
                    && text.Contains("HitTestEntity")
                    && text.Contains("HitTestDraggableMarker")
                    && text.Contains("EntityMoveCommitted")
                    && text.Contains("PlacedEntity"),
                "MapEditorTool map preview can draw imported entities and treat them as draggable scene markers.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-commits-entity-position", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("MapPreviewCanvasEntityMoveCommitted")
                    && text.Contains("PatchNodePosition")
                    && text.Contains("ScenePatchExecutor")
                    && text.Contains("ResolveGodotResourcePath")
                    && text.Contains("Entity moved"),
                "MapEditorTool UI commits dragged entity positions through ScenePatchExecutor.");
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
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-portal-target-undo-redo", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("PortalTargetUndoAction")
                    && text.Contains("PushPortalTargetUndo")
                    && text.Contains("ApplyPortalTargetUndoSnapshot")
                    && text.Contains("Scene portal target updated")
                    && text.Contains("PortalTargetSnapshot"),
                "MapEditorTool UI can undo and redo portal target changes while writing scene portal target data through the executor.");
            AddTextCheck(checks, godotRoot, "mapeditortool-executor-portal-animation-properties", "GodotTools/MapEditorTool/MapEditorTool/Executor/PortalEditing/PortalEditingExecutor.cs",
                text => text.Contains("AnimationVideoPath")
                    && text.Contains("AnimationFrameCount")
                    && text.Contains("ImportPortalVideoAndPatchScene")
                    && text.Contains("AnimationFramesDir")
                    && text.Contains("PatchPortalAnimation(")
                    && text.Contains("ComputePortalAnimFps")
                    && text.Contains("AnimationFps")
                    && text.Contains("AnimationDurationSec")
                    && text.Contains("PatchPortalAnimationSettings")
                    && text.Contains("KeyoutTolerance")
                    && text.Contains("ReapplyKeyout"),
                "MapEditorTool PortalEditingExecutor writes portal animation video imports, manual frame directories, settings, and keyout changes through Executor-owned side effects.");
            AddTextCheck(checks, godotRoot, "mapeditortool-executor-portal-animation-cli-extract", "GodotTools/MapEditorTool/MapEditorTool/Executor/PortalAnimation/PortalAnimationExecutor.cs",
                text => text.Contains("ExtractPortalAnimationFrames")
                    && text.Contains("ResolveBundledFfmpegPath")
                    && text.Contains("KeyOutBlackBackgroundInDir")
                    && text.Contains("OutputDirectoryResPath"),
                "MapEditorTool PortalAnimationExecutor exposes the non-interactive portal animation frame extractor used by CLI.");
            AddTextCheck(checks, godotRoot, "mapeditortool-viewmodel-link-navigation-state", "GodotTools/MapEditorTool/MapEditorTool/ViewModel/MapEditorShellViewModel.cs",
                text => text.Contains("SelectMapById")
                    && text.Contains("SelectLink(MapLink link)")
                    && text.Contains("ReferenceEquals(item, link)"),
                "MapEditorTool ViewModel exposes pure selection methods for links preview navigation.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-manual-link-editing", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("context.links.add")
                    && text.Contains("context.links.delete")
                    && text.Contains("AddLink")
                    && text.Contains("DeleteSelectedLink")
                    && text.Contains("NormalizeMapId(fromMap)")
                    && text.Contains("RemoveSelectedLink"),
                "MapEditorTool UI can manually add and delete project links from the links list context menu.");
            AddTextCheck(checks, godotRoot, "mapeditortool-viewmodel-manual-link-editing", "GodotTools/MapEditorTool/MapEditorTool/ViewModel/MapEditorShellViewModel.cs",
                text => text.Contains("AddLink(MapLink link)")
                    && text.Contains("RemoveSelectedLink")
                    && text.Contains("Added link")
                    && text.Contains("Deleted link")
                    && text.Contains("ProjectDirty"),
                "MapEditorTool ViewModel owns pure project link add/remove state updates.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-link-property-editing", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("linkPropertyGrid.PropertyValueChanged += LinkPropertyGridPropertyValueChanged")
                    && text.Contains("linkPropertyGrid.SelectedObject = _viewModel.SelectedLink")
                    && text.Contains("LinkPropertyGridPropertyValueChanged")
                    && text.Contains("MarkSelectedLinkEdited"),
                "MapEditorTool link property grid edits the selected MapLink object and routes pure dirty-state updates through the ViewModel.");
            AddTextCheck(checks, godotRoot, "mapeditortool-viewmodel-link-property-editing", "GodotTools/MapEditorTool/MapEditorTool/ViewModel/MapEditorShellViewModel.cs",
                text => text.Contains("MarkSelectedLinkEdited")
                    && text.Contains("Selected link updated")
                    && text.Contains("RefreshProjectSnapshot")
                    && text.Contains("ProjectDirty"),
                "MapEditorTool ViewModel can mark selected link property edits as project-dirty state changes.");
            AddTextCheck(checks, godotRoot, "mapeditortool-model-propertygrid-metadata", "GodotTools/MapEditorTool/MapEditorTool/Models/MapProject.cs",
                text => text.Contains("[DisplayName(\"Scene Path\")]")
                    && text.Contains("[Category(\"Textures\")]")
                    && text.Contains("[Category(\"Collision\")]")
                    && text.Contains("[Description(\"Node path inside the .tscn file")
                    && text.Contains("[DisplayName(\"Target Map\")]")
                    && text.Contains("[DisplayName(\"Map ID\")]"),
                "MapEditorTool model exposes English PropertyGrid display names, categories, and descriptions for migrated editor objects.");
            AddTextCheck(checks, godotRoot, "mapeditortool-ui-list-and-grid-tooltips", "GodotTools/MapEditorTool/MapEditorTool/UI/Form1.cs",
                text => text.Contains("ToolTip")
                    && text.Contains("MapsListMouseMove")
                    && text.Contains("LinksListMouseMove")
                    && text.Contains("PropertyGridSelectedGridItemChanged")
                    && text.Contains("ShowPropertyGridToolTip"),
                "MapEditorTool UI shows map/link list hover details and property grid selected-item help.");
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
