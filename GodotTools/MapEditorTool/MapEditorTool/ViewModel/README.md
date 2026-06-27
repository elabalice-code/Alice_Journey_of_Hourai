# MapEditorTool ViewModel

This layer separates UI-origin signals from UI refresh data.

- Producer side: `UiSignal` and `UiSignalFactory`. UI handlers create signals only; they do not own refresh state.
- Consumer side: `MapEditorUiSnapshot`, `MapShellState`, and `LinkShellState`. Forms read these objects when refreshing controls.
- Coordinator: `MapEditorShellViewModel`. It receives producer signals and folds them into the latest consumer snapshot.

Signal-bus access contract:

- `MapEditorShellViewModel.ProducerSignals` exposes every producer signal as a read-only list.
- `MapEditorShellViewModel.LatestProducerSignal` exposes the newest producer signal.
- `MapEditorShellViewModel.ConsumerSnapshot` exposes the current consumer snapshot.
- `MapEditorShellViewModel.Snapshot` remains as the compatibility alias for `ConsumerSnapshot`.
- Writes still go through bus methods such as `SubmitSignal`, `SubmitDeveloperCommentClick`, and `SetDeveloperCommentMode`.
- Terminal UI results such as confirmed DeveloperComment text are not producer signals anymore; UI hands them to an Executor after the SignalWeaver decision has already requested the UI action.

Current scope is the prototype shell. Real map data, validation state, and edit operations should enter this layer as new producer signals and consumer snapshot fields before UI controls consume them.
