extends RefCounted
class_name RuntimeEventProducers

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const RuntimeEventSignalFrameScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/RuntimeEventSignalFrame.gd")

static func from_workplace(workplace) -> RuntimeEventSignalFrame:
	if workplace == null:
		return RuntimeEventSignalFrameScript.make("", "", {})
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	match t:
		MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL:
			return RuntimeEventSignalFrameScript.make(
				str(msg.get("signal", "")),
				str(msg.get("source_domain", "")),
				msg,
				t
			)
		MessageTypes.TYPE_ROOM_LOADED:
			return RuntimeEventSignalFrameScript.make("room_loaded", "Map", msg, t)
		MessageTypes.TYPE_AREA_LOADED:
			return RuntimeEventSignalFrameScript.make("area_loaded", "Map", msg, t)
		MessageTypes.TYPE_LEVEL_EVENT:
			return RuntimeEventSignalFrameScript.make(str(msg.get("event", "")), "Story", msg, t)
		_:
			return RuntimeEventSignalFrameScript.make("", "", msg, t)
