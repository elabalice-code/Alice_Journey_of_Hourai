# Helper

Closed helper calculations live here.

Use this layer when a feature is:
- pure input/output calculation;
- deterministic for the same inputs;
- not a Workbench message route;
- not a signal pool or state machine;
- not responsible for mutating scenes, actors, player state, or global services.
- not a mutable runtime data container.

Examples:
- resolve an enum to a display name;
- normalize a resource path;
- calculate a damage number from raw value and armor;
- select a fallback room or node from plain data.

Current helper domains:
- `Expression/`: condition expression evaluation.
- `RuntimeEvent/`: runtime event action payload parsing and field normalization.
- `Map/`: map enum/name conversion and plain fact collection from map nodes.
- `Map/CollisionLayout.gd`: collision metadata/data parsing, texture anchoring, and map-bounds calculations that preserve MapEditor resource formats.
- `MapProperty/`: room path normalization for property config lookup.
- `Inventory/`: inventory slot shape/index helpers and equipment data shape helpers.
- `Combat/`: closed damage formula and state snapshot helpers.
- `Dialogue/`: dialogue progress event key formatting.

Layer rule:
- `Actor/` executes side effects and owns Workbench registration.
- `Data/` owns mutable runtime data containers and the Workbench message/global-state envelope.
- `Signal/` translates messages into frames, intents, and state transitions.
- `Helper/` provides closed calculations used by either layer.
