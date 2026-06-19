extends RefCounted
class_name RuntimeEventActionIntent

const KIND_NONE: StringName = &"none"
const KIND_START_EVENT: StringName = &"start_event"
const KIND_LOAD_ROOM: StringName = &"load_room"
const KIND_SET_VARIABLE: StringName = &"set_variable"
const KIND_EMIT_SIGNAL: StringName = &"emit_signal"
const KIND_START_DIALOGUE: StringName = &"start_dialogue"
const KIND_LEVEL_EVENT: StringName = &"level_event"
const KIND_COMPLETE_QUEST: StringName = &"complete_quest"

var kind: StringName = KIND_NONE
var delay_ms: int = 0
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}, p_delay_ms: int = 0) -> RuntimeEventActionIntent:
	var intent := RuntimeEventActionIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	intent.delay_ms = maxi(0, p_delay_ms)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
