extends RefCounted
class_name RuntimeEventStateOutput

const RuntimeEventStateIntentScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/RuntimeEventStateIntent.gd")

static func build_mark_fired_intent(event_id: String, event_dict: Dictionary) -> RuntimeEventStateIntent:
	if event_id.is_empty():
		return RuntimeEventStateIntent.new()
	var state := {
		"status": "fired",
		"title": str(event_dict.get("title", "")),
		"domain": str(event_dict.get("domain", "")),
		"one_shot": bool(event_dict.get("oneShot", false)),
	}
	return RuntimeEventStateIntentScript.make_mark_fired(event_id, state)
