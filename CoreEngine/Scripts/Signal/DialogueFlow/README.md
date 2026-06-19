# DialogueFlow

DialogueFlow owns the rules that decide which dialogue intent should happen next.

Boundary:
- `Actor/DialogueManagerActor.gd` owns UI nodes, input handling, Workbench sending, and progress mutation.
- NPCs and runtime event executors should request dialogue through `MessageTypes.TYPE_DIALOGUE_ACTION_REQUEST`.
- `Signal/DialogueFlow` owns dialogue choice rules such as story-first and active-dialogue handoff.
- `Helper/Dialogue` owns closed deterministic helpers such as progress event key formatting.

Current scope:
- first interaction opens story dialogue when the story event is not done;
- clicking through the same active story dialogue switches to normal NPC dialogue;
- requesting a new dialogue while another one is active asks the actor to close the active dialogue.

DialogueFlow only returns intents. It does not mutate story progress, hide/show UI, or send runtime event signals.
