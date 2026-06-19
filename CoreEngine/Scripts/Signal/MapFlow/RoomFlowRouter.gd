extends RefCounted
class_name RoomFlowRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const RoomFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowIntent.gd")

static func route(frame: RoomFlowSignalFrame) -> RoomFlowIntent:
	if frame == null or not frame.is_valid():
		return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_NONE)
	var msg := frame.payload
	match frame.source_type:
		MessageTypes.TYPE_LOAD_ROOM_REQUEST:
			return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_LOAD_ROOM, {
				"target_map": str(msg.get("target_map", "")),
				"after": msg.get("after", {}) if msg.get("after", null) is Dictionary else {},
			})
		MessageTypes.TYPE_RESET_MAP_STARTING_COORDS_REQUEST:
			return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_RESET_MAP_STARTING_COORDS)
		MessageTypes.TYPE_SHIFT_PLAYER_REQUEST:
			return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_SHIFT_PLAYER, {
				"delta": msg.get("delta", Vector2.ZERO) as Vector2
			})
		MessageTypes.TYPE_SET_LOOP_TARGET:
			return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_SET_LOOP_TARGET, {
				"loop_target": str(msg.get("loop_target", ""))
			})
		MessageTypes.TYPE_CLEAR_LOOP_TARGET:
			return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_CLEAR_LOOP_TARGET)
		_:
			return RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_NONE)
