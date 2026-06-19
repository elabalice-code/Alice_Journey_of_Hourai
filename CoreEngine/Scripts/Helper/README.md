# Helper

Closed helper calculations live here.

Use this layer when a feature is:
- pure input/output calculation;
- deterministic for the same inputs;
- not a Workbench message route;
- not a signal pool or state machine;
- not responsible for mutating scenes, actors, player state, or global services.

Examples:
- resolve an enum to a display name;
- normalize a resource path;
- calculate a damage number from raw value and armor;
- select a fallback room or node from plain data.

Current helper domains:
- `Expression/`: condition expression evaluation.
- `Map/`: map enum/name conversion and plain fact collection from map nodes.
- `MapProperty/`: room path normalization for property config lookup.
- `Inventory/`: inventory slot shape/index helpers.
- `Combat/`: closed damage formula helpers.

Layer rule:
- `Actor/` executes side effects and owns Workbench registration.
- `Signal/` translates messages into frames, intents, and state transitions.
- `Helper/` provides closed calculations used by either layer.
