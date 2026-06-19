extends RefCounted
class_name AreaFlowProducers

const AreaFlowSignalFrameScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/AreaFlowSignalFrame.gd")

static func from_workplace(workplace) -> AreaFlowSignalFrame:
	if workplace == null:
		return AreaFlowSignalFrameScript.make(&"", {})
	return AreaFlowSignalFrameScript.make(workplace.type, workplace.payload)
