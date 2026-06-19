# Data

Pure data containers live here.

Use this layer for runtime state objects that are owned by systems or the Workbench but should not belong to an Actor, Signal, or Helper folder.

Boundary:
- `Data/` owns mutable runtime data containers such as player stats, combat stats, inventory state, quest state, and progress.
- `Helper/` owns pure input/output calculations and formatting helpers.
- `Actor/` owns Workbench registration, scene/service access, and mutation side effects.
- `Signal/` owns message routing, planning, and state-machine decisions.

Current containers:
- `PlayerData.gd`: movement stat values and base/stat reset behavior.
- `CombatData.gd`: shared combat stat snapshot data.
- `InventoryData.gd`: bag, equipped item, rune state, inventory size constants, and shape normalization.
- `QuestData.gd`: active/completed quest state and dialogue history.
- `ProgressData.gd`: save/progress/event flags.
- `WorkPlace.gd`: Workbench message/global-state envelope that references the domain data objects above.

`ActorFramework.gd` should not own mutable runtime data containers. It remains only as a legacy message-constant compatibility shell for no-fly-zone map scenes that still preload it.
