# MapFlow

MapFlow signal customizors translate room and area workplace messages into small map intents.

Boundary:
- `Actor/RoomFlowActor.gd` owns Workbench registration and mutates `Game`, player, and map nodes.
- `Signal/MapFlow` owns message parsing, after-room-loaded decisions, portal matching, and intent diagnostics.
- EventStudio and runtime events should request map changes through Workbench messages, not by mutating scenes directly.

First scope:
- Room load requests.
- Room-loaded `after` actions.
- Loop target and player shift messages.
