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

Release output is copied to:

```text
ReleasePackage/
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

Current real MapEditorTool feature:

```text
SignalWeaver/Featuror/UI/DeveloperComment/
```

The older `Main`, `Compozor`, `Incubator`, and `Featuror` structure remains as long-evolution architecture reference. Do not delete it casually; adapt new features into that shape as they mature.

### Executor

Path:

```text
MapEditorTool/Executor/
```

Executors perform terminal side effects after the signal chain no longer needs the result as a producer.

Current executor:

```text
DeveloperCommentExecutor
```

It writes confirmed developer comments to:

```text
logs/developer-comments.log
```

## DeveloperComment Signal Story

DeveloperComment is the first real feature wired through the architecture.

Flow:

```text
User clicks a UI control
  -> UI creates UiSignalKind.Click
  -> MapEditorShellViewModel.SubmitDeveloperCommentClick(signal)
  -> DeveloperCommentSignalMachine consumes the click producer
  -> if Developer Comment Mode is ON, it emits OpenCommentBox = true
  -> ViewModel writes DeveloperCommentOpenRequested into the snapshot
  -> UI consumes and clears the open request
  -> UI shows DeveloperCommentBox
  -> user confirms text
  -> UI calls DeveloperCommentExecutor.WriteComment(...)
```

Important rule:

Confirmed comment text and canceled dialog results are terminal UI outcomes. They do not go back into SignalWeaver as producer signals. Once the UI has the final text, the correct next layer is Executor.

## Current Boundary Rules

- SignalWeaver may decide that comment input should be opened, but it must not know `DeveloperCommentBox`.
- ViewModel may expose `DeveloperCommentOpenRequested`, but it must not show the dialog.
- UI must clear the open request after consuming it, so the consumer signal does not hang around.
- Executor owns log file creation and writes.
- Heavy map data and Godot scene data should not enter SignalWeaver until reduced into simple facts.

## Key Files

```text
MapEditorTool/UI/Form1.cs
MapEditorTool/UI/DeveloperCommentBox.cs
MapEditorTool/ViewModel/MapEditorShellViewModel.cs
MapEditorTool/ViewModel/MapEditorUiSnapshot.cs
MapEditorTool/ViewModel/UiSignal.cs
MapEditorTool/SignalWeaver/Featuror/UI/DeveloperComment/
MapEditorTool/Executor/DeveloperCommentExecutor.cs
```
