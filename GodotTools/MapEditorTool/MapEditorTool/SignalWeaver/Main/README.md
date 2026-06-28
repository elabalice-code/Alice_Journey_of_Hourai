# SignalWeaver Main

`Main` is the MapEditorTool signal harness.

The ViewModel should cross into SignalWeaver through `MapEditorSignalWeaverHost`.
The host owns one producer-signal frame, calls `FeaturorEditor`, and returns pure
feature decisions for the ViewModel snapshot.

Current round shape:

```text
UI producer signal
  -> ViewModel submit method
  -> MapEditorSignalWeaverHost
  -> MapEditorSignalFrame
  -> FeaturorEditor
  -> feature SignalMachines
  -> decisions and state snapshots
  -> ViewModel consumer snapshot
```

Boundary:

- `Main` may know feature machines and pure decision objects.
- `Main` must not reference WinForms controls, dialogs, file IO, or process side effects.
- `FeaturorEditor` is the mature-feature dispatcher.
- ViewModel should not directly instantiate individual feature SignalMachines.

`CustomizeData.cs` and `CustomizeExecutor.cs` keep their old filenames for migration
continuity, but their namespaces and contents are MapEditorTool-specific.
