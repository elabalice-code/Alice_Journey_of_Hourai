# Featuror

`Featuror` holds mature SignalWeaver feature families for MapEditorTool.

Only promote a module here when it has a stable contract, verification path, and repeated usefulness beyond one local fix or experiment.

Current feature families:

```text
Featuror/
|- UI/
|  |- DeveloperComment/
|- FeaturorEditor.cs
```

Current real feature:

- `UI/DeveloperComment`
  - Producer inputs: developer-comment mode checkbox changes and UI click signals.
  - Consumer outputs: simple intent/status signals such as `OpenCommentBox`.
  - Boundary: the feature never opens WinForms dialogs and never writes logs.

Consumer domains should stay explicit. New features should be grouped by the consumer surface they ultimately serve, for example `UI`, `MapCanvas`, `MapGraph`, `Validation`, or `Persistence`.

Signal-frame governance, diagnostics, and generic arbitration belong in `SignalWeaver/Main`, not in a Featuror business domain.

Long-running migration notes live in `MIGRATION_PLAN.md`.
