# InventoryFlow

InventoryFlow signal customizors translate inventory workplace messages into inventory intents.

Boundary:
- `Actor/InventoryManagerActor.gd` owns inventory mutation, Workbench registration, and equipment sync side effects.
- `Signal/InventoryFlow` owns action routing, operation guard planning, and state-machine decisions for pickup, equipment, runes, composition, decomposition, and quest item requirements.
- `Helper/Inventory` owns closed slot calculations and equipment data shape conversion.

First scope:
- route existing item actions into intents;
- filter inventory intents against current inventory data before Actor mutation;
- reserve `compose` and `decompose` actions for the future crafting state machine;
- keep current inventory behavior unchanged.
