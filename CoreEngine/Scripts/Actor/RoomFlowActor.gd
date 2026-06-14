extends RefCounted
class_name RoomFlowActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

var _workbench: WorkbenchService
var _pending_after_room_loaded: Dictionary = {}

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			ActorFramework.TYPE_LOAD_ROOM_REQUEST,
			ActorFramework.TYPE_RESET_MAP_STARTING_COORDS_REQUEST,
			ActorFramework.TYPE_SHIFT_PLAYER_REQUEST,
			ActorFramework.TYPE_SET_LOOP_TARGET,
			ActorFramework.TYPE_CLEAR_LOOP_TARGET,
			ActorFramework.TYPE_ROOM_LOADED,
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var msg: Dictionary = workplace.payload
	var t: StringName = workplace.type
	var game := _workbench.get_service(&"game") as Game
	if game == null:
		return
	
	match t:
		ActorFramework.TYPE_LOAD_ROOM_REQUEST:
			var target: String = str(msg.get("target_map", ""))
			if msg.get("after", null) is Dictionary:
				_pending_after_room_loaded = msg.get("after", {}) as Dictionary
			if not target.is_empty():
				var resolved := ResourceUID.uid_to_path(target) if target.begins_with("uid://") else target
				print("RoomFlowActor load_room_request target=%s resolved=%s current_room=%s current_path=%s" % [
					target,
					resolved,
					str(MetSys.get_current_room_id()),
					str(game.map.scene_file_path) if game.map != null else ""
				])
				game.load_room(target)
		ActorFramework.TYPE_RESET_MAP_STARTING_COORDS_REQUEST:
			game.reset_map_starting_coords()
		ActorFramework.TYPE_SHIFT_PLAYER_REQUEST:
			var delta: Vector2 = msg.get("delta", Vector2.ZERO) as Vector2
			if game.player != null:
				game.player.position += delta
		ActorFramework.TYPE_SET_LOOP_TARGET:
			game.loop = str(msg.get("loop_target", ""))
		ActorFramework.TYPE_CLEAR_LOOP_TARGET:
			game.loop = ""
		ActorFramework.TYPE_ROOM_LOADED:
			_apply_after_room_loaded(game)

func _apply_after_room_loaded(game: Game) -> void:
	if _pending_after_room_loaded.is_empty():
		return
	var after := _pending_after_room_loaded
	_pending_after_room_loaded = {}
	
	var action: StringName = after.get("action", &"")
	match action:
		&"move_player_to_node":
			var node_name: StringName = after.get("node", &"")
			var portal := game.map.get_node_or_null(NodePath(String(node_name))) as Node2D
			if portal != null and game.player != null:
				game.player.position = portal.position
			else:
				_move_player_to_matching_portal(game, after)
		&"move_player_to_matching_portal":
			_move_player_to_matching_portal(game, after)
		&"call_map_node_method":
			var node_name2: StringName = after.get("node", &"")
			var method_name: StringName = after.get("method", &"")
			var target := game.map.get_node_or_null(NodePath(String(node_name2)))
			if target != null and method_name != &"" and target.has_method(method_name):
				target.call(method_name)
	
	if after.has("clear_player_event_after_sec") and game.player != null:
		var delay: float = float(after.get("clear_player_event_after_sec", 0.0))
		if delay <= 0.0:
			game.player.set("event", false)
		else:
			game.get_tree().create_timer(delay).timeout.connect(game.player.set.bind(&"event", false))

func _move_player_to_matching_portal(game: Game, after: Dictionary) -> void:
	if game.player == null or game.map == null:
		return
	var from_room := str(after.get("from_room", ""))
	var fallback_node: StringName = after.get("fallback_node", &"") as StringName
	var found: Node2D = null
	if not from_room.is_empty():
		for n in game.map.find_children("*", "Node2D", true, false):
			if n is Node2D:
				var tm := _get_string_property(n, &"target_map")
				if tm == from_room:
					found = n as Node2D
					break
	if found == null and fallback_node != &"":
		found = game.map.get_node_or_null(NodePath(String(fallback_node))) as Node2D
	if found != null:
		game.player.position = found.position

func _get_string_property(obj: Object, prop: StringName) -> String:
	if obj == null:
		return ""
	for p in obj.get_property_list():
		if StringName(p.get("name", "")) == prop:
			return str(obj.get(String(prop)))
	return ""
