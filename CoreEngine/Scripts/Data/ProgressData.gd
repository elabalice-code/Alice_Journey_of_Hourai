extends RefCounted
class_name ProgressData

var save_count: int = 0
var last_save_time_ms: int = 0
var generated_rooms: Array[Vector3i] = []
var events: Array[StringName] = []

func mark_saved() -> void:
	save_count += 1
	last_save_time_ms = Time.get_ticks_msec()

func has_event(name: StringName) -> bool:
	return name in events

func add_event(name: StringName) -> void:
	if not has_event(name):
		events.append(name)
