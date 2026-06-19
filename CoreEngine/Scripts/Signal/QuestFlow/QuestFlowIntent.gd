extends RefCounted
class_name QuestFlowIntent

const KIND_NONE: StringName = &"none"
const KIND_ACCEPT_QUEST: StringName = &"accept_quest"
const KIND_ADVANCE_QUEST: StringName = &"advance_quest"
const KIND_COMPLETE_QUEST: StringName = &"complete_quest"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> QuestFlowIntent:
	var intent := QuestFlowIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
