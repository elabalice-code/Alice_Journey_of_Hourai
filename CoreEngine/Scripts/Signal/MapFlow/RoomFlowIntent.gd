extends RefCounted
class_name RoomFlowIntent

const KIND_NONE: StringName = &"none"
const KIND_LOAD_ROOM: StringName = &"load_room"
const KIND_RESET_MAP_STARTING_COORDS: StringName = &"reset_map_starting_coords"
const KIND_SHIFT_PLAYER: StringName = &"shift_player"
const KIND_SET_LOOP_TARGET: StringName = &"set_loop_target"
const KIND_CLEAR_LOOP_TARGET: StringName = &"clear_loop_target"
const KIND_MOVE_PLAYER_TO_POSITION: StringName = &"move_player_to_position"
const KIND_MOVE_PLAYER_TO_NODE: StringName = &"move_player_to_node"
const KIND_CALL_MAP_NODE_METHOD: StringName = &"call_map_node_method"
const KIND_CLEAR_PLAYER_EVENT: StringName = &"clear_player_event"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> RoomFlowIntent:
	var intent := RoomFlowIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
