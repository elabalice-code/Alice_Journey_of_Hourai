extends Node
class_name WorkbenchService

const LevelEventManagerActor = preload("res://CoreEngine/Scripts/Actor/LevelEventManagerActor.gd")
const PlayerDataManagerActor = preload("res://CoreEngine/Scripts/Actor/PlayerDataManagerActor.gd")
const ProgressData = preload("res://CoreEngine/Scripts/Data/ProgressData.gd")
const WorkPlaceScript = preload("res://CoreEngine/Scripts/Data/WorkPlace.gd")
const SaveActor = preload("res://CoreEngine/Scripts/Actor/SaveActor.gd")
const RoomFlowActor = preload("res://CoreEngine/Scripts/Actor/RoomFlowActor.gd")
const AreaFlowActor = preload("res://CoreEngine/Scripts/Actor/AreaFlowActor.gd")

const InputControlActor = preload("res://CoreEngine/Scripts/Actor/InputControlActor.gd")
const CombatManagerActor = preload("res://CoreEngine/Scripts/Actor/CombatManagerActor.gd")
const InventoryManagerActor = preload("res://CoreEngine/Scripts/Actor/InventoryManagerActor.gd")
const QuestManagerActor = preload("res://CoreEngine/Scripts/Actor/QuestManagerActor.gd")
const AudioManagerActor = preload("res://CoreEngine/Scripts/Actor/AudioManagerActor.gd")
const RoomPropertyManagerActorScript = preload("res://CoreEngine/Scripts/Actor/RoomPropertyManagerActor.gd")
const RuntimeEventFlowActorScript = preload("res://CoreEngine/Scripts/Actor/RuntimeEventFlowActor.gd")
const TestActorScript = preload("res://CoreEngine/Scripts/Actor/TestActor.gd")

signal message_published(message: Dictionary)
signal tick(delta: float)
signal workplace_published(workplace)

var _queue_workplaces: Array[WorkPlace] = []
var _services: Dictionary = {}
var _max_dispatch_per_frame: int = 4096
var _routes: Dictionary = {}
var _global_workplace: WorkPlace

static func get_singleton() -> WorkbenchService:
	return (Engine.get_main_loop() as SceneTree).root.get_node_or_null(^"Workbench") as WorkbenchService

func get_workplace() -> WorkPlace:
	if _global_workplace != null:
		return _global_workplace
	return get_service(&"workplace") as WorkPlace

func register_workplace_data(key: StringName, value: Variant) -> void:
	var wp := get_workplace()
	if wp == null:
		return
	wp.set_data(key, value)

func get_workplace_data(key: StringName, default_value: Variant = null) -> Variant:
	var wp := get_workplace()
	if wp == null:
		return default_value
	return wp.get_data(key, default_value)

func _ready() -> void:
	if _global_workplace == null:
		_global_workplace = WorkPlaceScript.new()
		set_service(&"workplace", _global_workplace)
	if not _global_workplace.has_data(&"progress"):
		register_workplace_data(&"progress", ProgressData.new())

	var player_data_manager := PlayerDataManagerActor.new(self)
	var level_event_manager := LevelEventManagerActor.new(self)
	register_service(&"level_event_manager", level_event_manager)
	register_service(&"player_data_manager", player_data_manager)
	
	var save_actor := SaveActor.new(self)
	register_service(&"save_actor", save_actor)
	
	var room_flow_actor := RoomFlowActor.new(self)
	register_service(&"room_flow", room_flow_actor)
	
	var area_flow_actor := AreaFlowActor.new(self)
	register_service(&"area_flow", area_flow_actor)

	var input_actor := InputControlActor.new(self)
	register_service(&"input_control", input_actor)
	
	var combat_actor = CombatManagerActor.new(self)
	register_service(&"combat_manager", combat_actor)
	
	var inventory_actor := InventoryManagerActor.new(self)
	register_service(&"inventory_manager", inventory_actor)
	
	var quest_actor := QuestManagerActor.new(self)
	register_service(&"quest_manager", quest_actor)
	
	var audio_actor := AudioManagerActor.new(self)
	register_service(&"audio_manager", audio_actor)
	
	var room_prop_actor = RoomPropertyManagerActorScript.new(self)
	register_service(&"room_property_manager", room_prop_actor)
	
	var runtime_event_flow_actor = RuntimeEventFlowActorScript.new(self)
	register_service(&"runtime_event_flow", runtime_event_flow_actor)
	
	var test_actor = TestActorScript.new(self)
	register_service(&"test_actor", test_actor)

func register_service(name: StringName, service: Variant) -> void:
	var list := _prune_service_list(name)
	if not list.is_empty() and list.back() == service:
		return
	list.append(service)
	_services[name] = list

func set_service(name: StringName, service: Variant) -> void:
	_services[name] = [service]

func _prune_service_list(name: StringName) -> Array:
	var list: Array
	if _services.has(name):
		list = _services[name] as Array
	else:
		list = []
	if list.is_empty():
		return []
	var pruned: Array = []
	for s in list:
		if s == null:
			continue
		if s is Object and not is_instance_valid(s):
			continue
		pruned.append(s)
	if pruned.size() != list.size():
		_services[name] = pruned
	return pruned

func get_service(name: StringName) -> Variant:
	var list := _prune_service_list(name)
	if list.is_empty():
		return null
	return list.back()

func get_services(name: StringName) -> Array:
	return _prune_service_list(name)

func send(message: Dictionary) -> void:
	var wp := WorkPlaceScript.new()
	var raw_type = message.get("type", &"")
	if raw_type is StringName:
		wp.type = raw_type
	else:
		wp.type = StringName(str(raw_type))
	if not message.has("type"):
		message["type"] = wp.type
	wp.payload = message
	_queue_workplaces.append(wp)

func send_workplace(workplace: WorkPlace) -> void:
	if workplace == _global_workplace:
		var wp := WorkPlaceScript.new()
		wp.type = workplace.type
		wp.payload = workplace.payload
		wp.meta = workplace.meta.duplicate(true)
		_queue_workplaces.append(wp)
		return
	if workplace.payload is Dictionary and not workplace.payload.has("type"):
		workplace.payload["type"] = workplace.type
	_queue_workplaces.append(workplace)

func register_actor(actor: Object, types: Array, method: StringName = &"process_workplace") -> void:
	var entry := {"actor": actor, "method": method}
	for raw_t in types:
		var t: StringName
		if raw_t is StringName:
			t = raw_t
		else:
			t = StringName(str(raw_t))
		var list: Array
		if _routes.has(t):
			list = _routes[t] as Array
		else:
			list = []
		list.append(entry)
		_routes[t] = list

func unregister_actor(actor: Object) -> void:
	for t in _routes.keys():
		var list := _routes[t] as Array
		list = list.filter(func(e): return e.get("actor") != actor)
		_routes[t] = list

func _dispatch_workplace(workplace: WorkPlace) -> void:
	if workplace.payload is Dictionary and not workplace.payload.has("type"):
		workplace.payload["type"] = workplace.type
	if workplace.payload is Dictionary and not workplace.payload.is_empty():
		message_published.emit(workplace.payload)
	workplace_published.emit(workplace)

	var delivered: Dictionary = {}
	var targets: Array[StringName] = [workplace.type, &"*"]
	for t in targets:
		if not _routes.has(t):
			continue
		var list := _routes[t] as Array
		for entry in list:
			var a: Object = entry.get("actor")
			if a == null:
				continue
			if delivered.has(a):
				continue
			delivered[a] = true
			var m: StringName = entry.get("method", &"process_workplace")
			if a.has_method(m):
				a.call(m, workplace)

func _physics_process(delta: float) -> void:
	tick.emit(delta)
	var dispatched := 0
	while dispatched < _max_dispatch_per_frame and not _queue_workplaces.is_empty():
		var workplace := _queue_workplaces.pop_front() as WorkPlace
		_dispatch_workplace(workplace)
		dispatched += 1
	if dispatched >= _max_dispatch_per_frame and not _queue_workplaces.is_empty():
		push_warning("Workbench message queue overflow: %d" % _queue_workplaces.size())
