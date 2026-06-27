# UI Layer

The UI layer owns screens, buttons, localized labels, and visual feedback such as loading/prologue panels.

`GameStartupUI.gd` is the startup/menu boundary for `Game.tscn`:

- owns title menu, continue/start/settings/quit buttons, display/language settings, loading screen, and prologue playback;
- emits `continue_requested`, `new_game_requested`, and `quit_requested` instead of mutating game progression directly;
- does not reset MetSys, write ProgressData, enter rooms, or publish gameplay events.

`Game.gd` remains the system shell that consumes these UI intents and performs save/start/map orchestration.
