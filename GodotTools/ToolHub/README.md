# ToolHub

ToolHub is the manifest-facing CLI for `GodotTools/tools.json`.

It gives Coder/Agent a stable way to inspect available tools without reading every project file.

## Commands

From the project root, prefer the short wrapper:

```powershell
.\tools.ps1
.\tools.ps1 handoff
.\tools.ps1 handoff --summary
.\tools.ps1 dump-index --summary
.\tools.ps1 closure-gates --summary
.\tools.ps1 mutation-plan --summary --out BuildLogs\mutation_plan.json --domain map --intent "draft reviewed Corridor map portal and resource-classification edit workflow" --writes "res://CoreEngine/Maps/Corridor.tscn;BuildLogs/map_project_after.json" --before-dump BuildLogs\map_project_before.json --after-dump BuildLogs\map_project_after.json --summary-command "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map validate --summary -NoBuild" --verifier "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map runtime-verify --summary -NoBuild" --ux "MapEditor live click-through covering Corridor inspection edit preview validation and recovery before accepting the edit" --recovery "restore BuildLogs map_project_before snapshot, revert Corridor.tscn, then rerun map validate and runtime-verify"
.\tools.ps1 mutation-plan --summary --in BuildLogs\mutation_plan.json
.\tools.ps1 mutation-plan verify --summary --dir BuildLogs
.\tools.ps1 next-actions --summary
.\tools.ps1 status
.\tools.ps1 map status
.\tools.ps1 map status --summary
.\tools.ps1 map portal-review --summary
.\tools.ps1 map runtime-verify --summary
.\tools.ps1 map ux-audit --summary
.\tools.ps1 map ux-walkthrough --summary --out BuildLogs\map_ux_walkthrough.json
.\tools.ps1 map ux-review --summary
.\tools.ps1 map import --summary
.\tools.ps1 map validate --summary
.\tools.ps1 show resource-config
.\tools.ps1 run resource-config self-test
.\tools.ps1 resource refresh
.\tools.ps1 resource audit --out BuildLogs\resource_audit.json
.\tools.ps1 resource plan --out BuildLogs\resource_plan.json
.\tools.ps1 resource decide --id resource-plan-0001 --decision defer --note "needs owner review"
.\tools.ps1 resource apply --out BuildLogs\resource_apply_preview.json
.\tools.ps1 resource apply --execute --out BuildLogs\resource_apply_preview.json --approved-out BuildLogs\resource_approved_dependencies.json --cleanup-out BuildLogs\resource_cleanup_candidates.json
.\tools.ps1 resource pending --limit 10
.\tools.ps1 resource pending --summary --limit 10
.\tools.ps1 resource pending --summary --severity warning --type review-external-reference --limit 10
.\tools.ps1 resource pending --summary --query CustomRunner --limit 10
.\tools.ps1 resource pending --summary --query CoreEngine/Maps --limit 20
.\tools.ps1 resource pending --commands --query CustomRunner --limit 10
.\tools.ps1 resource map-review --summary --limit 20
.\tools.ps1 resource status
.\tools.ps1 resource status --summary
.\tools.ps1 resource verify-outputs
.\tools.ps1 resource verify-outputs --summary
.\tools.ps1 resource find --query Game --kind scene
.\tools.ps1 resource show --path res://CoreEngine/Game.tscn
.\tools.ps1 run-all self-test
.\tools.ps1 doctor
.\tools.ps1 validate-manifest
```

Direct `dotnet run` commands also work:

```powershell
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- list --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- handoff --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- handoff --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- dump-index --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- closure-gates --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- mutation-plan --summary --out BuildLogs\mutation_plan.json --domain map --intent "draft reviewed Corridor map portal and resource-classification edit workflow" --writes "res://CoreEngine/Maps/Corridor.tscn;BuildLogs/map_project_after.json" --before-dump BuildLogs\map_project_before.json --after-dump BuildLogs\map_project_after.json --summary-command "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map validate --summary -NoBuild" --verifier "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map runtime-verify --summary -NoBuild" --ux "MapEditor live click-through covering Corridor inspection edit preview validation and recovery before accepting the edit" --recovery "restore BuildLogs map_project_before snapshot, revert Corridor.tscn, then rerun map validate and runtime-verify" --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- mutation-plan --summary --in BuildLogs\mutation_plan.json --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- mutation-plan verify --summary --dir BuildLogs --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- next-actions --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- status --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map status --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map status --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map portal-review --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map runtime-verify --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map ux-audit --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map ux-walkthrough --summary --out BuildLogs\map_ux_walkthrough.json --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map ux-review --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map import --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map validate --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- show resource-config --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- validate-manifest --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- run resource-config self-test --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource audit --out BuildLogs\resource_audit.json --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource plan --out BuildLogs\resource_plan.json --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource decide --id resource-plan-0001 --decision defer --note "needs owner review" --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource apply --out BuildLogs\resource_apply_preview.json --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource apply --execute --out BuildLogs\resource_apply_preview.json --approved-out BuildLogs\resource_approved_dependencies.json --cleanup-out BuildLogs\resource_cleanup_candidates.json --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource pending --limit 10 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource pending --summary --limit 10 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource pending --summary --severity warning --type review-external-reference --limit 10 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource pending --summary --query CustomRunner --limit 10 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource pending --summary --query CoreEngine/Maps --limit 20 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource pending --commands --query CustomRunner --limit 10 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource map-review --summary --limit 20 --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource status --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource status --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource verify-outputs --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource verify-outputs --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- resource find --query Game --kind scene --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- run-all self-test --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- doctor --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- --agent-self-test
```

ToolHub is intentionally small. It validates, explains, and dispatches tool metadata; individual tools still own their own build and self-test behavior.

- `status`: print a read-only JSON summary of manifest validity, tool categories, project paths, build scripts, output directory file counts, output sizes, output timestamps, and Agent command availability.
- `handoff`: print one read-only JSON report containing ToolHub status, dump index, MapEditor map status, MapEditor portal review, MapEditor runtime verification, MapEditor UX audit, ResourceConfig workflow status, and ResourceConfig output verification. Add `--summary` for a concise human-readable Arch/Coder handoff while keeping JSON as the default Agent format.
- `dump-index`: print the canonical human/Agent-facing dump index for current map, resource, review, approval-output, and validation-log artifacts under `BuildLogs/`, including the fused `map_scene_resource_review.json` queue and persisted `mutation_plan.json` review artifact. Add `--summary` for the human-readable checklist with refresh and verify commands.
- `closure-gates`: print a read-only closure-gate report for the three external-tool acceptance gates: human-friendly dumps, game-effect verification, and human UX/recovery. Add `--summary` for the human-readable Testor/Arch checklist while keeping JSON as the default Agent format.
- `mutation-plan`: print a read-only pre-write design checklist for a future mutating workflow. It requires domain, intent, write targets, before/after dumps, human summary command, game-effect verifier, UX path, recovery path, and concrete non-template values before returning success; placeholders such as `<...>`, `...`, `describe intended edit`, `human review path`, and `rollback or reject path` keep the plan out of design-review-ready state. Add `--summary` for the human-readable Coder/Testor review note, `--out BuildLogs\mutation_plan.json` to persist the plan as a dump-indexed review artifact, `--in BuildLogs\mutation_plan.json` to revalidate a stored plan, and `mutation-plan verify --summary --dir BuildLogs` to batch-check stored `mutation_plan*.json` artifacts with per-plan check and missing counts while keeping JSON as the default Agent format.
- `next-actions`: print a read-only JSON recommendation report derived from ToolHub handoff plus existing BuildLogs. Add `--summary` for a concise review checklist with suggested commands, the persisted `mutation-plan --out BuildLogs\mutation_plan.json` workflow, MapEditor portal classifications, ResourceConfig severity/type review queues, map-scene resource review overlap counts, and sample undecided resource plan actions. Resource samples that point at classified map scenes include MapEditor portal hints. The JSON includes `mapSceneResourceReviewCount` and `mapSceneResourceReview` so Agent/Testor can separate total map-scene review actions from unique scene count.

The `map` shortcut is a thin Agent-facing route into MapEditor:

- `map status`: print a read-only JSON summary of current Godot maps, links, portals, tile layers, entities, and map graph warnings. Add `--summary` for a concise human-readable report while keeping JSON as the default Agent format.
- `map portal-review`: print a read-only portal coverage review focused on maps without portals and portals with missing target maps. For maps without portals, it includes conservative classification buckets, confidence, reasons, and recommendations so human review can separate terminal, scripted/dynamic, utility, and possible map-graph gap cases. Add `--summary` for a concise human-readable review list while keeping JSON as the default Agent format.
- `map runtime-verify`: run a read-only static game-effect verifier for the map runtime wiring. It checks imported portal targets, runtime entry rooms, and the `Portal.gd` -> `RoomFlowActor.gd` -> `load_room` consumption chain. Add `--summary` for a concise Testor report while keeping JSON as the default Agent format.
- `map ux-audit`: run a read-only static UX audit for MapEditor discoverability, feedback, recovery, and Agent mirror surfaces. Add `--summary` for a concise Testor report while keeping JSON as the default Agent format. It is not a substitute for a human click-through review.
- `map ux-walkthrough`: print a human live-review checklist for MapEditor launch, import, inspect, edit preview, save/review, validation, and recovery flows. Add `--out BuildLogs\map_ux_walkthrough.json` to persist the checklist locally as a review artifact; the command prepares the human click-through, it does not prove the click-through has been completed.
- `map ux-review`: record or verify the human walkthrough result. By default it reads `BuildLogs\map_ux_review_result.json` and exits nonzero until reviewer, overall result, and per-step results prove the UX gate is accepted. To record a completed pass, provide `--in BuildLogs\map_ux_walkthrough.json --out BuildLogs\map_ux_review_result.json --reviewer <name> --result pass --step-results "launch=pass;import=pass;inspect=pass;edit-preview=pass;save-review=pass;error-recovery=pass;agent-mirror=pass" --summary`.
- `map import`: scan current Godot maps and write a MapEditor project JSON file. Defaults to `BuildLogs/map_project.json`; add `--summary` for a concise import report.
- `map validate`: compare a MapEditor project JSON file with a fresh Godot map scan. Defaults to `BuildLogs/map_project.json`; add `--summary` for missing and extra scene path counts.

The `resource` shortcut is a thin Agent-facing route into ResourceConfig for frequent resource graph operations:

- `resource refresh`: rebuild `BuildLogs/resource_index.json`;
- `resource audit`: write or print a read-only resource cleanup/configuration report;
- `resource plan`: write or print read-only candidate cleanup/fix actions;
- `resource decide`: record `accept`, `defer`, or `reject` for one plan action in `BuildLogs/resource_decisions.json`;
- `resource apply`: write a preview of accepted plan actions; with `--execute`, write approved external dependencies and cleanup candidates under `BuildLogs/`;
- `resource pending`: read the current plan and decision ledger, then list undecided review actions with severity/type review buckets, suggested queue commands, and suggested decision commands. Add `--summary` for a concise human-readable review list, `--commands` for only suggested decision commands, and use `--severity`, `--type`, or `--query` to focus the list;
- `resource map-review`: read the current resource plan and MapEditor portal review, then show undecided `res://CoreEngine/Maps/*.tscn` resource actions with portal coverage hints. Add `--summary` for a concise human-readable review list, `--limit <n>` to control samples, and `--out BuildLogs\map_scene_resource_review.json` when refreshing the dump-indexed review artifact;
- `resource status`: summarize existing resource workflow BuildLogs outputs. Add `--summary` for a concise human-readable workflow report while keeping JSON as the default Agent format;
- `resource verify-outputs`: verify approved dependency and cleanup-candidate outputs against the plan, decisions, and apply preview. Add `--summary` for a concise human-readable gate report while keeping JSON as the default Agent format;
- `resource find`: search the resource index by path text, kind, extension, and limit;
- `resource show`: print one indexed `res://` resource entry as JSON.

## Manifest Validation Scope

`validate-manifest` checks required metadata, supported categories, safe project-relative paths, existing project files, supported `dotnet-run` / `exe-run` command kinds, packaged executable paths, known `{projectRoot}` / `{toolsRoot}` / `{outputRoot}` tokens, and `requiresProjectRoot` command wiring.
