extends RefCounted
class_name RoomFlowActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const RoomFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowProducers.gd")
const RoomFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowRouter.gd")
const RoomLoadedAfterActionScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/Featuror/RoomLoadedAfterAction.gd")
const RoomFlowIntentExecutorScript = preload("res://CoreEngine/Scripts/Actor/RoomFlowIntentExecutor.gd")
const MapNodeFactsScript = preload("res://CoreEngine/Scripts/Helper/Map/MapNodeFacts.gd")

var _workbench: WorkbenchService
var _pending_after_room_loaded: Dictionary = {}

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			MessageTypes.TYPE_LOAD_ROOM_REQUEST,
			MessageTypes.TYPE_RESET_MAP_STARTING_COORDS_REQUEST,
			MessageTypes.TYPE_SHIFT_PLAYER_REQUEST,
			MessageTypes.TYPE_SET_LOOP_TARGET,
			MessageTypes.TYPE_CLEAR_LOOP_TARGET,
			MessageTypes.TYPE_ROOM_LOADED,
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var game := _workbench.get_service(&"game") as Game
	if game == null:
		return
	if workplace.type == MessageTypes.TYPE_ROOM_LOADED:
		_apply_after_room_loaded(game)
		return
	var frame: RoomFlowSignalFrame = RoomFlowProducersScript.from_workplace(workplace)
	var intent: RoomFlowIntent = RoomFlowRouterScript.route(frame)
	_execute_intent(game, intent)

func _execute_intent(game: Game, intent: RoomFlowIntent) -> void:
	_pending_after_room_loaded = RoomFlowIntentExecutorScript.execute(game, intent, _pending_after_room_loaded)

func _apply_after_room_loaded(game: Game) -> void:
	if _pending_after_room_loaded.is_empty():
		return
	var after := _pending_after_room_loaded
	_pending_after_room_loaded = {}
	var map_facts: Dictionary = MapNodeFactsScript.collect(game.map)
	var intents: Array[RoomFlowIntent] = RoomLoadedAfterActionScript.build_intents(after, map_facts, game.player != null)
	for intent in intents:
		_execute_intent(game, intent)
