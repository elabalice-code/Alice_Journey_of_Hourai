extends RefCounted
class_name CombatFlowIntent

const KIND_NONE: StringName = &"none"
const KIND_APPLY_DAMAGE: StringName = &"apply_damage"
const KIND_SYNC: StringName = &"sync"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> CombatFlowIntent:
	var intent := CombatFlowIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(false)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
