extends RefCounted
class_name LevelEventManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [MessageTypes.TYPE_LEVEL_EVENT_REQUEST], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null or _workbench == null:
		return
	var msg: Dictionary = workplace.payload
	var ev: StringName = msg.get("event", &"")
	var room_id: String = str(msg.get("room", ""))
	if ev == &"":
		return
	_workbench.send({
		"type": MessageTypes.TYPE_LEVEL_EVENT,
		"event": ev,
		"room": room_id
	})
