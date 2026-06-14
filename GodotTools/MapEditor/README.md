# MapEditor

MapEditor is the external map editing tool for the Godot project. It has a WinForms UI, plus CLI commands that Coder/Agent can run without opening the UI.

## Agent Commands

From the project root:

```powershell
.\tools.ps1 map status
.\tools.ps1 map status --summary
.\tools.ps1 map portal-review --summary
.\tools.ps1 map runtime-verify --summary
.\tools.ps1 map ux-audit --summary
.\tools.ps1 map ux-walkthrough --summary --out BuildLogs\map_ux_walkthrough.json
.\tools.ps1 map ux-review --summary
.\tools.ps1 map import --summary
.\tools.ps1 map validate --summary
.\tools.ps1 run map-editor self-test

dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- status --godotRoot .
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- status --godotRoot . --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- portal-review --godotRoot . --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- runtime-verify --godotRoot . --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- ux-audit --godotRoot . --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- ux-walkthrough --godotRoot . --summary --out BuildLogs\map_ux_walkthrough.json
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- ux-review --godotRoot . --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- import --godotRoot . --out BuildLogs\map_project.json --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- validate --godotRoot . --in BuildLogs\map_project.json --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- agent-self-test --godotRoot .
```

`status` scans the current Godot maps through the existing importer and prints JSON with map, link, portal, tile-layer, entity, missing-scene, maps-without-portals, and missing-link-target counts. Add `--summary` for a concise human-readable report while keeping JSON as the default Agent format. It does not write project files.

`portal-review` scans the same imported map graph and focuses on portal coverage: maps without portals and portals whose target map is missing. It adds conservative classification buckets, confidence, reasons, and recommendations for maps without portals so human review can separate terminal, scripted/dynamic, utility, and possible map-graph gap cases. Add `--summary` for a concise review list. It does not write project files.

`runtime-verify` is a read-only static game-effect verifier. It checks that imported portal targets resolve to imported map scenes and that `CoreEngine` runtime scripts expose the expected portal-to-room-loading chain (`Portal.gd` target_map, `TYPE_LOAD_ROOM_REQUEST`, `RoomFlowActor.gd`, and `load_room`). Add `--summary` for a human-readable Testor report. It does not execute a live player transition.

`ux-audit` is a read-only static UX audit. It checks discoverability, feedback, recovery, and Agent mirror surfaces in the MapEditor UI source. Add `--summary` for a human-readable Testor report. It does not replace a human click-through review.

`ux-walkthrough` writes the human live-review checklist. `ux-review` records or verifies the result file and exits nonzero until the UX gate has reviewer, overall result, and per-step pass/partial/fail evidence.

`import` scans the current Godot maps and writes a MapEditor project JSON file. `tools.ps1 map import` defaults to `BuildLogs/map_project.json`; add `--summary` for a concise report with map, link, portal, tile-layer, and entity counts.

`validate` compares a MapEditor project JSON file with a fresh Godot map scan. `tools.ps1 map validate` defaults to `BuildLogs/map_project.json`; add `--summary` for a concise report of missing and extra scene paths.

`agent-self-test` checks model serialization and, when `--godotRoot` is supplied, confirms the project shape and map status scan.

## Editing Commands

```powershell
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- import --godotRoot . --out BuildLogs\map_project.json --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- validate --godotRoot . --in BuildLogs\map_project.json --summary
dotnet run --project GodotTools\MapEditor\MapEditor\MapEditor.csproj -c Release -- patchpos --godotRoot . --scene res://CoreEngine/Maps/DemoMap.tscn --nodePath SomeNode --x 0 --y 0
```

Only `status`, `portal-review`, `runtime-verify`, `ux-audit`, `ux-walkthrough`, `ux-review` without `--out`, `import`, `validate`, and `agent-self-test` are read-only. `ux-review --out` writes only the review-result artifact. Commands such as `patchpos` and `portalanim` can write project files or generated assets.
