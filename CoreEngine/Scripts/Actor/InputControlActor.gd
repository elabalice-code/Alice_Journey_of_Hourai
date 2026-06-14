extends RefCounted
class_name InputControlActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

var _workbench: WorkbenchService
var _current_mode: StringName = &"side_scrolling"

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			ActorFramework.TYPE_INPUT_MODE_CHANGE_REQUEST
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	
	match t:
		ActorFramework.TYPE_INPUT_MODE_CHANGE_REQUEST:
			var new_mode: StringName = msg.get("mode", &"")
			if new_mode != &"" and new_mode != _current_mode:
				_current_mode = new_mode
				_broadcast_mode_change()

func _broadcast_mode_change() -> void:
	if _workbench:
		_workbench.send({
			"type": ActorFramework.TYPE_INPUT_MODE_CHANGED,
			"mode": _current_mode
		})
