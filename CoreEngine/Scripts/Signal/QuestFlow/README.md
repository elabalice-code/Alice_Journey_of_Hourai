# QuestFlow

QuestFlow signal customizors translate quest workplace messages into lifecycle intents.

Boundary:
- `Actor/QuestManagerActor.gd` owns quest data mutation and Workbench registration.
- `Signal/QuestFlow` owns lifecycle checks and intent routing.
- `Contract/QuestActionTypes.gd` owns accepted quest action names.
- `Helper/` should hold closed pure calculations once quest rules become complex enough.

First scope:
- accept active quest;
- advance active quest stage;
- complete active quest.

Inventory requirements are reserved for a later pass. This layer should decide that an item requirement exists, while inventory actors own item mutation.
