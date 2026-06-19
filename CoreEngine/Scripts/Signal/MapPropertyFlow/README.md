# MapPropertyFlow

MapPropertyFlow signal customizors translate room property config into map property intents.

Boundary:
- `Actor/RoomPropertyManagerActor.gd` owns Workbench registration and passes the current map root to the actor-side executor.
- `Actor/MapPropertyIntentExecutor.gd` owns the concrete map-root side effects for approved property intents.
- `Signal/MapPropertyFlow` owns room-loaded message routing, room config lookup, and op-to-intent planning.
- `Helper/MapProperty` owns closed parsing helpers.

First scope:
- `set_props`
- `set_resources`
- `set_shape`
- `replace_source_id`
- `replace_tile`
