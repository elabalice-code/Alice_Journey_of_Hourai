# EventStudio

EventStudio edits event and quest-flow data for Alice Journey of Hourai.

## Paths

- Source project: `GodotTools/EventStudio/EventStudio/EventStudio.csproj`
- Build output: `GodotTools-Build/EventStudio/net8.0-windows`
- Interactive executable: `GodotTools-Build/EventStudio/net8.0-windows/EventStudio.exe`

## Data

- Editor project files: `*.events.json`
- Runtime export files: `*.runtime.events.json`

Event groups are display-only folders. Events are the executable files inside those folders. Moving an event or group in the UI changes only folder ownership metadata; event trigger/output logic is kept on the event itself.

## Logs

EventStudio writes timestamped local logs beside the built executable:

```text
GodotTools-Build/EventStudio/net8.0-windows/logs/EventStudio_yyyyMMdd_HHmmss_fff.log
GodotTools-Build/EventStudio/net8.0-windows/logs/EventStudio.latest.log
```

Use `File > Open Logs Folder` in the EventStudio UI to open that folder. Unexpected UI-thread errors, fatal exceptions, Replay bridge errors, project load/save/export actions, and key drag/drop edits are written there with local timestamps.

## Validation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools.ps1 run event-studio self-test -NoBuild
dotnet build .\GodotTools\EventStudio\EventStudio\EventStudio.csproj -c Release
```

`EventStudio --agent-self-test` verifies:

- event group boundaries;
- event field JSON roundtrip;
- runtime state export;
- event group parent-cycle validation;
- log file creation.

## Runtime Boundary

EventStudio produces event configuration and runtime event-state data. It does not directly mutate scenes. Runtime systems consume exported state, signals, and event actions to perform scene, dialogue, combat, or inventory effects.
