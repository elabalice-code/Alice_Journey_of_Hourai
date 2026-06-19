extends RefCounted
class_name AudioManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			MessageTypes.TYPE_AUDIO_REQUEST
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	
	match t:
		MessageTypes.TYPE_AUDIO_REQUEST:
			var action: StringName = msg.get("action", &"")
			var resource_path: String = str(msg.get("resource", ""))
			if resource_path.is_empty():
				return
			match action:
				&"play_sfx":
					pass
				&"play_music":
					pass
