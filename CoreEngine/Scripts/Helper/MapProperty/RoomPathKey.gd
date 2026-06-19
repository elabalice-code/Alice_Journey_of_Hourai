extends RefCounted
class_name RoomPathKey

static func normalize(room_path: String) -> String:
	if room_path.begins_with("uid://"):
		var resolved := ResourceUID.uid_to_path(room_path)
		if not resolved.is_empty():
			return resolved
	return room_path
