extends RefCounted
class_name CombatFlowProducers

const CombatFlowSignalFrameScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowSignalFrame.gd")

static func from_workplace(workplace) -> CombatFlowSignalFrame:
	if workplace == null:
		return CombatFlowSignalFrameScript.make(&"", {})
	return CombatFlowSignalFrameScript.make(workplace.type, workplace.payload)
