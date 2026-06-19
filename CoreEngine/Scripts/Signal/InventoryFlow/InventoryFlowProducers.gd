extends RefCounted
class_name InventoryFlowProducers

const InventoryFlowSignalFrameScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowSignalFrame.gd")

static func from_workplace(workplace) -> InventoryFlowSignalFrame:
	if workplace == null:
		return InventoryFlowSignalFrameScript.make(&"", {})
	return InventoryFlowSignalFrameScript.make(workplace.type, workplace.payload)
