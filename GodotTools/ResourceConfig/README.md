# ResourceConfig

ResourceConfig is a small CLI for project resource discovery and validation.

Current scope:

- scan Godot project files into a resource manifest;
- classify scenes, resources, images, translations, imports, scripts, and JSON;
- extract `res://...` references from text resources;
- report missing references;
- export a JSON manifest for future editors or Agent workflows.
- export a resource index with incoming/outgoing reference links for editor tools.
- export a read-only resource audit report for configuration cleanup decisions.
- export a read-only resource plan with reviewable cleanup/fix candidate actions.
- record explicit decisions for resource plan actions before any future apply/fix step.
- export a dry-run apply preview from accepted plan decisions.
- list pending resource plan reviews from the current plan and decision ledger.
- summarize the current resource workflow BuildLogs state.
- verify approved dependency and cleanup-candidate outputs against the plan, decisions, and apply preview.
- query one resource entry from the resource index as JSON.
- find resource entries by path text, kind, or extension.

## Commands

```powershell
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- summary --godotRoot . --scope core
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- validate --godotRoot . --scope core
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- export-manifest --godotRoot . --scope core --out BuildLogs\resource_manifest.json
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- export-index --godotRoot . --scope core --out BuildLogs\resource_index.json
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- audit --godotRoot . --scope core --out BuildLogs\resource_audit.json
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- plan --godotRoot . --scope core --out BuildLogs\resource_plan.json
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- decide --godotRoot . --id resource-plan-0001 --decision defer --note "needs owner review"
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- apply --godotRoot . --out BuildLogs\resource_apply_preview.json
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- apply --godotRoot . --execute --out BuildLogs\resource_apply_preview.json --approved-out BuildLogs\resource_approved_dependencies.json --cleanup-out BuildLogs\resource_cleanup_candidates.json
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- pending --godotRoot . --limit 10
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- pending --godotRoot . --summary --limit 10
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- pending --godotRoot . --summary --severity warning --type review-external-reference --limit 10
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- pending --godotRoot . --summary --query CustomRunner --limit 10
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- pending --godotRoot . --commands --query CustomRunner --limit 10
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- status --godotRoot .
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- status --godotRoot . --summary
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- verify-outputs --godotRoot .
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- verify-outputs --godotRoot . --summary
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- show --godotRoot . --path res://CoreEngine/Game.tscn
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- find --godotRoot . --query Game --kind scene --limit 10
dotnet run --project GodotTools\ResourceConfig\ResourceConfig\ResourceConfig.csproj -c Release -- --agent-self-test
```

Use `--scope all` when you also want to inspect extension and addon folders.

`export-index` writes editor-friendly JSON entries with:

- resource path, kind, extension, and size;
- outgoing `res://` links with existence flags;
- incoming link source paths;
- missing outgoing reference count.

The manifest launch command for `resource-config` refreshes `BuildLogs/resource_index.json`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 run resource-config launch -NoBuild
```

`show` reads `BuildLogs/resource_index.json` by default and returns one resource entry as JSON. Add `--refresh` to rebuild the index before querying, or `--index <file>` to read another index file.

`find` reads the same index and returns compact JSON search results. Use `--query <text>`, `--kind <kind>`, `--extension <ext>`, and `--limit <n>` to narrow results.

`audit` is a read-only configuration report for future editors and Agent cleanup planning. It reports:

- largest resources;
- references that point outside `CoreEngine/`;
- unreferenced resources, excluding known entry points;
- duplicate file-name groups.

`plan` is also read-only. It turns the manifest/index/audit signals into reviewable candidate actions such as missing-reference resolution, external dependency review, unreferenced-resource review, and duplicate-name review. It does not modify project files.

`decide` records a review decision for one plan action in `BuildLogs/resource_decisions.json`. Supported decisions are `accept`, `defer`, and `reject`. A decision is not an apply step; it is an audit trail for future approved resource edits.

`apply` defaults to dry-run preview. It reads `BuildLogs/resource_plan.json` and `BuildLogs/resource_decisions.json`, lists accepted actions as `wouldApply`, and explains why unaccepted or undecided actions are skipped. It writes `BuildLogs/resource_apply_preview.json`.

`apply --execute` currently supports accepted `review-external-reference` and `review-unreferenced-resource` actions. It writes approved external dependencies to `BuildLogs/resource_approved_dependencies.json` and cleanup candidates to `BuildLogs/resource_cleanup_candidates.json`. It still does not modify Godot project resources.

`pending` is read-only. It reads `BuildLogs/resource_plan.json` and `BuildLogs/resource_decisions.json`, summarizes accepted/deferred/rejected/pending counts, review buckets by severity/type, and the highest-priority undecided actions with suggested `resource decide` commands. Add `--summary` for a concise human-readable review list with suggested queue commands, or `--commands` for only the suggested decision commands. Use `--severity error|warning|info`, `--type <action-type>`, and `--query <text>` to focus the list without changing the underlying plan. It does not scan or write project resources.

`status` reads existing BuildLogs JSON files and returns a compact workflow summary without scanning or writing project resources. Add `--summary` for a concise human-readable workflow report while keeping JSON as the default Agent format.

`verify-outputs` reads the resource plan, decision ledger, apply preview, approved dependencies, and cleanup candidates. It exits 0 only when accepted executable actions line up with the generated review outputs. Add `--summary` for a concise human-readable gate report while keeping JSON as the default Agent format. It is a pre-mutation safety gate and does not scan or modify project resources.

The tool is integrated into `GodotTools/tools.json`, so the normal build gate also validates it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Mode Tools
```
