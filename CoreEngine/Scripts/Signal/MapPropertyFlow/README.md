# MapPropertyFlow

MapPropertyFlow signal customizors translate room property config into map property intents.

Boundary:
- `Actor/RoomPropertyManagerActor.gd` owns scene mutation, node lookup, resource loading, shape creation, and TileMap writes.
- `Signal/MapPropertyFlow` owns room-loaded message routing, room config lookup, and op-to-intent planning.
- `Helper/MapProperty` owns closed parsing helpers.

First scope:
- `set_props`
- `set_resources`
- `set_shape`
- `replace_source_id`
- `replace_tile`
