# Signal

Runtime signal customizors live here. They translate dense workplace messages into small domain frames, match rules, and emit intent messages that other runtime actors consume.

Current rule:
- `Actor/` owns execution boundaries and Workbench registration.
- `Contract/` owns shared message names that Actor, Signal, UI, World, and tools must agree on.
- `Signal/` owns matching, state output, diagnostics, and configurable signal behavior.
- `Helper/` owns closed pure input/output calculations that do not need a signal pool, state machine, or Workbench context.
- Scene mutation stays outside editor data. Runtime event signals may output state or intent; map, dialogue, combat, and quest actors consume those intents.

Do not put every extracted function into `Signal/`. If a feature is closed, deterministic, and only maps inputs to outputs, place it under `Scripts/Helper/` and let Signal or Actor code call it.

Current domains:
- `RuntimeEvent/`: EventStudio runtime event trigger matching, event state/action intent planning, and diagnostics.
- `MapFlow/`: room and area flow message planning.
- `MapPropertyFlow/`: room property config lookup and op-to-intent planning for external map/property tools.
- `QuestFlow/`: quest lifecycle routing and intent planning.
- `InventoryFlow/`: inventory action routing with reserved composition/decomposition state-machine entry points.
- `CombatFlow/`: combat request routing and damage/sync intent planning.
- `DialogueFlow/`: story-first dialogue routing and active-dialogue handoff planning.

Signal hygiene check:
- Signal code should not call Workbench `send`, mutate workplace data, load resources, instantiate classes, or set scene/node properties.
- Signal code should use `Contract/MessageTypes.gd` for Workbench message names instead of treating `ActorFramework.gd` as the message source.
- Signal code may read static catalogs and plain data in order to build intents.
- Map node inspection should be converted to plain facts before entering Signal. For example, `Helper/Map/MapNodeFacts.gd` collects positions and portal targets, while `MapFlow/Featuror/RoomLoadedAfterAction.gd` only turns those facts into room-flow intents.

Boundary scan:
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\check-architecture.ps1` from the project root to list suspicious Signal/Helper boundary crossings.
- Add `-FailOnHigh` when the scan should fail on high-severity findings.
