extends RefCounted
class_name RoomFlowProducers

const RoomFlowSignalFrameScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowSignalFrame.gd")

static func from_workplace(workplace) -> RoomFlowSignalFrame:
	if workplace == null:
		return RoomFlowSignalFrameScript.make(&"", {})
	return RoomFlowSignalFrameScript.make(workplace.type, workplace.payload)
