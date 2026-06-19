extends RefCounted
class_name RuntimeEventStateIntent

const KIND_NONE: StringName = &"none"
const KIND_MARK_FIRED: StringName = &"mark_fired"

var kind: StringName = KIND_NONE
var event_id: String = ""
var state: Dictionary = {}

static func make_mark_fired(p_event_id: String, p_state: Dictionary) -> RuntimeEventStateIntent:
	var intent := RuntimeEventStateIntent.new()
	intent.kind = KIND_MARK_FIRED
	intent.event_id = p_event_id
	intent.state = p_state.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE and not event_id.is_empty()
