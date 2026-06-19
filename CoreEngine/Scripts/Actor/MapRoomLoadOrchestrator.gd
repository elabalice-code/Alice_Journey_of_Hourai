extends RefCounted
class_name MapRoomLoadOrchestrator

const GeneratedRoomFactoryScript = preload("res://CoreEngine/Scripts/Actor/GeneratedRoomFactory.gd")

var loop_path: String = ""
var map_changing: bool = false

func load_room_with_progress(game, path: String, label_text: String, set_loading_visible: Callable, load_packed_scene_threaded: Callable) -> bool:
	if game == null or map_changing:
		return false
	map_changing = true
	game.map_changing = true
	var effective := path
	effective = consume_loop_path(effective)
	var resolved := ResourceUID.uid_to_path(effective) if effective.begins_with("uid://") else effective

	set_loading_visible.call(true, label_text, 0.0)
	await game.get_tree().process_frame

	if game.map:
		game.map.queue_free()
		await game.map.tree_exited
		game.map = null

	var new_map: Node2D = null
	if GeneratedRoomFactoryScript.can_create(effective):
		new_map = GeneratedRoomFactoryScript.create(effective) as Node2D
	else:
		var ps: PackedScene = await load_packed_scene_threaded.call(resolved, label_text)
		if ps != null:
			set_loading_visible.call(true, label_text, 1.0)
			await game.get_tree().process_frame
			new_map = ps.instantiate() as Node2D

	if new_map == null:
		map_changing = false
		game.map_changing = false
		return false

	game.map = new_map
	game.add_child(game.map)

	MetSys.current_layer = MetSys.get_current_room_instance().get_layer()
	map_changing = false
	game.map_changing = false
	game.room_loaded.emit()
	return true

func instantiate_room(path: String, default_loader: Callable) -> Node:
	var effective := consume_loop_path(path)
	if GeneratedRoomFactoryScript.can_create(effective):
		return GeneratedRoomFactoryScript.create(effective)
	return default_loader.call(effective) as Node

func consume_loop_path(path: String) -> String:
	if path.begins_with("GEN") or loop_path.is_empty():
		return path
	var effective := loop_path
	loop_path = ""
	return effective
