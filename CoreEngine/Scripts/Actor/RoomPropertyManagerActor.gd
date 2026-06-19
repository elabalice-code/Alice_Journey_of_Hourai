extends RefCounted
class_name RoomPropertyManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const MapPropertyProducersScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertyProducers.gd")
const MapPropertyRouterScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertyRouter.gd")
const MapPropertyIntentExecutorScript = preload("res://CoreEngine/Scripts/Actor/MapPropertyIntentExecutor.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench != null:
		_workbench.register_actor(self, [MessageTypes.TYPE_ROOM_LOADED], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	
	var game := _workbench.get_service(&"game") as Game
	if game == null or game.map == null:
		return
	
	var frame: MapPropertySignalFrame = MapPropertyProducersScript.from_workplace(workplace)
	var intents: Array[MapPropertyIntent] = MapPropertyRouterScript.route(frame)
	for intent in intents:
		MapPropertyIntentExecutorScript.execute(game.map, intent)
