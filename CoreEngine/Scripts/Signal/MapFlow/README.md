# MapFlow

MapFlow signal customizors translate room and area workplace messages into small map intents.

Boundary:
- `Actor/RoomFlowActor.gd` owns Workbench registration and pending after-room-loaded state.
- `Actor/RoomFlowIntentExecutor.gd` owns the concrete Game/player/map-node side effects for approved map intents.
- `Actor/GeneratedRoomFactory.gd` owns runtime-only `GEN/...` room instantiation from the Junction prototype.
- `Actor/MapRoomLifecycleActor.gd` owns room-loaded publication, random-level enter/exit events, and initial MetSys player-position sync.
- `Actor/MapAreaStateActor.gd` owns startup/save-restore area state application from `AreaCatalog` into Workbench data and input-mode requests.
- `Actor/AreaFlowActor.gd` owns runtime area-load message execution and area-loaded notifications.
- `Signal/MapFlow` owns message parsing, after-room-loaded decisions, portal matching, and intent diagnostics.
- EventStudio and runtime events should request map changes through Workbench messages, not by mutating scenes directly.

First scope:
- Room load requests.
- Room-loaded `after` actions.
- Loop target and player shift messages.
- Startup/save-restore area state should stay in `MapAreaStateActor`; in-game area transitions should flow through `TYPE_LOAD_AREA_REQUEST`.
