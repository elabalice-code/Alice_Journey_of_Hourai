extends RefCounted
class_name RoomFlowSignalFrame

var source_type: StringName = &""
var payload: Dictionary = {}

static func make(p_source_type: StringName, p_payload: Dictionary) -> RoomFlowSignalFrame:
	var frame := RoomFlowSignalFrame.new()
	frame.source_type = p_source_type
	frame.payload = p_payload.duplicate(true)
	return frame

func is_valid() -> bool:
	return source_type != &""
