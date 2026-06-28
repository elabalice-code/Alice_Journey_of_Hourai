# MapEditorTool

MapEditorTool is the new maintainable map-editor baseline for Alice Journey of Hourai.

The current implementation is still a UI shell, but its architecture is already being shaped around a long-lived signal pipeline:

```text
UI producer event
  -> ViewModel signal bus
  -> SignalWeaver state machine
  -> ViewModel consumer snapshot
  -> UI consumes display intent
  -> Executor performs terminal side effects
```

## Build

Use the project build script:

```bat
Build.bat
```

Release output is copied to the shared tool build directory:

```text
../../GodotTools-Build/MapEditorTool/
```

## Layer Contract

### UI

Path:

```text
MapEditorTool/UI/
```

The UI owns WinForms controls and visible dialogs.

Allowed:

- convert control events into simple producer signals;
- read the ViewModel consumer snapshot;
- consume UI display intents such as `DeveloperCommentOpenRequested`;
- show `DeveloperCommentBox`;
- call an Executor after a terminal UI action is complete.

Not allowed:

- decide feature policy that belongs to SignalWeaver;
- write logs or files directly;
- feed terminal UI results back into SignalWeaver when no more state-machine digestion is needed.

### ViewModel

Path:

```text
MapEditorTool/ViewModel/
```

The ViewModel is the signal bus and UI refresh surface.

Main bus:

```text
MapEditorShellViewModel
```

It exposes:

- `ProducerSignals`: all producer signals seen by the bus;
- `LatestProducerSignal`: the newest producer signal;
- `ConsumerSnapshot` / `Snapshot`: current consumer data for UI refresh.

The ViewModel may call SignalWeaver state machines and fold their decisions into the snapshot. It must not show UI dialogs or perform file writes.

### SignalWeaver

Path:

```text
MapEditorTool/SignalWeaver/
```

SignalWeaver is the pure signal-state-machine layer. It receives simple producer facts and emits simple consumer decisions.

It must not reference WinForms controls, dialogs, file IO, or process side effects.

**Architecture**: SignalWeaver is organized as a central cable harness that routes signals to feature modules, each owning one stable rule family.

- **`Main/` — Signal harness host**: The central entry point that receives all producer signals from the ViewModel in a single frame. It dispatches each signal to the relevant feature machines, collects their consumer decisions, and hands the combined result back to the ViewModel to fold into the snapshot. It owns the routing table but holds no feature-specific rules itself.
- **`Featuror/` — Mature feature dispatch**: Each folder under `Featuror/` is one stable, verified feature family. A feature receives only the signal facts it needs, runs a pure state machine, and returns a simple consumer decision (bool, enum, compact key, short status label). Features are clustered by consumer surface: `UI/` for display intent, `MapEditor/MapGraph/` for selection state, `MapEditor/MapCanvas/` for canvas tool mode, `MapEditor/MapProject/` for mutation classification, `Persistence/` for file action intent.
- **`Compozor/` — External pressure inbox**: Signals driven by user-reported UX pain, field issues, or compatibility constraints start here before they earn a stable contract. An empty `Apply()` is normal — it means no external pressure has been formalized yet.
- **`Incubator/` — Internal experiment space**: Proposed signal policies, queue/coalesce ideas, and diagnostic models live here until they prove themselves. An empty `Apply()` is normal here too — it means no experiment has graduated.

Feature lifecycle: external pressure enters through Compozor, internal experiments through Incubator, and only proven rules with stable contracts and verification paths are promoted to Featuror. DeveloperComment was the first feature to complete this journey.

Current active Featuror modules:

```text
SignalWeaver/Featuror/UI/DeveloperComment/     — comment mode + dialog request
SignalWeaver/Featuror/UI/TerminalAction/       — menu/toolbar click classification
SignalWeaver/Featuror/MapEditor/MapGraph/Selection/   — map/link selection decisions
SignalWeaver/Featuror/MapEditor/MapCanvas/ToolMode/   — canvas editor mode state
SignalWeaver/Featuror/MapEditor/MapProject/Mutation/  — add/delete/pin intent classification
SignalWeaver/Featuror/Persistence/Action/      — file/project action intent classification
```

ViewModel boundary: ViewModel calls into SignalWeaver only through the `Main/` harness host. It never reaches into individual Featuror modules directly. The harness returns decisions as plain data — no mutable objects, no control references, no file paths. ViewModel folds those decisions into `MapEditorUiSnapshot` without knowing which feature produced them.

### Executor

Path:

```text
MapEditorTool/Executor/
```

Executors perform terminal side effects after the signal chain no longer needs the result as a producer.

**Design intent**: Executor owns execution — any read or write against objects outside the pure in-memory decision system. This includes files, logs, processes, operating-system resources, project assets, Godot scene files, FFmpeg calls, and any other external target. When a signal no longer needs to feed back into the state machine, the action belongs in Executor.

### Helper

Path:

```text
MapEditorTool/Executor/<FeatureName>/<FeatureName>Helper.cs
```

**Design intent**: Helper does not execute. Helper is for pure calculation — given input data, it returns output data without owning side effects. It must not touch files, logs, processes, or external resources.

A top-level `Helper/` folder is only for routines with real project-wide or external reuse value (e.g. shared path resolution, math utilities, deterministic data transforms). Most migrated business behavior belongs in Executor, not in top-level helpers. If a helper-like routine is useful only inside one Executor feature, keep the `Helper.cs` naming style but place it under that feature's Executor folder — `Feature` is not a literal folder name; use the actual feature name.

## UI Click Signal Story

DeveloperComment is the first feature wired through the architecture. TerminalAction turns migrated menu and toolbar clicks into snapshot intents before the UI executes terminal work. MapGraphSelection turns map/link list selection and canvas/navigation requests into signal decisions before the ViewModel refreshes selected map/link consumer data. MapCanvasToolMode turns toolbar and shortcut mode changes into signal decisions before the canvas receives editor mode state. PersistenceAction identifies file/project action intents before UI terminal execution reaches Executor-owned side effects. MapProjectMutation identifies map/link add, delete, and pin intents before UI terminal execution mutates project state or calls resource Executors.

Flow:

```text
User clicks a UI control
  -> UI creates UiSignalKind.Click
  -> MapEditorShellViewModel.SubmitUiClick(signal)
  -> DeveloperCommentSignalMachine consumes the click producer
  -> TerminalActionSignalMachine consumes the same click producer
  -> ViewModel folds both decisions into the snapshot
  -> UI consumes TerminalActionRequested and clears it before executing the terminal action
  -> if Developer Comment Mode is ON, UI consumes DeveloperCommentOpenRequested and shows DeveloperCommentBox
  -> user confirms text
  -> UI calls DeveloperCommentExecutor.WriteComment(...)
```

Map and link selection follows the same producer/decision shape:

```text
List selection or canvas navigation changes
  -> UI creates UiSignalKind.SelectionChanged with selected index, map id, or link key
  -> MapEditorShellViewModel submits it to MapGraphSelectionSignalMachine
  -> ViewModel applies the selected map/link index from the decision
  -> UI refreshes from the consumer snapshot
```

Map canvas tool mode follows the same shape:

```text
Toolbar combo/button or canvas shortcut changes
  -> UI creates UiSignalKind.ValueChanged with compact mode keys
  -> MapEditorShellViewModel submits it to MapCanvasToolModeSignalMachine
  -> ViewModel stores the mode decision in the consumer snapshot
  -> UI applies the snapshot state to MapPreviewCanvas
```

Persistence actions follow the same shape:

```text
File menu click
  -> UI creates UiSignalKind.Click with action key
  -> MapEditorShellViewModel submits it to PersistenceActionSignalMachine
  -> ViewModel stores the persistence intent in the consumer snapshot
  -> UI consumes and clears the persistence intent before terminal execution
  -> Executor performs file/project side effects where needed
```

Map project mutations follow the same shape:

```text
Map/link context-menu click
  -> UI creates UiSignalKind.Click with action key
  -> MapEditorShellViewModel submits it to MapProjectMutationSignalMachine
  -> ViewModel stores the mutation intent in the consumer snapshot
  -> UI consumes and clears the mutation intent before terminal execution
  -> ViewModel and Executor perform the actual project mutation where needed
```

**Design intent**: DeveloperComment is a direct feedback channel from human developers to the Agent. A developer enables Developer Comment Mode, clicks any UI control, and writes targeted guidance — for example, "this button should validate before saving" or "this collision editor needs undo support." The comment is timestamped, attributed to the specific control that was clicked, and written to a persistent log. This lets the Agent later correlate developer intent to exact UI elements and their related functionality.

**Do not break the notification chain**. Every control click — menu items, toolbar buttons, context menu actions, list selections — is wired through `WireDeveloperInteractionHandlers`, which routes through `UiSignalFactory.Click` → `SubmitDeveloperCommentClick` → `DeveloperCommentSignalMachine` → snapshot → `DeveloperCommentBox`. When adding new controls or modifying existing ones, keep them inside this wiring. The chain from click to comment log must remain intact across all code changes.

## Current Boundary Rules

- SignalWeaver may decide that comment input should be opened, but it must not know `DeveloperCommentBox`.
- ViewModel may expose `DeveloperCommentOpenRequested`, but it must not show the dialog.
- ViewModel may expose `TerminalActionRequested`, but it must not execute the action.
- ViewModel may expose `MapProjectMutationRequested`, but it must not show dialogs or perform resource side effects for that request.
- UI must clear consumer requests after consuming them, so they do not hang around.
- Executor owns log file creation and writes.
- Heavy map data and Godot scene data should not enter SignalWeaver until reduced into simple facts.

## Key Files

```text
MapEditorTool/UI/Form1.cs
MapEditorTool/UI/DeveloperCommentBox.cs
MapEditorTool/ViewModel/MapEditorShellViewModel.cs
MapEditorTool/ViewModel/MapEditorUiSnapshot.cs
MapEditorTool/ViewModel/UiSignal.cs
MapEditorTool/SignalWeaver/Featuror/MapEditor/MapCanvas/ToolMode/
MapEditorTool/SignalWeaver/Featuror/MapEditor/MapGraph/Selection/
MapEditorTool/SignalWeaver/Featuror/MapEditor/MapProject/Mutation/
MapEditorTool/SignalWeaver/Featuror/Persistence/Action/
MapEditorTool/SignalWeaver/Featuror/UI/DeveloperComment/
MapEditorTool/SignalWeaver/Featuror/UI/TerminalAction/
MapEditorTool/Executor/DeveloperCommentExecutor.cs
```
