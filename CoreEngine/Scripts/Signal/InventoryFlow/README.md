# InventoryFlow

InventoryFlow signal customizors translate inventory workplace messages into inventory intents.

Boundary:
- `Actor/InventoryManagerActor.gd` owns inventory mutation, Workbench registration, and equipment sync side effects.
- `Signal/InventoryFlow` owns action routing and state-machine decisions for pickup, equipment, runes, composition, decomposition, and quest item requirements.
- `Helper/Inventory` owns closed slot calculations.

First scope:
- route existing item actions into intents;
- reserve `compose` and `decompose` actions for the future crafting state machine;
- keep current inventory behavior unchanged.
