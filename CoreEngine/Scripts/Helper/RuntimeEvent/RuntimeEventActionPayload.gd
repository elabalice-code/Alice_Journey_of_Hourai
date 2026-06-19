extends RefCounted
class_name RuntimeEventActionPayload

static func parse(action: Dictionary) -> Dictionary:
	var raw := str(action.get("payloadJson", "")).strip_edges()
	if raw.is_empty():
		return {}
	var parsed = JSON.parse_string(raw)
	if parsed is Dictionary:
		return parsed
	return {}

static func get_string(payload: Dictionary, key: String, fallback: String = "") -> String:
	return str(payload.get(key, fallback)).strip_edges()

static func get_string_name(payload: Dictionary, key: String, fallback: String = "") -> StringName:
	return StringName(get_string(payload, key, fallback))

static func get_value(payload: Dictionary, key: String, fallback: Variant = null) -> Variant:
	return payload.get(key, fallback)

static func get_quest_id(payload: Dictionary) -> StringName:
	return StringName(str(payload.get("questId", payload.get("quest_id", ""))).strip_edges())
