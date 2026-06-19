extends RefCounted
class_name GeneratedRoomFactory

const JunctionScene = preload("res://CoreEngine/Maps/Junction.tscn")

static func can_create(path: String) -> bool:
	return path.begins_with("GEN")

static func create(path: String) -> Node:
	if not can_create(path):
		return null
	var config := path.split("/")
	if config.size() < 4:
		return null
	var prototype := JunctionScene.instantiate()
	prototype.scene_file_path = path
	prototype.exits = config[2].to_int()
	prototype.has_collectible = config[3] == "true"
	prototype.apply_config()
	return prototype
