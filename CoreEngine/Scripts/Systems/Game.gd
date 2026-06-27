# This is the main script of the game. It manages the current map and some other stuff.
extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysGame.gd"
class_name Game

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const SaveManager = preload("res://addons/MetroidvaniaSystem/Template/Scripts/SaveManager.gd")
const DialogueManagerActor = preload("res://CoreEngine/Scripts/Actor/DialogueManagerActor.gd")
const MapRoomLoadOrchestratorScript = preload("res://CoreEngine/Scripts/Actor/MapRoomLoadOrchestrator.gd")
const MapRuntimeGuardScript = preload("res://CoreEngine/Scripts/Actor/MapRuntimeGuard.gd")
const MapSpawnActorScript = preload("res://CoreEngine/Scripts/Actor/MapSpawnActor.gd")
const MapRuntimeSurfaceScript = preload("res://CoreEngine/Scripts/Actor/MapRuntimeSurface.gd")
const MapAreaStateActorScript = preload("res://CoreEngine/Scripts/Actor/MapAreaStateActor.gd")
const MapRoomLifecycleActorScript = preload("res://CoreEngine/Scripts/Actor/MapRoomLifecycleActor.gd")
const ProgressData = preload("res://CoreEngine/Scripts/Data/ProgressData.gd")
const SAVE_PATH = "user://example_save_data.sav"
const INITIAL_ROOM_PATH: String = "res://CoreEngine/Maps/DiceRoom.tscn"

# The game starts in this map. Uses special annotation that enabled dedicated inspector plugin.
@export_file("room_link") var starting_map: String

# Number of collected collectibles. Setting it also updates the counter.
var collectibles: int:
	set(count):
		collectibles = count
		%CollectibleCount.text = "%d/7" % count
		var workbench := WorkbenchService.get_singleton()
		if workbench:
			workbench.send({
				"type": &"value_sync",
				"key": &"collectibles",
				"value": count,
				"context": {"origin": &"Game"}
			})

# The coordinates of generated rooms. MetSys does not keep this list, so it needs to be done manually.
# For Custom Runner integration.
var custom_run: bool
var loop: String:
	get:
		return _map_room_loader.loop_path
	set(value):
		_map_room_loader.loop_path = value
var _debug_collisions_visible: bool = false
var _map_surface_state: Dictionary = {}
var _map_runtime_guard: MapRuntimeGuard = MapRuntimeGuardScript.new()
var _map_room_loader: MapRoomLoadOrchestrator = MapRoomLoadOrchestratorScript.new()
var _map_room_lifecycle: MapRoomLifecycleActor = MapRoomLifecycleActorScript.new()

@onready var _startup_ui: GameStartupUI = $UI as GameStartupUI

func _ready() -> void:
	# A trick for static object reference (before static vars were a thing).
	get_script().set_meta(&"singleton", self)
	
	var default_window_size := DisplayServer.window_get_size()
	if is_instance_valid(_startup_ui) and _startup_ui.apply_display_settings_on_startup(default_window_size):
		return
	# Make sure MetSys is in initial state.
	# Does not matter in this project, but normally this ensures that the game works correctly when you exit to menu and start again.
	MetSys.reset_state()
	# Assign player for MetSysGame.
	set_player($Player)
	
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.set_service(&"game", self)
		if is_instance_valid(player):
			workbench.set_service(&"player", player)
		workbench.register_actor(self, [MessageTypes.TYPE_BATTLE_RESULT_REQUEST], &"_on_workplace")
	
	add_module("res://CoreEngine/Scripts/Systems/AreaRoomTransitions.gd")
	add_module("res://CoreEngine/Scripts/Core/ExtensionBootstrap.gd")
	_ensure_dialogue_manager()
	
	if FileAccess.file_exists(SAVE_PATH):
		# If save data exists, load it using MetSys SaveManager.
		var save_manager := SaveManager.new()
		save_manager.load_from_text(SAVE_PATH)
		save_manager.retrieve_game(self)
	else:
		# If no data exists, set empty one.
		MetSys.set_save_data()
	
	_apply_area_defaults()
	
	# Initialize room when it changes.
	room_loaded.connect(init_room, CONNECT_DEFERRED)
	
	# Make sure minimap is at correct position (required for themes to work correctly).
	%Minimap.set_offsets_preset(Control.PRESET_TOP_RIGHT, Control.PRESET_MODE_MINSIZE, 8)
	
	_setup_startup_ui(default_window_size)

func _setup_startup_ui(default_window_size: Vector2i) -> void:
	if not is_instance_valid(_startup_ui):
		return
	_connect_startup_ui_signals()
	_startup_ui.initialize(SAVE_PATH, default_window_size)
	player.process_mode = Node.PROCESS_MODE_DISABLED

func _connect_startup_ui_signals() -> void:
	if not _startup_ui.continue_requested.is_connected(_continue_game):
		_startup_ui.continue_requested.connect(_continue_game)
	if not _startup_ui.new_game_requested.is_connected(_start_new_game):
		_startup_ui.new_game_requested.connect(_start_new_game)
	if not _startup_ui.quit_requested.is_connected(_quit_game):
		_startup_ui.quit_requested.connect(_quit_game)

func _set_loading_visible(visible: bool, text: String = "", progress: float = 0.0) -> void:
	if is_instance_valid(_startup_ui):
		_startup_ui.set_loading_visible(visible, text, progress)

func _show_title_menu() -> void:
	if is_instance_valid(_startup_ui):
		_startup_ui.show_title_menu()
	player.process_mode = Node.PROCESS_MODE_DISABLED

func _hide_title_menu() -> void:
	if is_instance_valid(_startup_ui):
		_startup_ui.hide_title_menu()

func _play_prologue_if_any() -> void:
	if is_instance_valid(_startup_ui):
		await _startup_ui.play_prologue_if_any()

func _enter_starting_room() -> bool:
	var ok := await _load_room_with_progress(starting_map, tr("LOADING_CHAPTER"))
	if ok:
		_teleport_player_to_save_point_if_any()
		await get_tree().physics_frame
		var workbench := WorkbenchService.get_singleton()
		if workbench != null:
			workbench.send({
				"type": MessageTypes.TYPE_RESET_MAP_STARTING_COORDS_REQUEST
			})
		player.process_mode = Node.PROCESS_MODE_INHERIT
	_set_loading_visible(false)
	if not ok:
		_show_title_menu()
	return ok

func _load_packed_scene_threaded(resolved_path: String, label_text: String) -> PackedScene:
	if resolved_path.is_empty():
		return null
	_set_loading_visible(true, label_text, 0.0)
	await get_tree().process_frame
	var err := ResourceLoader.load_threaded_request(resolved_path, "PackedScene")
	if err != OK:
		return null
	var progress: Array = []
	while true:
		var status := ResourceLoader.load_threaded_get_status(resolved_path, progress)
		if status == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
			if progress.size() > 0:
				_set_loading_visible(true, "", float(progress[0]))
			await get_tree().process_frame
			continue
		if status == ResourceLoader.THREAD_LOAD_LOADED:
			_set_loading_visible(true, "", 1.0)
			var res := ResourceLoader.load_threaded_get(resolved_path)
			return res as PackedScene
		return null
	return null

func _load_room_with_progress(path: String, label_text: String) -> bool:
	var ok := await _map_room_loader.load_room_with_progress(self, path, label_text, _set_loading_visible, _load_packed_scene_threaded)
	map_changing = _map_room_loader.map_changing
	return ok

func _teleport_player_to_save_point_if_any() -> void:
	MapSpawnActorScript.teleport_player_to_save_point_if_any(map, player, custom_run)

func _continue_game() -> void:
	_hide_title_menu()
	player.process_mode = Node.PROCESS_MODE_DISABLED
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.send({
			"type": MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
			"signal": "Game.Continue",
			"source_domain": "System"
		})
	await _enter_starting_room()

func _start_new_game() -> void:
	if FileAccess.file_exists(SAVE_PATH):
		var abs_path := ProjectSettings.globalize_path(SAVE_PATH)
		DirAccess.remove_absolute(abs_path)
	
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.register_workplace_data(&"progress", ProgressData.new())
	collectibles = 0
	loop = ""
	_map_room_lifecycle.reset()
	
	MetSys.reset_state()
	MetSys.set_save_data()
	_apply_initial_area()
	_hide_title_menu()
	player.process_mode = Node.PROCESS_MODE_DISABLED
	_set_loading_visible(false)
	if workbench != null:
		workbench.send({
			"type": MessageTypes.TYPE_RUNTIME_EVENT_START
		})
		workbench.send({
			"type": MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
			"signal": "Game.StartNew",
			"source_domain": "System"
		})
	await _play_prologue_if_any()
	await _enter_starting_room()

func _quit_game() -> void:
	get_tree().quit()

# Debugging helper. Press F2 to quickly reload game.
func _unhandled_input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if k and k.pressed and not k.echo:
		var key := k.keycode
		if key == 0:
			key = k.physical_keycode
		if key == KEY_Q:
			_debug_collisions_visible = not _debug_collisions_visible
			get_tree().debug_collisions_hint = _debug_collisions_visible
			get_viewport().set_input_as_handled()
			return
		
		var area_id: StringName = &""
		if key == KEY_F6:
			area_id = &"story_vertical"
		elif key == KEY_F7:
			area_id = &"story_dungeon_rooms"
		elif key == KEY_F8:
			area_id = &"story_overworld"
		elif key == KEY_F9:
			area_id = &"story_starsky"
		if area_id != &"":
			var workbench := WorkbenchService.get_singleton()
			if workbench != null:
				workbench.send({
					"type": MessageTypes.TYPE_LOAD_AREA_REQUEST,
					"area_id": area_id
				})
			get_viewport().set_input_as_handled()
			return
	if event.is_action_pressed(&"toggle_bag"):
		var inv := get_node_or_null(^"UI/InventoryUI") as InventoryUI
		if inv != null:
			inv.toggle_bag()
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed(&"toggle_equipment"):
		var inv2 := get_node_or_null(^"UI/InventoryUI") as InventoryUI
		if inv2 != null:
			inv2.toggle_equipment()
		get_viewport().set_input_as_handled()
		return
	if k and k.pressed and k.keycode == KEY_F2:
		var cr: Script
		# CustomRunner can't be used directly, since the addon is optional.
		if ResourceLoader.exists("res://addons/CustomRunner/CustomRunner.gd"):
			cr = load("res://addons/CustomRunner/CustomRunner.gd")
		
		if cr and cr.is_custom_running():
			get_tree().change_scene_to_file.call_deferred("res://CoreEngine/CustomRunnerIntegration/CustomStart.tscn")
		else:
			get_tree().reload_current_scene()

# Returns this node from anywhere.
static func get_singleton() -> Game:
	return (Game as Script).get_meta(&"singleton") as Game

func _get_save_data() -> Dictionary:
	var progress := _get_progress()
	var workbench := WorkbenchService.get_singleton()
	var area_id := MapAreaStateActorScript.current_area_id(workbench)
	return {
		"collectible_count": collectibles,
		"generated_rooms": progress.generated_rooms if progress != null else [],
		"events": progress.events if progress != null else [],
		"current_area_id": area_id,
		"current_room": MetSys.get_current_room_id(),
		"abilities": player.abilities,
	}

func _set_save_data(data: Dictionary):
	collectibles = int(data.get("collectible_count", collectibles))
	var progress := _get_progress()
	if progress != null:
		progress.generated_rooms.assign(data.get("generated_rooms", []))
		var raw_events: Array = data.get("events", [])
		progress.events.clear()
		for e in raw_events:
			var s := StringName(str(e))
			if s != &"":
				progress.add_event(s)
	player.abilities.assign(data.get("abilities", []))
	var saved_area_id := StringName(str(data.get("current_area_id", "")))
	if saved_area_id != &"":
		_apply_area_state(saved_area_id, false)
	
	if not custom_run:
		var loaded_starting_map: String = data.get("current_room", "")
		if not loaded_starting_map.is_empty():
			starting_map = loaded_starting_map

func _apply_area_defaults() -> void:
	var workbench := WorkbenchService.get_singleton()
	var result := MapAreaStateActorScript.apply_defaults(workbench)
	_apply_area_result(result)

func _apply_initial_area() -> void:
	var workbench := WorkbenchService.get_singleton()
	var result := MapAreaStateActorScript.apply_initial_area(workbench)
	_apply_area_result(result)

func _apply_area_state(area_id: StringName, also_set_starting_room: bool) -> void:
	var workbench := WorkbenchService.get_singleton()
	var result := MapAreaStateActorScript.apply_area(workbench, area_id, also_set_starting_room)
	_apply_area_result(result)

func _apply_area_result(result: Dictionary) -> void:
	var next_starting_map := str(result.get("starting_room", ""))
	if not next_starting_map.is_empty():
		starting_map = next_starting_map

# Save game using MetSys SaveManager.
func save_game():
	var save_manager := SaveManager.new()
	save_manager.store_game(self)
	save_manager.save_as_text(SAVE_PATH)

func init_room():
	var cam := $Player/Camera2D as Camera2D
	var ri := MetSys.get_current_room_instance()
	if ri != null and cam != null:
		ri.adjust_camera_limits(cam)
	player.on_enter()
	MapSpawnActorScript.ensure_alice(map, player, INITIAL_ROOM_PATH)
	_map_runtime_guard.reset_entry(player)
	_map_surface_state = MapRuntimeSurfaceScript.apply_surface_metadata(map, player)
	if cam != null:
		MapRuntimeSurfaceScript.apply_camera_limits_from_metadata(map, cam, _current_room_has_cells())
	MapRuntimeSurfaceScript.print_background_diagnostics(map, _get_viewport_size())
	_map_room_lifecycle.publish_room_entered(WorkbenchService.get_singleton(), map, player)

func _process(_delta: float) -> void:
	if map == null:
		return
	MapRuntimeSurfaceScript.update_background_texture_focus(_map_surface_state, map, player)

func _physics_process(delta: float) -> void:
	if _map_runtime_guard.tick(delta, map, player, map_changing):
		MetSys.set_player_position(player.position)

func _current_room_has_cells() -> bool:
	var ri := MetSys.get_current_room_instance()
	if ri != null and typeof(ri.get("cells")) == TYPE_ARRAY:
		return not (ri.get("cells") as Array).is_empty()
	return false

func _get_viewport_size() -> Vector2:
	var vp := get_viewport()
	if vp == null:
		return Vector2.ZERO
	return vp.get_visible_rect().size

func _get_progress() -> ProgressData:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return null
	return workbench.get_workplace_data(&"progress") as ProgressData

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	match t:
		MessageTypes.TYPE_BATTLE_RESULT_REQUEST:
			_show_battle_result(str(msg.get("text", "")))

func _ensure_dialogue_manager() -> void:
	if get_node_or_null(^"DialogueManagerActor") != null:
		return
	var mgr := DialogueManagerActor.new()
	mgr.name = "DialogueManagerActor"
	add_child(mgr)

func _on_enemy_defeated(_npc: Node) -> void:
	_show_battle_result("胜利")

func _show_battle_result(text: String) -> void:
	var hud := get_node_or_null(^"UI/BattleHUD") as BattleHUD
	if hud != null:
		hud.show_result(text, 2.0)

# Customized load hook for procedurally generated rooms and loop-room redirects.
func _load_room(path: String) -> Node:
	return _map_room_loader.instantiate_room(path, Callable(self, "_instantiate_default_room"))

func _instantiate_default_room(effective: String) -> Node:
	return super._load_room(effective)
