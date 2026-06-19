extends RefCounted
class_name MapPropertyRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const RoomPropertyPlanScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/Featuror/RoomPropertyPlan.gd")

static func route(frame: MapPropertySignalFrame) -> Array[MapPropertyIntent]:
	var intents: Array[MapPropertyIntent] = []
	if frame == null or not frame.is_valid():
		return intents
	match frame.source_type:
		MessageTypes.TYPE_ROOM_LOADED:
			return RoomPropertyPlanScript.build_room_loaded_intents(frame.payload)
		_:
			return intents
