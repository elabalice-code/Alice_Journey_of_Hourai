extends RefCounted
class_name MapPropertyProducers

const MapPropertySignalFrameScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertySignalFrame.gd")

static func from_workplace(workplace) -> MapPropertySignalFrame:
	if workplace == null:
		return MapPropertySignalFrameScript.make(&"", {})
	return MapPropertySignalFrameScript.make(workplace.type, workplace.payload)
