extends RefCounted
class_name DialogueFlowIntent

const KIND_NONE: StringName = &"none"
const KIND_START_STORY: StringName = &"start_story"
const KIND_START_NPC: StringName = &"start_npc"
const KIND_END_ACTIVE: StringName = &"end_active"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> DialogueFlowIntent:
	var intent := DialogueFlowIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
