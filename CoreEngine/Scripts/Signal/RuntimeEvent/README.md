# RuntimeEvent Signal

Runtime event Signal code turns event configuration and incoming runtime frames into state/action intents.

Boundary:
- `RuntimeEventFlowActor.gd` loads project data, owns event-state mutation, and executes action/state intents.
- `Contract/RuntimeEventActionTypes.gd` owns runtime event action type names used by event configuration.
- `RuntimeEvent/Featuror/` owns trigger matching, event state output, and action intent planning.
- `Helper/RuntimeEvent/RuntimeEventActionPayload.gd` owns closed payload JSON parsing and field normalization.
- Scene mutation and Workbench sends stay in Actor executors.

Current rule:
- Signal may decide which intent to emit.
- Helper may parse and normalize data.
- Actor executes the intent.
