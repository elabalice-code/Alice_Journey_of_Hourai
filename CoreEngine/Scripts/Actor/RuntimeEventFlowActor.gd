extends RefCounted
class_name RuntimeEventFlowActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const RuntimeEventProducersScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/RuntimeEventProducers.gd")
const RuntimeEventActionExecutorScript = preload("res://CoreEngine/Scripts/Actor/RuntimeEventActionExecutor.gd")
const RuntimeEventActionPlanScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/Featuror/RuntimeEventActionPlan.gd")
const RuntimeEventStateExecutorScript = preload("res://CoreEngine/Scripts/Actor/RuntimeEventStateExecutor.gd")
const RuntimeEventTriggerMatchScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/Featuror/TriggerMatch.gd")
const RuntimeEventStateOutputScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/Featuror/EventStateOutput.gd")

const KEY_RUNTIME_EVENT_PROJECT_PATH: StringName = &"runtime_event_project_path"
const DEFAULT_PROJECT_PATH: String = "res://GodotTools/EventStudio/Samples/prologue_flow.events.json"

var _workbench: WorkbenchService
var _project: Dictionary = {}
var _events_by_id: Dictionary = {}
var _fired_once: Dictionary = {}
var _action_executor: RuntimeEventActionExecutor
var _state_executor: RuntimeEventStateExecutor

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench == null:
		return
	_action_executor = RuntimeEventActionExecutorScript.new(_workbench)
	_state_executor = RuntimeEventStateExecutorScript.new(_workbench)
	_workbench.register_actor(self, [
		MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
		MessageTypes.TYPE_RUNTIME_EVENT_START,
		MessageTypes.TYPE_ROOM_LOADED,
		MessageTypes.TYPE_AREA_LOADED,
		MessageTypes.TYPE_LEVEL_EVENT,
	], &"_on_workplace")
	_load_project()

func _on_workplace(workplace) -> void:
	if workplace == null or _workbench == null:
		return
	if _events_by_id.is_empty():
		return
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	match t:
		MessageTypes.TYPE_RUNTIME_EVENT_START:
			var start_id := str(msg.get("event_id", "")).strip_edges()
			if start_id.is_empty():
				start_id = str(_project.get("startEventId", "")).strip_edges()
			_fire_event(start_id)
		_:
			_on_signal_frame(RuntimeEventProducersScript.from_workplace(workplace))

func _load_project() -> void:
	var configured := str(_workbench.get_workplace_data(KEY_RUNTIME_EVENT_PROJECT_PATH, ""))
	var path := configured if not configured.is_empty() else DEFAULT_PROJECT_PATH
	_project = {}
	_events_by_id.clear()
	if path.is_empty() or not FileAccess.file_exists(path):
		return
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return
	var raw := file.get_as_text()
	var parsed = JSON.parse_string(raw)
	if not (parsed is Dictionary):
		return
	_project = parsed
	_reindex_events()

func reload_project() -> void:
	_fired_once.clear()
	_load_project()

func _reindex_events() -> void:
	_events_by_id.clear()
	var list = _project.get("events", [])
	if not (list is Array):
		return
	for entry in list:
		if not (entry is Dictionary):
			continue
		var id := str(entry.get("id", "")).strip_edges()
		if id.is_empty():
			continue
		_events_by_id[id] = entry

func _on_signal_frame(frame: RuntimeEventSignalFrame) -> void:
	if frame == null or not frame.is_valid():
		return
	for event_id in _events_by_id.keys():
		var event_dict := _events_by_id[event_id] as Dictionary
		if not bool(event_dict.get("enabled", true)):
			continue
		if _is_consumed_one_shot(event_dict):
			continue
		if RuntimeEventTriggerMatchScript.matches_event(event_dict, frame):
			_fire_event(str(event_id))

func _is_consumed_one_shot(event_dict: Dictionary) -> bool:
	if not bool(event_dict.get("oneShot", false)):
		return false
	var id := str(event_dict.get("id", ""))
	return _fired_once.get(id, false)

func _fire_event(event_id: String) -> void:
	if event_id.is_empty():
		return
	var event_dict := _events_by_id.get(event_id, {}) as Dictionary
	if event_dict.is_empty():
		return
	if _is_consumed_one_shot(event_dict):
		return
	_fired_once[event_id] = true
	if _state_executor != null:
		var state_intent: RuntimeEventStateIntent = RuntimeEventStateOutputScript.build_mark_fired_intent(event_id, event_dict)
		_state_executor.execute_intent(state_intent)
	if _action_executor != null:
		var intents: Array[RuntimeEventActionIntent] = RuntimeEventActionPlanScript.build_action_intents(event_dict, event_id)
		_action_executor.execute_intents(intents, Callable(self, &"_fire_event"))
