extends RefCounted
class_name MapPropertyIntent

const KIND_NONE: StringName = &"none"
const KIND_SET_PROPS: StringName = &"set_props"
const KIND_SET_RESOURCES: StringName = &"set_resources"
const KIND_SET_SHAPE: StringName = &"set_shape"
const KIND_REPLACE_SOURCE_ID: StringName = &"replace_source_id"
const KIND_REPLACE_TILE: StringName = &"replace_tile"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> MapPropertyIntent:
	var intent := MapPropertyIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
