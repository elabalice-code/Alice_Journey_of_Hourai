extends RefCounted
class_name TestActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

const KEY_AGENT_DEBUG_ENABLED: StringName = &"agent_debug_enabled"
const KEY_AGENT_DEBUG_DIR: StringName = &"agent_debug_dir"
const KEY_AGENT_DEBUG_SNAPSHOT_PATH: StringName = &"agent_debug_snapshot_path"
const KEY_AGENT_DEBUG_COMMAND_PATH: StringName = &"agent_debug_command_path"
const KEY_AGENT_DEBUG_RESPONSE_PATH: StringName = &"agent_debug_response_path"
const KEY_AGENT_DEBUG_LOG_PATH: StringName = &"agent_debug_log_path"

var _workbench: WorkbenchService
var _enabled: bool = false
var _snapshot_interval_s: float = 0.35
var _poll_interval_s: float = 0.10
var _snapshot_elapsed: float = 0.0
var _poll_elapsed: float = 0.0
var _last_command_signature: String = ""
var _last_mouse_position: Vector2 = Vector2.ZERO
var _last_input_summary: String = ""
var _synthetic_input_count: int = 0

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench == null:
		return
	_apply_boot_settings()
	_workbench.register_actor(self, [
		MessageTypes.TYPE_ROOM_LOADED,
		MessageTypes.TYPE_AREA_LOADED,
		MessageTypes.TYPE_LEVEL_EVENT,
		MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
		MessageTypes.TYPE_RUNTIME_EVENT_ACTION,
		MessageTypes.TYPE_PLAYER_DATA_CHANGED,
		MessageTypes.TYPE_INVENTORY_UPDATED,
		MessageTypes.TYPE_QUEST_UPDATED,
		MessageTypes.TYPE_COMBAT_STATE_CHANGED,
		MessageTypes.TYPE_SAVE_COMPLETED,
	], &"_on_workplace")
	if not _workbench.tick.is_connected(_on_tick):
		_workbench.tick.connect(_on_tick)
	if _enabled:
		_log("AgentDebug 已启用")
		_log("snapshot=%s command=%s response=%s" % [
			_get_snapshot_path(),
			_get_command_path(),
			_get_response_path()
		])
		_write_snapshot("boot")

func _on_workplace(workplace) -> void:
	if not _enabled or workplace == null:
		return
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	match t:
		MessageTypes.TYPE_ROOM_LOADED:
			_log("room_loaded room_id=%s room_path=%s" % [str(msg.get("room_id", "")), str(msg.get("room_path", ""))])
			_write_snapshot("room_loaded")
		MessageTypes.TYPE_AREA_LOADED:
			_log("area_loaded area_id=%s entry_room=%s" % [str(msg.get("area_id", "")), str(msg.get("entry_room", ""))])
			_write_snapshot("area_loaded")
		MessageTypes.TYPE_LEVEL_EVENT:
			_log("level_event event=%s room=%s" % [str(msg.get("event", "")), str(msg.get("room", ""))])
		MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL:
			_log("runtime_event_signal signal=%s source=%s" % [str(msg.get("signal", "")), str(msg.get("source_domain", ""))])
		MessageTypes.TYPE_RUNTIME_EVENT_ACTION:
			_log("runtime_event_action event_id=%s" % [str(msg.get("event_id", ""))])
		MessageTypes.TYPE_PLAYER_DATA_CHANGED:
			_log("player_data_changed speed=[%s,%s] jump=%s" % [
				str(msg.get("speed_min", "")),
				str(msg.get("speed_max", "")),
				str(msg.get("jump_velocity", ""))
			])
		MessageTypes.TYPE_INVENTORY_UPDATED:
			_log("inventory_updated bag=%s equipped=%s" % [str(msg.get("bag_size", "")), str(msg.get("equipped_count", ""))])
		MessageTypes.TYPE_QUEST_UPDATED:
			_log("quest_updated quest_id=%s status=%s" % [str(msg.get("quest_id", "")), str(msg.get("status", ""))])
		MessageTypes.TYPE_COMBAT_STATE_CHANGED:
			_log("combat_state_changed hp=%s max_hp=%s" % [str(msg.get("hp", "")), str(msg.get("max_hp", ""))])
		MessageTypes.TYPE_SAVE_COMPLETED:
			_log("save_completed reason=%s" % [str(msg.get("reason", ""))])

func _on_tick(delta: float) -> void:
	if not _enabled:
		return
	_snapshot_elapsed += delta
	_poll_elapsed += delta
	if _poll_elapsed >= _poll_interval_s:
		_poll_elapsed = 0.0
		_process_command_file()
	if _snapshot_elapsed >= _snapshot_interval_s:
		_snapshot_elapsed = 0.0
		_write_snapshot("tick")

func _apply_boot_settings() -> void:
	var args: PackedStringArray = OS.get_cmdline_user_args()
	var enabled: bool = bool(_workbench.get_workplace_data(KEY_AGENT_DEBUG_ENABLED, false))
	if not enabled:
		enabled = _has_flag(args, "--agent-debug") or _is_truthy(OS.get_environment("AGENT_DEBUG"))
	_workbench.register_workplace_data(KEY_AGENT_DEBUG_ENABLED, enabled)
	_enabled = enabled
	var base_dir: String = str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_DIR, "")).strip_edges()
	if base_dir.is_empty():
		base_dir = _get_option(args, "--agent-debug-dir", OS.get_environment("AGENT_DEBUG_DIR"))
	if base_dir.is_empty():
		base_dir = "user://agent_debug"
	_workbench.register_workplace_data(KEY_AGENT_DEBUG_DIR, base_dir)
	var snapshot_path: String = str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_SNAPSHOT_PATH, "")).strip_edges()
	if snapshot_path.is_empty():
		snapshot_path = base_dir.path_join("snapshot.json")
	_workbench.register_workplace_data(KEY_AGENT_DEBUG_SNAPSHOT_PATH, snapshot_path)
	var command_path: String = str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_COMMAND_PATH, "")).strip_edges()
	if command_path.is_empty():
		command_path = base_dir.path_join("command.json")
	_workbench.register_workplace_data(KEY_AGENT_DEBUG_COMMAND_PATH, command_path)
	var response_path: String = str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_RESPONSE_PATH, "")).strip_edges()
	if response_path.is_empty():
		response_path = base_dir.path_join("response.json")
	_workbench.register_workplace_data(KEY_AGENT_DEBUG_RESPONSE_PATH, response_path)
	var log_path: String = str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_LOG_PATH, "")).strip_edges()
	if log_path.is_empty():
		log_path = base_dir.path_join("testactor.log")
	_workbench.register_workplace_data(KEY_AGENT_DEBUG_LOG_PATH, log_path)
	if _enabled:
		_ensure_parent_dir(snapshot_path)
		_ensure_parent_dir(command_path)
		_ensure_parent_dir(response_path)
		_ensure_parent_dir(log_path)

func _process_command_file() -> void:
	var command_path: String = _get_command_path()
	if command_path.is_empty() or not FileAccess.file_exists(command_path):
		return
	var file: FileAccess = FileAccess.open(command_path, FileAccess.READ)
	if file == null:
		return
	var raw: String = file.get_as_text().strip_edges()
	if raw.is_empty() or raw == _last_command_signature:
		return
	_last_command_signature = raw
	var parsed = JSON.parse_string(raw)
	if not (parsed is Dictionary):
		_write_response({
			"success": false,
			"message": "invalid command payload",
			"received_at": Time.get_datetime_string_from_system(true, true)
		})
		_log("command invalid_payload")
		return
	var result: Dictionary = await _execute_command(parsed as Dictionary)
	result["received_at"] = Time.get_datetime_string_from_system(true, true)
	result["snapshot_path"] = ProjectSettings.globalize_path(_get_snapshot_path())
	_write_response(result)
	_write_snapshot("command:" + str(result.get("action", "")))

func _execute_command(command: Dictionary) -> Dictionary:
	if command.has("commands") and command.get("commands") is Array:
		return await _command_sequence("sequence", command)
	var action: String = str(command.get("action", "")).strip_edges()
	if action.is_empty():
		_log("command missing_action")
		return {
			"success": false,
			"action": "",
			"message": "missing action"
		}
	_log("command action=%s" % [action])
	match action:
		"sequence":
			return await _command_sequence(action, command)
		"snapshot":
			return {
				"success": true,
				"action": action,
				"message": "snapshot updated"
			}
		"load_room":
			var target_map: String = str(command.get("target_map", "")).strip_edges()
			if target_map.is_empty():
				return _fail_command(action, "target_map is empty")
			_workbench.send({
				"type": MessageTypes.TYPE_LOAD_ROOM_REQUEST,
				"target_map": target_map
			})
			return _ok_command(action, "load_room queued")
		"start_runtime_event":
			var event_id: String = str(command.get("event_id", "")).strip_edges()
			_workbench.send({
				"type": MessageTypes.TYPE_RUNTIME_EVENT_START,
				"event_id": event_id
			})
			return _ok_command(action, "runtime event queued")
		"emit_runtime_signal":
			var signal_name: String = str(command.get("signal", "")).strip_edges()
			if signal_name.is_empty():
				return _fail_command(action, "signal is empty")
			_workbench.send({
				"type": MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
				"signal": signal_name,
				"source_domain": str(command.get("source_domain", "Agent")),
				"context": command.get("context", {})
			})
			return _ok_command(action, "runtime signal queued")
		"teleport_player":
			return _command_teleport_player(action, command)
		"move_player_to_node":
			return _command_move_player_to_node(action, command)
		"sleep":
			return await _command_sleep(action, command)
		"wait_for":
			return await _command_wait_for(action, command)
		"click_control":
			return _command_click_control(action, command)
		"advance_prologue":
			return await _command_advance_prologue(action, command)
		"mouse_move":
			return _command_mouse_move(action, command)
		"mouse_button":
			return _command_mouse_button(action, command)
		"mouse_click":
			return _command_mouse_click(action, command)
		"key_press":
			return _command_key_event(action, command, true)
		"key_release":
			return _command_key_event(action, command, false)
		"key_tap":
			return _command_key_tap(action, command)
		"text_input":
			return _command_text_input(action, command)
		"set_runtime_event_project":
			var path: String = str(command.get("path", "")).strip_edges()
			if path.is_empty():
				return _fail_command(action, "path is empty")
			_workbench.register_workplace_data(&"runtime_event_project_path", path)
			var runtime_flow := _workbench.get_service(&"runtime_event_flow") as RuntimeEventFlowActor
			if runtime_flow != null:
				runtime_flow.reload_project()
			return _ok_command(action, "runtime event project updated")
		"set_workplace_data":
			var key: StringName = StringName(str(command.get("key", "")).strip_edges())
			if key == &"":
				return _fail_command(action, "key is empty")
			_workbench.register_workplace_data(key, command.get("value", null))
			return _ok_command(action, "workplace data updated")
		_:
			return _fail_command(action, "unsupported action")

func _command_teleport_player(action: String, command: Dictionary) -> Dictionary:
	var player: Node2D = _workbench.get_service(&"player") as Node2D
	if player == null:
		return _fail_command(action, "player not found")
	var target: Vector2 = Vector2(float(command.get("x", player.position.x)), float(command.get("y", player.position.y)))
	var delta: Vector2 = target - player.position
	_workbench.send({
		"type": MessageTypes.TYPE_SHIFT_PLAYER_REQUEST,
		"delta": delta
	})
	if _has_property(player, &"velocity"):
		player.set("velocity", Vector2.ZERO)
	return _ok_command(action, "player shifted")

func _command_move_player_to_node(action: String, command: Dictionary) -> Dictionary:
	var game: Game = _workbench.get_service(&"game") as Game
	if game == null or game.map == null or game.player == null:
		return _fail_command(action, "game or player not ready")
	var node_path: String = str(command.get("node_path", "")).strip_edges()
	var node_name: String = str(command.get("node", "")).strip_edges()
	var target: Node2D = null
	if not node_path.is_empty():
		target = game.map.get_node_or_null(NodePath(node_path)) as Node2D
	elif not node_name.is_empty():
		target = game.map.find_child(node_name, true, false) as Node2D
	if target == null:
		return _fail_command(action, "target node not found")
	var delta: Vector2 = target.global_position - game.player.global_position
	_workbench.send({
		"type": MessageTypes.TYPE_SHIFT_PLAYER_REQUEST,
		"delta": delta
	})
	if _has_property(game.player, &"velocity"):
		game.player.set("velocity", Vector2.ZERO)
	return _ok_command(action, "player moved to node")

func _command_sequence(action: String, command: Dictionary) -> Dictionary:
	var commands: Array = command.get("commands", [])
	if commands.is_empty():
		return _fail_command(action, "commands is empty")
	var last_result: Dictionary = {}
	for index in range(commands.size()):
		var item_any: Variant = commands[index]
		if not (item_any is Dictionary):
			return _fail_command(action, "command[%s] is not an object" % [index])
		var item: Dictionary = item_any as Dictionary
		var delay_ms: int = int(item.get("delay_ms", 0))
		if delay_ms > 0:
			await _workbench.get_tree().create_timer(float(delay_ms) / 1000.0).timeout
		last_result = await _execute_command(item)
		if not bool(last_result.get("success", false)):
			last_result["action"] = action
			last_result["message"] = "sequence failed at index %s: %s" % [index, str(last_result.get("message", ""))]
			return last_result
	return _ok_command(action, "sequence completed (%s steps)" % [commands.size()])

func _command_sleep(action: String, command: Dictionary) -> Dictionary:
	var delay_ms: int = int(command.get("delay_ms", command.get("ms", 0)))
	if delay_ms < 0:
		delay_ms = 0
	if delay_ms > 0:
		await _workbench.get_tree().create_timer(float(delay_ms) / 1000.0).timeout
	return _ok_command(action, "slept %sms" % [delay_ms])

func _command_wait_for(action: String, command: Dictionary) -> Dictionary:
	var path: String = str(command.get("path", "")).strip_edges()
	if path.is_empty():
		return _fail_command(action, "path is empty")
	var expected: Variant = command.get("equals", null)
	var not_equals: Variant = command.get("not_equals", null)
	var timeout_ms: int = max(1, int(command.get("timeout_ms", 3000)))
	var poll_ms: int = max(10, int(command.get("poll_ms", 50)))
	var deadline: int = Time.get_ticks_msec() + timeout_ms
	while Time.get_ticks_msec() <= deadline:
		var snapshot: Dictionary = _build_snapshot("wait_for")
		var current: Variant = _read_snapshot_value(snapshot, path)
		var matched: bool = false
		if command.has("equals"):
			matched = _variant_equals(current, expected)
		elif command.has("not_equals"):
			matched = not _variant_equals(current, not_equals)
		elif current != null:
			matched = true
		if matched:
			return _ok_command(action, "condition met for %s" % [path])
		await _workbench.get_tree().create_timer(float(poll_ms) / 1000.0).timeout
	return _fail_command(action, "timeout waiting for %s" % [path])

func _command_click_control(action: String, command: Dictionary) -> Dictionary:
	var control: Control = _resolve_control(command)
	if control == null:
		return _fail_command(action, "control not found")
	if not control.is_visible_in_tree():
		return _fail_command(action, "control is not visible")
	var rect: Rect2 = control.get_global_rect()
	var center: Vector2 = rect.position + rect.size * 0.5
	var click_command: Dictionary = {
		"x": center.x,
		"y": center.y,
		"button": command.get("button", "left")
	}
	return _command_mouse_click(action, click_command)

func _command_advance_prologue(action: String, command: Dictionary) -> Dictionary:
	var delay_ms: int = max(50, int(command.get("delay_ms", 500)))
	var timeout_ms: int = max(delay_ms, int(command.get("timeout_ms", 30000)))
	var deadline: int = Time.get_ticks_msec() + timeout_ms
	while Time.get_ticks_msec() <= deadline:
		var snapshot: Dictionary = _build_snapshot("advance_prologue")
		if not bool(_read_snapshot_value(snapshot, "ui.prologue_visible")):
			return _ok_command(action, "prologue completed")
		if bool(_read_snapshot_value(snapshot, "ui.next_button_visible")):
			var click_result: Dictionary = _command_click_control(action, {
				"node": str(command.get("node", "NextButton")),
				"button": command.get("button", "left")
			})
			if not bool(click_result.get("success", false)):
				return click_result
		await _workbench.get_tree().create_timer(float(delay_ms) / 1000.0).timeout
	return _fail_command(action, "timeout advancing prologue")

func _command_mouse_move(action: String, command: Dictionary) -> Dictionary:
	var position: Vector2 = _resolve_pointer_position(command)
	var event: InputEventMouseMotion = InputEventMouseMotion.new()
	event.position = position
	event.global_position = position
	event.relative = position - _get_pointer_position()
	event.velocity = event.relative / maxf(_poll_interval_s, 0.001)
	_last_mouse_position = position
	if _dispatch_input_event(event, "mouse_move pos=%s,%s" % [position.x, position.y], true):
		return _ok_command(action, "mouse move dispatched")
	return _fail_command(action, "viewport not available")

func _command_mouse_button(action: String, command: Dictionary) -> Dictionary:
	var position: Vector2 = _resolve_pointer_position(command)
	var button_index: int = _resolve_mouse_button(command)
	if button_index <= 0:
		return _fail_command(action, "invalid mouse button")
	var pressed: bool = bool(command.get("pressed", true))
	var event: InputEventMouseButton = InputEventMouseButton.new()
	event.position = position
	event.global_position = position
	event.button_index = button_index
	event.pressed = pressed
	event.double_click = bool(command.get("double_click", false))
	_last_mouse_position = position
	if _dispatch_input_event(event, "mouse_button button=%s pressed=%s pos=%s,%s" % [button_index, pressed, position.x, position.y], true):
		return _ok_command(action, "mouse button dispatched")
	return _fail_command(action, "viewport not available")

func _command_mouse_click(action: String, command: Dictionary) -> Dictionary:
	var position: Vector2 = _resolve_pointer_position(command)
	var button_index: int = _resolve_mouse_button(command)
	if button_index <= 0:
		return _fail_command(action, "invalid mouse button")
	var press_event: InputEventMouseButton = InputEventMouseButton.new()
	press_event.position = position
	press_event.global_position = position
	press_event.button_index = button_index
	press_event.pressed = true
	var release_event: InputEventMouseButton = InputEventMouseButton.new()
	release_event.position = position
	release_event.global_position = position
	release_event.button_index = button_index
	release_event.pressed = false
	_last_mouse_position = position
	if not _dispatch_input_event(press_event, "mouse_click_press button=%s pos=%s,%s" % [button_index, position.x, position.y], true):
		return _fail_command(action, "viewport not available")
	_dispatch_input_event(release_event, "mouse_click_release button=%s pos=%s,%s" % [button_index, position.x, position.y], true)
	return _ok_command(action, "mouse click dispatched")

func _command_key_event(action: String, command: Dictionary, pressed: bool) -> Dictionary:
	var event: InputEventKey = _build_key_event(command, pressed)
	if event == null:
		return _fail_command(action, "invalid key")
	if _dispatch_input_event(event, "key_event key=%s pressed=%s" % [_describe_key(command), pressed], false):
		return _ok_command(action, "key event dispatched")
	return _fail_command(action, "viewport not available")

func _command_key_tap(action: String, command: Dictionary) -> Dictionary:
	var press_event: InputEventKey = _build_key_event(command, true)
	var release_event: InputEventKey = _build_key_event(command, false)
	if press_event == null or release_event == null:
		return _fail_command(action, "invalid key")
	if not _dispatch_input_event(press_event, "key_tap_press key=%s" % [_describe_key(command)], false):
		return _fail_command(action, "viewport not available")
	_dispatch_input_event(release_event, "key_tap_release key=%s" % [_describe_key(command)], false)
	return _ok_command(action, "key tap dispatched")

func _command_text_input(action: String, command: Dictionary) -> Dictionary:
	var text: String = str(command.get("text", ""))
	if text.is_empty():
		return _fail_command(action, "text is empty")
	for i in range(text.length()):
		var character: String = text.substr(i, 1)
		var key_event: InputEventKey = InputEventKey.new()
		key_event.pressed = true
		key_event.keycode = OS.find_keycode_from_string(character)
		key_event.unicode = character.unicode_at(0)
		if not _dispatch_input_event(key_event, "text_input char=%s" % [character], false):
			return _fail_command(action, "viewport not available")
		var release_event: InputEventKey = InputEventKey.new()
		release_event.pressed = false
		release_event.keycode = key_event.keycode
		release_event.unicode = key_event.unicode
		_dispatch_input_event(release_event, "text_input_release char=%s" % [character], false)
	return _ok_command(action, "text input dispatched")

func _write_snapshot(reason: String) -> void:
	var snapshot: Dictionary = _build_snapshot(reason)
	var path: String = _get_snapshot_path()
	_ensure_parent_dir(path)
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return
	file.store_string(JSON.stringify(snapshot, "\t"))

func _build_snapshot(reason: String) -> Dictionary:
	var game: Game = _workbench.get_service(&"game") as Game
	var player: Node2D = _workbench.get_service(&"player") as Node2D
	var wp: WorkPlace = _workbench.get_workplace()
	return {
		"source": "TestActor",
		"reason": reason,
		"recorded_at": Time.get_datetime_string_from_system(true, true),
		"room": _capture_room(game),
		"player": _capture_player(player),
		"input": _capture_input_state(),
		"dialogue": _capture_dialogue_state(),
		"ui": _capture_ui_state(),
		"map": _capture_map(game),
		"portals": _capture_portals(game),
		"collision_areas": _capture_collision_areas(game),
		"workplace": _capture_workplace(wp)
	}

func _capture_room(game: Game) -> Dictionary:
	var room_path: String = ""
	if game != null and game.map != null:
		room_path = str(game.map.scene_file_path)
	return {
		"id": str(MetSys.get_current_room_id()),
		"path": room_path,
		"coords": _vector3i_to_dict(MetSys.get_current_coords()),
		"current_area_id": str(_workbench.get_workplace_data(&"current_area_id", &"")),
		"input_mode": str(_workbench.get_workplace_data(&"input_mode", ""))
	}

func _capture_player(player: Node2D) -> Dictionary:
	if player == null:
		return {}
	var data: Dictionary = {
		"name": player.name,
		"path": str(player.get_path()),
		"position": _vector2_to_dict(player.global_position),
		"groups": _groups_to_strings(player.get_groups()),
		"abilities": player.get("abilities") if _has_property(player, &"abilities") else [],
		"is_transferred": bool(player.get_meta(&"IsTransferred", false))
	}
	if _has_property(player, &"velocity"):
		data["velocity"] = _vector2_to_dict(player.get("velocity") as Vector2)
	return data

func _capture_dialogue_state() -> Dictionary:
	var manager := _workbench.get_tree().root.find_child("DialogueManagerActor", true, false) as DialogueManagerActor
	if manager == null:
		return {}
	return {
		"is_open": manager.is_dialogue_open(),
		"active_mode": str(manager.get("_active_mode")) if _has_property(manager, &"_active_mode") else "",
		"active_dialogue_id": str(manager.get("_active_npc_id")) if _has_property(manager, &"_active_npc_id") else ""
	}

func _capture_input_state() -> Dictionary:
	return {
		"mouse_position": _vector2_to_dict(_get_pointer_position()),
		"last_input_summary": _last_input_summary,
		"synthetic_input_count": _synthetic_input_count
	}

func _capture_ui_state() -> Dictionary:
	var root: Viewport = _get_root_viewport()
	var focused: Control = null
	if root != null:
		focused = root.gui_get_focus_owner()
	var title_menu: CanvasItem = _find_canvas_item("TitleMenu")
	var prologue_screen: CanvasItem = _find_canvas_item("PrologueScreen")
	var next_button: CanvasItem = _find_canvas_item("NextButton")
	var skip_button: CanvasItem = _find_canvas_item("SkipButton")
	var start_button: CanvasItem = _find_canvas_item("StartButton")
	return {
		"focused_control": str(focused.name) if focused != null else "",
		"title_menu_visible": title_menu != null and title_menu.is_visible_in_tree(),
		"prologue_visible": prologue_screen != null and prologue_screen.is_visible_in_tree(),
		"next_button_visible": next_button != null and next_button.is_visible_in_tree(),
		"skip_button_visible": skip_button != null and skip_button.is_visible_in_tree(),
		"start_button_visible": start_button != null and start_button.is_visible_in_tree()
	}

func _capture_map(game: Game) -> Dictionary:
	if game == null or game.map == null:
		return {}
	return {
		"name": game.map.name,
		"path": str(game.map.get_path()),
		"scene_file_path": str(game.map.scene_file_path),
		"child_count": game.map.get_child_count(),
		"node_count": _count_nodes(game.map, 256)
	}

func _capture_portals(game: Game) -> Array:
	var out: Array = []
	if game == null or game.map == null:
		return out
	for node in game.map.find_children("*", "Area2D", true, false):
		if out.size() >= 24:
			break
		if not (node is Node2D):
			continue
		if not _has_property(node, &"target_map"):
			continue
		out.append({
			"name": node.name,
			"path": str(node.get_path()),
			"position": _vector2_to_dict((node as Node2D).global_position),
			"target_map": str(node.get("target_map")),
			"target_area": str(node.get("target_area")) if _has_property(node, &"target_area") else "",
			"target_entry_node": str(node.get("target_entry_node")) if _has_property(node, &"target_entry_node") else ""
		})
	return out

func _capture_collision_areas(game: Game) -> Array:
	var out: Array = []
	if game == null or game.map == null:
		return out
	for node in game.map.find_children("*", "Area2D", true, false):
		if out.size() >= 40:
			break
		if not (node is Area2D):
			continue
		var area: Area2D = node as Area2D
		out.append({
			"name": area.name,
			"path": str(area.get_path()),
			"position": _vector2_to_dict(area.global_position),
			"groups": _groups_to_strings(area.get_groups()),
			"monitoring": area.monitoring,
			"monitorable": area.monitorable,
			"collision_layer": area.collision_layer,
			"collision_mask": area.collision_mask
		})
	return out

func _capture_workplace(wp: WorkPlace) -> Dictionary:
	if wp == null:
		return {}
	return {
		"player": {
			"speed_min": wp.player.speed_min,
			"speed_max": wp.player.speed_max,
			"jump_velocity": wp.player.jump_velocity
		},
		"combat": {
			"hp": wp.combat.hp,
			"max_hp": wp.combat.max_hp,
			"defense": wp.combat.defense,
			"attack_power": wp.combat.attack_power,
			"status_effects": _stringify_array(wp.combat.status_effects)
		},
		"inventory": {
			"bag_size": wp.inventory.bag.size(),
			"equipped_slots": wp.inventory.equipped.keys().size(),
			"rune_count": wp.inventory.runes.size()
		},
		"quest": {
			"active_quests": wp.quest.active_quests.keys(),
			"completed_quests": _stringify_array(wp.quest.completed_quests),
			"dialogue_history_count": wp.quest.dialogue_history.size()
		},
		"data_keys": _stringify_array(wp.data.keys())
	}

func _write_response(payload: Dictionary) -> void:
	var path: String = _get_response_path()
	_ensure_parent_dir(path)
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return
	file.store_string(JSON.stringify(payload, "\t"))

func _ok_command(action: String, message: String) -> Dictionary:
	_log("command_ok action=%s message=%s" % [action, message])
	return {
		"success": true,
		"action": action,
		"message": message
	}

func _fail_command(action: String, message: String) -> Dictionary:
	_log("command_fail action=%s message=%s" % [action, message])
	return {
		"success": false,
		"action": action,
		"message": message
	}

func _log(message: String) -> void:
	var line: String = "[TestActor] %s %s" % [Time.get_datetime_string_from_system(true, true), message]
	print(line)
	var path: String = _get_log_path()
	if path.is_empty():
		return
	_ensure_parent_dir(path)
	var file: FileAccess = FileAccess.open(path, FileAccess.READ_WRITE)
	if file == null:
		return
	file.seek_end()
	file.store_line(line)

func _get_snapshot_path() -> String:
	return str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_SNAPSHOT_PATH, ""))

func _get_command_path() -> String:
	return str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_COMMAND_PATH, ""))

func _get_response_path() -> String:
	return str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_RESPONSE_PATH, ""))

func _get_log_path() -> String:
	return str(_workbench.get_workplace_data(KEY_AGENT_DEBUG_LOG_PATH, ""))

func _ensure_parent_dir(path: String) -> void:
	var absolute: String = ProjectSettings.globalize_path(path)
	var base_dir: String = absolute.get_base_dir()
	if base_dir.is_empty():
		return
	DirAccess.make_dir_recursive_absolute(base_dir)

func _has_flag(args: PackedStringArray, flag: String) -> bool:
	for arg in args:
		if arg == flag:
			return true
	return false

func _get_option(args: PackedStringArray, name: String, fallback: String) -> String:
	for i in range(args.size()):
		var arg: String = args[i]
		if arg == name and i + 1 < args.size():
			return str(args[i + 1]).strip_edges()
		if arg.begins_with(name + "="):
			return arg.substr(name.length() + 1).strip_edges()
	if not fallback.is_empty():
		return fallback.strip_edges()
	return ""

func _is_truthy(value: String) -> bool:
	var text: String = value.strip_edges().to_lower()
	return text in ["1", "true", "yes", "on"]

func _has_property(obj: Object, property_name: StringName) -> bool:
	if obj == null:
		return false
	for info in obj.get_property_list():
		if StringName(info.get("name", "")) == property_name:
			return true
	return false

func _vector2_to_dict(value: Vector2) -> Dictionary:
	return {"x": value.x, "y": value.y}

func _vector3i_to_dict(value: Vector3i) -> Dictionary:
	return {"x": value.x, "y": value.y, "z": value.z}

func _groups_to_strings(groups: Array) -> Array[String]:
	var out: Array[String] = []
	for g in groups:
		out.append(str(g))
	return out

func _stringify_array(input: Array) -> Array[String]:
	var out: Array[String] = []
	for item in input:
		out.append(str(item))
	return out

func _count_nodes(root: Node, limit: int) -> int:
	if root == null:
		return 0
	var count: int = 0
	var stack: Array[Node] = [root]
	while not stack.is_empty() and count < limit:
		var node: Node = stack.pop_back()
		count += 1
		for child in node.get_children():
			if child is Node:
				stack.append(child)
	return count

func _find_node_by_hint(hint: String) -> Node:
	if _workbench == null:
		return null
	var tree: SceneTree = _workbench.get_tree()
	if tree == null or tree.root == null or hint.is_empty():
		return null
	var by_path: Node = tree.root.get_node_or_null(NodePath(hint))
	if by_path != null:
		return by_path
	return tree.root.find_child(hint, true, false)

func _find_canvas_item(hint: String) -> CanvasItem:
	var node: Node = _find_node_by_hint(hint)
	return node as CanvasItem

func _resolve_control(command: Dictionary) -> Control:
	var node_path: String = str(command.get("node_path", command.get("path", ""))).strip_edges()
	if not node_path.is_empty():
		return _find_node_by_hint(node_path) as Control
	var node_name: String = str(command.get("node", "")).strip_edges()
	if not node_name.is_empty():
		return _find_node_by_hint(node_name) as Control
	return null

func _read_snapshot_value(snapshot: Dictionary, path: String) -> Variant:
	var current: Variant = snapshot
	for part in path.split(".", false):
		if current is Dictionary:
			var dict: Dictionary = current as Dictionary
			if not dict.has(part):
				return null
			current = dict.get(part)
			continue
		return null
	return current

func _variant_equals(left: Variant, right: Variant) -> bool:
	if typeof(left) == TYPE_BOOL or typeof(right) == TYPE_BOOL:
		return bool(left) == bool(right)
	return str(left) == str(right)

func _get_root_viewport() -> Viewport:
	if _workbench == null:
		return null
	var tree: SceneTree = _workbench.get_tree()
	if tree == null:
		return null
	return tree.root

func _get_pointer_position() -> Vector2:
	var viewport: Viewport = _get_root_viewport()
	if viewport != null:
		var current: Vector2 = viewport.get_mouse_position()
		if current != Vector2.ZERO or _last_mouse_position == Vector2.ZERO:
			return current
	return _last_mouse_position

func _resolve_pointer_position(command: Dictionary) -> Vector2:
	if command.has("position") and command.get("position") is Dictionary:
		var position_dict: Dictionary = command.get("position", {}) as Dictionary
		return Vector2(float(position_dict.get("x", _get_pointer_position().x)), float(position_dict.get("y", _get_pointer_position().y)))
	return Vector2(float(command.get("x", _get_pointer_position().x)), float(command.get("y", _get_pointer_position().y)))

func _resolve_mouse_button(command: Dictionary) -> int:
	var raw_button: Variant = command.get("button", command.get("button_index", "left"))
	if raw_button is int:
		return int(raw_button)
	var name: String = str(raw_button).strip_edges().to_lower()
	match name:
		"", "left", "lmb", "1":
			return MOUSE_BUTTON_LEFT
		"right", "rmb", "2":
			return MOUSE_BUTTON_RIGHT
		"middle", "mmb", "3":
			return MOUSE_BUTTON_MIDDLE
		"wheel_up", "4":
			return MOUSE_BUTTON_WHEEL_UP
		"wheel_down", "5":
			return MOUSE_BUTTON_WHEEL_DOWN
		_:
			return 0

func _build_key_event(command: Dictionary, pressed: bool) -> InputEventKey:
	var keycode: int = _resolve_keycode(command)
	if keycode <= 0:
		return null
	var event: InputEventKey = InputEventKey.new()
	event.pressed = pressed
	event.keycode = keycode
	event.physical_keycode = int(command.get("physical_keycode", keycode))
	if command.has("unicode"):
		event.unicode = int(command.get("unicode", 0))
	elif command.has("text"):
		var text: String = str(command.get("text", ""))
		if not text.is_empty():
			event.unicode = text.unicode_at(0)
	return event

func _resolve_keycode(command: Dictionary) -> int:
	if command.has("keycode"):
		return int(command.get("keycode", 0))
	if command.has("unicode"):
		return int(command.get("unicode", 0))
	var raw_key: String = str(command.get("key", command.get("text", ""))).strip_edges()
	if raw_key.is_empty():
		return 0
	if raw_key.length() == 1:
		return OS.find_keycode_from_string(raw_key)
	return OS.find_keycode_from_string(raw_key.to_upper())

func _describe_key(command: Dictionary) -> String:
	if command.has("key"):
		return str(command.get("key", ""))
	if command.has("text"):
		return str(command.get("text", ""))
	if command.has("keycode"):
		return str(command.get("keycode", 0))
	return ""

func _dispatch_input_event(event: InputEvent, summary: String, update_pointer: bool) -> bool:
	var viewport: Viewport = _get_root_viewport()
	if viewport == null:
		return false
	if update_pointer and event is InputEventFromWindow:
		var window_event: InputEventFromWindow = event as InputEventFromWindow
		viewport.warp_mouse(window_event.position)
	Input.parse_input_event(event)
	_synthetic_input_count += 1
	_last_input_summary = summary
	_log("synthetic_input %s" % [summary])
	return true
