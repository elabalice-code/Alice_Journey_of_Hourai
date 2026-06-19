extends RefCounted
class_name AreaFlowIntent

const KIND_NONE: StringName = &"none"
const KIND_PREPARE_AREA_LOADED: StringName = &"prepare_area_loaded"
const KIND_SET_AREA_STATE: StringName = &"set_area_state"
const KIND_REQUEST_INPUT_MODE: StringName = &"request_input_mode"
const KIND_LOAD_ENTRY_ROOM: StringName = &"load_entry_room"
const KIND_RESET_MAP_STARTING_COORDS: StringName = &"reset_map_starting_coords"
const KIND_EMIT_AREA_LOADED: StringName = &"emit_area_loaded"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> AreaFlowIntent:
	var intent := AreaFlowIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
