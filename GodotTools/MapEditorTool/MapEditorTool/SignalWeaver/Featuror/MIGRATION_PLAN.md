# Featuror Migration Plan

This document tracks staged promotion of MapEditorTool signal rules into mature Featuror modules.

Migration anchor:

```text
Which consumer surface does this rule finally serve?
```

## Principles

- Migrate by consumer cluster.
- Keep each feature focused on one stable rule family.
- Write only simple consumer decisions from Featuror modules.
- Let ViewModel fold decisions into snapshots.
- Let UI consume display intents.
- Let Executor perform terminal side effects.
- Keep private intermediate values inside one feature module.
- Promote genuinely shared signals to the ViewModel bus before exposing them to multiple features.
- Only migrate simple signal rules here: bool, numeric values, enums, compact ids, short status labels.
- Leave long text, file paths, arrays, scene payloads, and mutable objects to owner modules or Executors.

## Current Domains

```text
Featuror/
|- MapEditor/
|  |- MapCanvas/
|  |  |- ToolMode/
|  |- MapGraph/
|  |  |- Selection/
|  |- MapProject/
|  |  |- Mutation/
|- Persistence/
|  |- Action/
|- UI/
|  |- DeveloperComment/
|  |- TerminalAction/
```

## Stage 1 - UI DeveloperComment

Goal: prove the MapEditorTool signal-state-machine shape with one real feature.

Feature:

- `UI/DeveloperComment`
  - Status: active.
  - Producer facts: checkbox mode state and UI click source.
  - Consumer decisions: request comment input, mark source signal consumed, status text.
  - UI side effect: show `DeveloperCommentBox`.
  - Executor side effect: write `logs/developer-comments.log`.

Done when:

```text
UI click -> ViewModel bus -> DeveloperComment state machine -> snapshot intent -> UI consumes intent -> Executor writes terminal comment
```

works without SignalWeaver referencing WinForms or file IO.

## Stage 2 - UI TerminalAction

Goal: route migrated menu and toolbar actions through a consumer intent before the UI performs terminal work.

Feature:

- `UI/TerminalAction`
  - Status: active.
  - Producer facts: click source and compact action key.
  - Consumer decisions: request terminal action execution, mark source signal consumed, status text.
  - UI side effect: consume and clear `TerminalActionRequested`, then invoke the existing UI-owned terminal action handler.
  - Executor side effect: unchanged; concrete actions still call their owning Executor after the UI consumes the intent.

Done when:

```text
UI click -> ViewModel bus -> TerminalAction state machine -> snapshot intent -> UI consumes intent -> existing terminal action path runs
```

works without SignalWeaver referencing WinForms controls or executing business side effects.

## Stage 3 - MapGraph Selection

Goal: route map and link selection/navigation changes through signal decisions before the ViewModel refreshes consumer state.

Feature:

- `MapEditor/MapGraph/Selection`
  - Status: active.
  - Producer facts: selection-changed source plus selected index, map id, or compact link key.
  - Consumer decisions: apply map or link selection, mark source signal consumed, status text.
  - UI side effect: refresh visible map/link property grids and canvases from the snapshot.
  - Executor side effect: none.

Done when:

```text
List or canvas navigation change -> ViewModel bus -> MapGraphSelection state machine -> selection decision -> ViewModel applies selection -> UI refreshes snapshot
```

works without SignalWeaver referencing WinForms list controls, canvases, or mutable map/link objects.

## Stage 4 - MapCanvas ToolMode

Goal: route toolbar and canvas shortcut mode changes through signal decisions before the canvas receives editor mode state.

Feature:

- `MapEditor/MapCanvas/ToolMode`
  - Status: active.
  - Producer facts: compact view mode, collision edit mode, collision data mode, tool, and target keys.
  - Consumer decisions: update map canvas tool-mode state, mark source signal consumed, status text.
  - UI side effect: apply snapshot mode state to `MapPreviewCanvas`.
  - Executor side effect: none.

Done when:

```text
Toolbar or shortcut change -> ViewModel bus -> MapCanvasToolMode state machine -> tool-mode decision -> snapshot -> UI applies canvas state
```

works without SignalWeaver referencing WinForms controls or `MapPreviewCanvas`.

## Stage 5 - Persistence Action

Goal: classify file/project terminal action intents before UI terminal execution reaches Executor side effects.

Feature:

- `Persistence/Action`
  - Status: active.
  - Producer facts: click action key for New, Open, Save, Save As, Import, Apply, and Exit.
  - Consumer decisions: request persistence action execution, classify action kind, mark source signal consumed, status text.
  - UI side effect: consume and clear the persistence action intent before terminal execution.
  - Executor side effect: concrete file/project side effects remain in ProjectFile, MapImport, MapApply, GameSettings, and related Executors.

Done when:

```text
File menu click -> ViewModel bus -> PersistenceAction state machine -> persistence action intent -> UI consumes intent -> Executor-backed terminal action runs
```

works without SignalWeaver referencing dialogs, files, or process side effects.

## Stage 6 - MapProject Mutation

Goal: classify project graph mutation intents before UI terminal execution mutates the ViewModel or calls resource Executors.

Feature:

- `MapEditor/MapProject/Mutation`
  - Status: active.
  - Producer facts: click action key for Add Map, Delete Map, Pin Map, Add Link, and Delete Link.
  - Consumer decisions: request project mutation execution, classify mutation kind, mark source signal consumed, status text.
  - UI side effect: consume and clear the mutation intent before invoking the existing terminal mutation handler.
  - Executor side effect: map creation/deletion and starting-map writes remain in their owning Executors.

Done when:

```text
Map/link context click -> ViewModel bus -> MapProjectMutation state machine -> mutation intent -> UI consumes intent -> ViewModel/Executor-backed mutation runs
```

works without SignalWeaver referencing WinForms controls, mutable map/link objects, files, or Godot resources.

## Future Candidate Domains

- `UI`: tool-window and review-mode display intents.
- `MapEditor/MapCanvas`: pointer/canvas editing facts beyond current tool-mode state.
- `MapEditor/MapGraph`: selected portal, link navigation, graph visibility.
- `MapEditor/MapProject`: deeper dirty-state and mutation validation rules.
- `Validation`: current validation status and review gates.
- `Persistence`: save/apply/dirty-state intents.
