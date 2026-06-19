# Contract

Shared runtime contracts live here.

Use this layer for small, stable names that must be shared by Actor, Signal, UI, World, and tools without making one runtime layer depend on another.

Current contracts:
- `MessageTypes.gd`: Workbench message type constants.

Layer rule:
- `ActorFramework.gd` owns actor/workbench infrastructure and keeps compatibility aliases for old message constants.
- New code should reference `MessageTypes.gd` directly for message type constants.
- Data containers such as `InventoryData` and `QuestData` still live in `ActorFramework.gd` until they are split in a dedicated pass.
