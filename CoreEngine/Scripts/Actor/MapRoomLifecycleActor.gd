extends RefCounted
class_name MapRoomLifecycleActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

var in_random_level: bool = false

func reset() -> void:
	in_random_level = false

func publish_room_entered(workbench: WorkbenchService, map_root: Node2D, player: Node2D) -> void:
	var room_id := StringName(str(MetSys.get_current_room_id()))
	var room_path := _room_path(map_root)
	print("Game.init_room room_id=%s room_path=%s" % [str(room_id), room_path])
	if workbench != null:
		if is_instance_valid(player):
			workbench.set_service(&"player", player)
		workbench.send({
			"type": MessageTypes.TYPE_ROOM_LOADED,
			"room_id": room_id,
			"room_path": room_path
		})
	_publish_random_level_event_if_changed(workbench, room_path)
	_sync_initial_metsys_player_position(player)

func _publish_random_level_event_if_changed(workbench: WorkbenchService, room_path: String) -> void:
	var is_random := room_path.begins_with("GEN")
	if is_random == in_random_level:
		return
	if workbench != null:
		workbench.send({
			"type": MessageTypes.TYPE_LEVEL_EVENT_REQUEST,
			"event": &"enter_random_level" if is_random else &"exit_random_level",
			"room": room_path
		})
	in_random_level = is_random

func _sync_initial_metsys_player_position(player: Node2D) -> void:
	if not is_instance_valid(player):
		return
	if MetSys.last_player_position.x == Vector2i.MAX.x:
		MetSys.set_player_position(player.position)

func _room_path(map_root: Node) -> String:
	if map_root == null:
		return ""
	return str(map_root.scene_file_path)
