extends RefCounted
class_name SaveActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")
const ProgressData = preload("res://CoreEngine/Scripts/Data/ProgressData.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [ActorFramework.TYPE_SAVE_REQUEST], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	
	var msg: Dictionary = workplace.payload
	var reason: StringName = msg.get("reason", &"")
	
	var game: Game = _workbench.get_service(&"game") as Game
	if game != null:
		game.save_game()
	_workbench.send({"type": ActorFramework.TYPE_RESET_MAP_STARTING_COORDS_REQUEST})
	
	var progress := _workbench.get_workplace_data(&"progress") as ProgressData
	if progress:
		progress.mark_saved()
	
	_workbench.send({
		"type": ActorFramework.TYPE_SAVE_COMPLETED,
		"reason": reason
	})
