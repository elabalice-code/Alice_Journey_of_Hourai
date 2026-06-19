extends RefCounted
class_name RuntimeEventSignalFrame

var signal_name: String = ""
var source_domain: String = ""
var context: Dictionary = {}
var source_type: StringName = &""

static func make(p_signal_name: String, p_source_domain: String, p_context: Dictionary, p_source_type: StringName = &"") -> RuntimeEventSignalFrame:
	var frame := RuntimeEventSignalFrame.new()
	frame.signal_name = p_signal_name.strip_edges()
	frame.source_domain = p_source_domain.strip_edges()
	frame.context = p_context.duplicate(true)
	frame.source_type = p_source_type
	return frame

func is_valid() -> bool:
	return not signal_name.is_empty()

func to_dictionary() -> Dictionary:
	return {
		"signal": signal_name,
		"source_domain": source_domain,
		"source_type": source_type,
		"context": context,
	}
