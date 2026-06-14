# GodotTools

External tools for Alice Journey of Hourai live here. The source of truth for tool discovery is:

`GodotTools/tools.json`

## Tool Contract

Every tool entry should include:

- `id`: stable machine-readable id.
- `name`: display name used by scripts and logs.
- `category`: `map`, `resource`, `event`, `replay`, `validation`, or `utility`.
- `purpose`: one sentence describing what the tool does.
- `project`: `.csproj` path relative to the Godot project root.
- `output`: expected build output path relative to the Godot project root.
- `launch`: optional interactive command metadata.
- `selfTest`: non-interactive Agent validation command.
- `requiresProjectRoot`: whether the tool needs the Godot project root.
- `notes`: short operating note for Coder/Agent.

## Agent Rule

New tools are not integrated until their Release build succeeds and their `selfTest` exits 0 through:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Mode Tools
```

The default project build also runs the tool build and self-tests unless `-SkipTools` or `-SkipToolSelfTest` is passed.

## ToolHub

Use ToolHub as the first Agent/Coder entry point for tool discovery:

```powershell
.\tools.ps1
.\tools.ps1 handoff
.\tools.ps1 handoff --summary
.\tools.ps1 dump-index --summary
.\tools.ps1 closure-gates --summary
.\tools.ps1 mutation-plan --summary --out BuildLogs\mutation_plan.json --domain map --intent "describe intended edit" --writes "res://CoreEngine/..." --before-dump BuildLogs\before.json --after-dump BuildLogs\after.json --summary-command "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map validate --summary -NoBuild" --verifier "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map runtime-verify --summary -NoBuild" --ux "human review path" --recovery "rollback or reject path"
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
.\tools.ps1 map import --summary
.\tools.ps1 map validate --summary
.\tools.ps1 show resource-config
.\tools.ps1 run resource-config self-test
.\tools.ps1 run resource-config launch
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
.\tools.ps1 resource pending --commands --query CustomRunner --limit 10
.\tools.ps1 resource status
.\tools.ps1 resource status --summary
.\tools.ps1 resource verify-outputs
.\tools.ps1 resource verify-outputs --summary
.\tools.ps1 resource find --query Game --kind scene
.\tools.ps1 resource show --path res://CoreEngine/Game.tscn
.\tools.ps1 run-all self-test
.\tools.ps1 doctor
.\tools.ps1 validate-manifest

dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- list --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- handoff --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- handoff --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- dump-index --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- closure-gates --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- mutation-plan --summary --out BuildLogs\mutation_plan.json --domain map --intent "describe intended edit" --writes "res://CoreEngine/..." --before-dump BuildLogs\before.json --after-dump BuildLogs\after.json --summary-command "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map validate --summary -NoBuild" --verifier "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 map runtime-verify --summary -NoBuild" --ux "human review path" --recovery "rollback or reject path" --godotRoot .
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
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map import --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- map validate --summary --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- validate-manifest --godotRoot .
dotnet run --project GodotTools\ToolHub\ToolHub\ToolHub.csproj -c Release -- run resource-config self-test --godotRoot .
```

`tools.ps1 status` prints a read-only ToolHub summary of manifest validity, categories, project paths, build scripts, output directory file counts, output sizes, output timestamps, and Agent command availability.

`tools.ps1 handoff` prints one read-only JSON handoff report containing ToolHub status, MapEditor map status, MapEditor portal review, MapEditor runtime verification, ResourceConfig workflow status, and ResourceConfig output verification. `tools.ps1 handoff --summary` prints a concise human-readable Arch/Coder handoff from the same checks.

`tools.ps1 dump-index` prints the canonical human/Agent-facing dump index for current map, resource, review, approval-output, and validation-log artifacts under `BuildLogs/`, including the fused `map_scene_resource_review.json` queue and persisted `mutation_plan.json` review artifact. Add `--summary` for the human-readable checklist with refresh and verify commands.

`tools.ps1 closure-gates` prints the three external-tool acceptance gates as JSON: human-friendly dumps, game-effect verification, and human UX/recovery. Add `--summary` for the human-readable Arch/Testor checklist. The command reports the current read-only/generated baseline separately from future mutating workflow readiness.

`tools.ps1 mutation-plan` prints a read-only pre-write design checklist for a future mutating workflow. It exits nonzero until the design names domain, intent, write targets, before/after dumps, summary command, verifier, UX path, and recovery path. Add `--out BuildLogs\mutation_plan.json` to persist the plan as a dump-indexed review artifact, use `--in BuildLogs\mutation_plan.json --summary` to revalidate a stored plan without rewriting it, and use `mutation-plan verify --summary --dir BuildLogs` to batch-check stored `mutation_plan*.json` artifacts before Coder writes mutating code.

`tools.ps1 next-actions` prints a read-only recommendation report derived from the handoff checks and current BuildLogs. `tools.ps1 next-actions --summary` is the quick Arch/Coder checklist for what to review next; it includes the persisted `mutation-plan --out BuildLogs\mutation_plan.json` recommendation, MapEditor portal classifications, ResourceConfig severity/type review queues with direct follow-up commands, and map portal hints on resource review samples that point at classified map scenes.

`map status` scans the current Godot maps through MapEditor and prints a read-only JSON summary for Agent/Coder handoff. Add `--summary` for a concise human-readable report while keeping JSON as the default Agent format.

`map portal-review` scans the current Godot maps through MapEditor and prints a read-only portal coverage review for maps without portals and portals with missing target maps. For maps without portals, it includes conservative classification buckets, confidence, reasons, and recommendations so human review can separate terminal, scripted/dynamic, utility, and possible map-graph gap cases. Add `--summary` for a concise human-readable review list while keeping JSON as the default Agent format.

`map runtime-verify` runs a read-only static game-effect verifier for the map runtime wiring. It checks imported portal targets, runtime entry rooms, and the `Portal.gd` -> `RoomFlowActor.gd` -> `load_room` consumption chain. Add `--summary` for a concise Testor report while keeping JSON as the default Agent format.

`map ux-audit` runs a read-only static UX audit for MapEditor discoverability, feedback, recovery, and Agent mirror surfaces. Add `--summary` for a concise Testor report while keeping JSON as the default Agent format. It is not a substitute for a human click-through review.

`map ux-walkthrough` prints a human live-review checklist for MapEditor launch, import, inspect, edit preview, save/review, validation, and recovery flows. Add `--out BuildLogs\map_ux_walkthrough.json` to persist the checklist locally as a review artifact; the command prepares the human click-through, it does not prove the click-through has been completed.

`map import` scans the current Godot maps and writes a MapEditor project JSON file. `tools.ps1 map import` defaults to `BuildLogs/map_project.json`; add `--summary` for a concise import report. `map validate` compares that file with a fresh Godot map scan; add `--summary` for missing and extra scene path counts.

`resource-config` launch refreshes `BuildLogs/resource_index.json`, an editor-friendly resource graph with incoming and outgoing `res://` links. Prefer the shorter `resource refresh/audit/plan/decide/apply/pending/status/verify-outputs/find/show` commands for day-to-day Agent/Coder resource lookup and cleanup planning. `resource refresh` updates `BuildLogs/resource_index.json`, `BuildLogs/resource_audit.json`, and `BuildLogs/resource_plan.json`. `resource decide` writes review decisions to `BuildLogs/resource_decisions.json`. `resource apply` writes a preview to `BuildLogs/resource_apply_preview.json`; with `--execute`, it writes approved external dependencies and cleanup candidates under `BuildLogs/`. `resource pending` lists undecided plan actions from the current plan and decision ledger without scanning or writing resources; use `resource pending --summary` for human review with severity/type review buckets and suggested queue commands, `--commands` for command-only output, and `--severity` / `--type` / `--query` to focus the list. `resource status` summarizes the current BuildLogs workflow state; add `--summary` for a concise human-readable workflow report. `resource verify-outputs` checks those review outputs against the plan, decisions, and apply preview before any future resource-mutating action; add `--summary` for the human-readable gate report while keeping JSON as the default Agent format.

`tools.ps1` forwards tool arguments manually so options such as `--out`, `--id`, and `--decision` are passed through to ToolHub instead of being interpreted as PowerShell script parameters.

## Token Expansion

`tools.json` command args may use:

- `{projectRoot}`: absolute path to the Godot project root.
- `{toolsRoot}`: absolute path to `GodotTools`.
- `{outputRoot}`: absolute path to `GodotTools-Build`.

## Manifest Validation

`ToolHub validate-manifest` enforces the current tool contract:

- tool ids use lowercase kebab-case, such as `resource-config`;
- category is one of `map`, `resource`, `event`, `replay`, `validation`, or `utility`;
- `toolsRoot`, `outputRoot`, `project`, and `output` are project-relative paths and must not escape the project root;
- `project` points to an existing `.csproj`;
- `launch` and `selfTest` commands use `dotnet-run`;
- command args do not contain empty values or unknown `{tokens}`;
- tools marked `requiresProjectRoot` include `{projectRoot}` in `launch` or `selfTest` args;
- `notes` are required so Agents have an operating hint.
