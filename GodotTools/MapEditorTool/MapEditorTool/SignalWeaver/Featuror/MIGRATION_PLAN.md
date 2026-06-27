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
|- UI/
|  |- DeveloperComment/
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

## Future Candidate Domains

- `UI`: tool-window and review-mode display intents.
- `MapCanvas`: pointer/tool/canvas mode signals.
- `MapGraph`: selected map, selected portal, link navigation, graph visibility.
- `Validation`: current validation status and review gates.
- `Persistence`: save/apply/dirty-state intents.
