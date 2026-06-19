extends RefCounted
class_name RoomFlowActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const RoomFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowProducers.gd")
const RoomFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowRouter.gd")
const RoomLoadedAfterActionScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/Featuror/RoomLoadedAfterAction.gd")
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
	if intent == null or not intent.is_valid():
		return
	match intent.kind:
		RoomFlowIntent.KIND_LOAD_ROOM:
			var target: String = str(intent.payload.get("target_map", ""))
			var after: Variant = intent.payload.get("after", {})
			if after is Dictionary and not after.is_empty():
				_pending_after_room_loaded = after as Dictionary
			if not target.is_empty():
				var resolved := ResourceUID.uid_to_path(target) if target.begins_with("uid://") else target
				print("RoomFlowActor load_room_request target=%s resolved=%s current_room=%s current_path=%s" % [
					target,
					resolved,
					str(MetSys.get_current_room_id()),
					str(game.map.scene_file_path) if game.map != null else ""
				])
				game.load_room(target)
		RoomFlowIntent.KIND_RESET_MAP_STARTING_COORDS:
			game.reset_map_starting_coords()
		RoomFlowIntent.KIND_SHIFT_PLAYER:
			var delta: Vector2 = intent.payload.get("delta", Vector2.ZERO) as Vector2
			if game.player != null:
				game.player.position += delta
		RoomFlowIntent.KIND_SET_LOOP_TARGET:
			game.loop = str(intent.payload.get("loop_target", ""))
		RoomFlowIntent.KIND_CLEAR_LOOP_TARGET:
			game.loop = ""
		RoomFlowIntent.KIND_MOVE_PLAYER_TO_POSITION:
			if game.player != null:
				game.player.position = intent.payload.get("position", game.player.position) as Vector2
		RoomFlowIntent.KIND_CALL_MAP_NODE_METHOD:
			var node_name: StringName = intent.payload.get("node", &"")
			var method_name: StringName = intent.payload.get("method", &"")
			var node_target: Node = game.map.get_node_or_null(NodePath(String(node_name))) if game.map != null else null
			if node_target != null and method_name != &"" and node_target.has_method(method_name):
				node_target.call(method_name)
		RoomFlowIntent.KIND_CLEAR_PLAYER_EVENT:
			if game.player == null:
				return
			var delay: float = float(intent.payload.get("delay_sec", 0.0))
			if delay <= 0.0:
				game.player.set("event", false)
			else:
				game.get_tree().create_timer(delay).timeout.connect(game.player.set.bind(&"event", false))

func _apply_after_room_loaded(game: Game) -> void:
	if _pending_after_room_loaded.is_empty():
		return
	var after := _pending_after_room_loaded
	_pending_after_room_loaded = {}
	var map_facts: Dictionary = MapNodeFactsScript.collect(game.map)
	var intents: Array[RoomFlowIntent] = RoomLoadedAfterActionScript.build_intents(after, map_facts, game.player != null)
	for intent in intents:
		_execute_intent(game, intent)
