extends RefCounted
class_name PlayerDataManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [MessageTypes.TYPE_LEVEL_EVENT], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null or _workbench == null:
		return
	
	var msg: Dictionary = workplace.payload
	var ev: StringName = msg.get("event", &"")
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return

	match ev:
		&"enter_random_level":
			global_wp.player.apply_multiplier(1.5)
		&"exit_random_level":
			global_wp.player.reset_to_base()
		_:
			return
	
	_workbench.send({
		"type": MessageTypes.TYPE_PLAYER_DATA_CHANGED,
		"speed_min": global_wp.player.speed_min,
		"speed_max": global_wp.player.speed_max,
		"jump_velocity": global_wp.player.jump_velocity,
		"context": {
			"event": ev,
			"room": str(msg.get("room", ""))
		}
	})
