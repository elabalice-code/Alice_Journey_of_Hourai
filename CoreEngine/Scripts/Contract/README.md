# Contract

Shared runtime contracts live here.

Use this layer for small, stable names that must be shared by Actor, Signal, UI, World, and tools without making one runtime layer depend on another.

Current contracts:
- `MessageTypes.gd`: Workbench message type constants.
- `RuntimeEventActionTypes.gd`: Runtime event configuration action type names.
- `InventoryActionTypes.gd`: inventory workplace action names shared by UI, Actor, Signal, and tools.
- `DialogueActionTypes.gd`: dialogue workplace action names shared by NPCs, Actor, Signal, and tools.
- `QuestActionTypes.gd`: quest workplace action names shared by RuntimeEvent, Actor, Signal, and tools.
- `AudioActionTypes.gd`: audio workplace action names shared by Actor, Signal, and tools.
- `CombatFactionTypes.gd`: combat faction/group names shared by characters, bullets, Actor, Signal, and tools.
- `EquipmentSlotTypes.gd`: equipment slot names shared by inventory, combat, UI, and tools.

Layer rule:
- `ActorFramework.gd` keeps compatibility aliases for old message constants used by no-fly-zone map scenes.
- New code should reference `MessageTypes.gd` directly for message type constants.
- Data containers and `WorkPlace` live in `Scripts/Data/`.
