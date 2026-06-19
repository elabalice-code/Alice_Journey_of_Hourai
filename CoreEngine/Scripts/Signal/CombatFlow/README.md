# CombatFlow

CombatFlow signal customizors translate combat workplace messages into combat intents.

Boundary:
- `Actor/CombatManagerActor.gd` owns Combatant access, damage application, Workbench updates, and battle result side effects.
- `Signal/CombatFlow` owns combat message routing and intent planning.
- `Helper/Combat` owns closed numeric calculations and state snapshot comparison.

First scope:
- apply damage request;
- combat sync request;
- raw damage and armor formula extraction;
- combat state snapshot comparison for throttled state output.
