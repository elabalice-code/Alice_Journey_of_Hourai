extends RefCounted
class_name RoomFlowIntentExecutor

const RoomFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowIntent.gd")

static func execute(game: Game, intent: RoomFlowIntent, pending_after: Dictionary) -> Dictionary:
	if game == null or intent == null or not intent.is_valid():
		return pending_after
	match intent.kind:
		RoomFlowIntentScript.KIND_LOAD_ROOM:
			return _execute_load_room(game, intent, pending_after)
		RoomFlowIntentScript.KIND_RESET_MAP_STARTING_COORDS:
			_reset_map_starting_coords(game)
		RoomFlowIntentScript.KIND_SHIFT_PLAYER:
			var delta: Vector2 = intent.payload.get("delta", Vector2.ZERO) as Vector2
			if game.player != null:
				game.player.position += delta
		RoomFlowIntentScript.KIND_SET_LOOP_TARGET:
			game.loop = str(intent.payload.get("loop_target", ""))
		RoomFlowIntentScript.KIND_CLEAR_LOOP_TARGET:
			game.loop = ""
		RoomFlowIntentScript.KIND_MOVE_PLAYER_TO_POSITION:
			if game.player != null:
				game.player.position = intent.payload.get("position", game.player.position) as Vector2
		RoomFlowIntentScript.KIND_CALL_MAP_NODE_METHOD:
			_call_map_node_method(game, intent)
		RoomFlowIntentScript.KIND_CLEAR_PLAYER_EVENT:
			_clear_player_event(game, intent)
	return pending_after

static func _reset_map_starting_coords(game: Game) -> void:
	var map_window := game.get_node_or_null(^"UI/MapWindow") if game != null else null
	if map_window != null and map_window.has_method("reset_starting_coords"):
		map_window.call("reset_starting_coords")

static func _execute_load_room(game: Game, intent: RoomFlowIntent, pending_after: Dictionary) -> Dictionary:
	var target: String = str(intent.payload.get("target_map", ""))
	var after: Variant = intent.payload.get("after", {})
	var next_after := pending_after
	if after is Dictionary and not after.is_empty():
		next_after = (after as Dictionary).duplicate(true)
	if not target.is_empty():
		var resolved := ResourceUID.uid_to_path(target) if target.begins_with("uid://") else target
		print("RoomFlowActor load_room_request target=%s resolved=%s current_room=%s current_path=%s" % [
			target,
			resolved,
			str(MetSys.get_current_room_id()),
			str(game.map.scene_file_path) if game.map != null else ""
		])
		game.load_room(target)
	return next_after

static func _call_map_node_method(game: Game, intent: RoomFlowIntent) -> void:
	var node_name: StringName = intent.payload.get("node", &"")
	var method_name: StringName = intent.payload.get("method", &"")
	var node_target: Node = game.map.get_node_or_null(NodePath(String(node_name))) if game.map != null else null
	if node_target != null and method_name != &"" and node_target.has_method(method_name):
		node_target.call(method_name)

static func _clear_player_event(game: Game, intent: RoomFlowIntent) -> void:
	if game.player == null:
		return
	var delay: float = float(intent.payload.get("delay_sec", 0.0))
	if delay <= 0.0:
		game.player.event = false
	else:
		game.get_tree().create_timer(delay).timeout.connect(_set_player_event.bind(game.player, false))

static func _set_player_event(player: Node, value: bool) -> void:
	if player != null:
		player.set(&"event", value)
