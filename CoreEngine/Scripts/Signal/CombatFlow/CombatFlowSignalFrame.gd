extends RefCounted
class_name CombatFlowSignalFrame

var source_type: StringName = &""
var payload: Dictionary = {}

static func make(p_source_type: StringName, p_payload: Dictionary) -> CombatFlowSignalFrame:
	var frame := CombatFlowSignalFrame.new()
	frame.source_type = p_source_type
	frame.payload = p_payload.duplicate(false)
	return frame

func is_valid() -> bool:
	return source_type != &""
