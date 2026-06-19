extends RefCounted
class_name AreaFlowActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const AreaFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/AreaFlowProducers.gd")
const AreaFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/AreaFlowRouter.gd")

var _workbench: WorkbenchService
var _pending_area_loaded: Dictionary = {}

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			MessageTypes.TYPE_LOAD_AREA_REQUEST,
			MessageTypes.TYPE_ROOM_LOADED,
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var t: StringName = workplace.type
	match t:
		MessageTypes.TYPE_LOAD_AREA_REQUEST:
			var frame: AreaFlowSignalFrame = AreaFlowProducersScript.from_workplace(workplace)
			_execute_intents(AreaFlowRouterScript.route(frame))
		MessageTypes.TYPE_ROOM_LOADED:
			var intents: Array[AreaFlowIntent] = AreaFlowRouterScript.route_area_loaded(_pending_area_loaded)
			_pending_area_loaded = {}
			_execute_intents(intents)

func _execute_intents(intents: Array[AreaFlowIntent]) -> void:
	for intent in intents:
		_execute_intent(intent)

func _execute_intent(intent: AreaFlowIntent) -> void:
	if intent == null or not intent.is_valid():
		return
	match intent.kind:
		AreaFlowIntent.KIND_PREPARE_AREA_LOADED:
			_pending_area_loaded = intent.payload.duplicate(true)
		AreaFlowIntent.KIND_SET_AREA_STATE:
			for key in intent.payload.keys():
				_workbench.register_workplace_data(StringName(key), intent.payload[key])
		AreaFlowIntent.KIND_REQUEST_INPUT_MODE:
			_workbench.send({
				"type": MessageTypes.TYPE_INPUT_MODE_CHANGE_REQUEST,
				"mode": intent.payload.get("mode", &"side_scrolling")
			})
		AreaFlowIntent.KIND_LOAD_ENTRY_ROOM:
			var room_req := {
				"type": MessageTypes.TYPE_LOAD_ROOM_REQUEST,
				"target_map": str(intent.payload.get("target_map", ""))
			}
			if intent.payload.get("after", null) is Dictionary:
				room_req["after"] = intent.payload.get("after", {}) as Dictionary
			_workbench.send(room_req)
		AreaFlowIntent.KIND_RESET_MAP_STARTING_COORDS:
			_workbench.send({
				"type": MessageTypes.TYPE_RESET_MAP_STARTING_COORDS_REQUEST
			})
		AreaFlowIntent.KIND_EMIT_AREA_LOADED:
			_workbench.send({
				"type": MessageTypes.TYPE_AREA_LOADED,
				"area_id": intent.payload.get("area_id", &""),
				"entry_room": intent.payload.get("entry_room", ""),
			})
