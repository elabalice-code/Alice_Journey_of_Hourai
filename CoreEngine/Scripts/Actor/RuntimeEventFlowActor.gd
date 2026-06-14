extends RefCounted
class_name RuntimeEventFlowActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

const KEY_RUNTIME_EVENT_PROJECT_PATH: StringName = &"runtime_event_project_path"
const DEFAULT_PROJECT_PATH: String = "res://GodotTools/EventStudio/Samples/prologue_flow.events.json"

var _workbench: WorkbenchService
var _project: Dictionary = {}
var _events_by_id: Dictionary = {}
var _fired_once: Dictionary = {}

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench == null:
		return
	_workbench.register_actor(self, [
		ActorFramework.TYPE_RUNTIME_EVENT_SIGNAL,
		ActorFramework.TYPE_RUNTIME_EVENT_START,
		ActorFramework.TYPE_ROOM_LOADED,
		ActorFramework.TYPE_AREA_LOADED,
		ActorFramework.TYPE_LEVEL_EVENT,
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
		ActorFramework.TYPE_RUNTIME_EVENT_START:
			var start_id := str(msg.get("event_id", "")).strip_edges()
			if start_id.is_empty():
				start_id = str(_project.get("startEventId", "")).strip_edges()
			_fire_event(start_id)
		ActorFramework.TYPE_RUNTIME_EVENT_SIGNAL:
			_on_signal(str(msg.get("signal", "")), str(msg.get("source_domain", "")), msg)
		ActorFramework.TYPE_ROOM_LOADED:
			_on_signal("room_loaded", "Map", msg)
		ActorFramework.TYPE_AREA_LOADED:
			_on_signal("area_loaded", "Map", msg)
		ActorFramework.TYPE_LEVEL_EVENT:
			_on_signal(str(msg.get("event", "")), "Story", msg)

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

func _on_signal(signal_name: String, source_domain: String, context: Dictionary) -> void:
	if signal_name.is_empty():
		return
	for event_id in _events_by_id.keys():
		var event_dict := _events_by_id[event_id] as Dictionary
		if not bool(event_dict.get("enabled", true)):
			continue
		if _is_consumed_one_shot(event_dict):
			continue
		if _matches_any_trigger(event_dict, signal_name, source_domain, context):
			_fire_event(str(event_id))

func _matches_any_trigger(event_dict: Dictionary, signal_name: String, source_domain: String, context: Dictionary) -> bool:
	var triggers = event_dict.get("triggers", [])
	if not (triggers is Array):
		return false
	for t in triggers:
		if not (t is Dictionary):
			continue
		var trigger := t as Dictionary
		var expected_signal := str(trigger.get("signal", "")).strip_edges()
		if not expected_signal.is_empty() and expected_signal != signal_name:
			continue
		var expected_domain := str(trigger.get("sourceDomain", "")).strip_edges().to_lower()
		if not expected_domain.is_empty() and expected_domain != source_domain.to_lower():
			continue
		var condition := str(trigger.get("conditionExpr", "")).strip_edges()
		if _eval_condition(condition, context):
			return true
	return false

func _eval_condition(expr: String, context: Dictionary) -> bool:
	if expr.is_empty():
		return true
	var parts := expr.split("==")
	if parts.size() != 2:
		return true
	var key := parts[0].strip_edges()
	var rhs := parts[1].strip_edges()
	if rhs.begins_with("\"") and rhs.ends_with("\"") and rhs.length() >= 2:
		rhs = rhs.substr(1, rhs.length() - 2)
	return str(context.get(key, "")) == rhs

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
	_workbench.send({
		"type": ActorFramework.TYPE_RUNTIME_EVENT_ACTION,
		"event_id": event_id
	})
	var actions = event_dict.get("actions", [])
	if not (actions is Array):
		return
	for a in actions:
		if not (a is Dictionary):
			continue
		_apply_action(a as Dictionary, event_id)

func _apply_action(action: Dictionary, source_event_id: String) -> void:
	var action_type := str(action.get("type", ""))
	var delay_ms := int(action.get("delayMs", 0))
	if delay_ms > 0:
		await _workbench.get_tree().create_timer(float(delay_ms) / 1000.0).timeout
	match action_type:
		"StartEvent":
			var target_id := str(action.get("targetEventId", "")).strip_edges()
			if not target_id.is_empty():
				_fire_event(target_id)
		"ChangeMap":
			var payload := _parse_payload(action)
			var target_map := str(payload.get("map", "")).strip_edges()
			if not target_map.is_empty():
				_workbench.send({
					"type": ActorFramework.TYPE_LOAD_ROOM_REQUEST,
					"target_map": target_map
				})
		"SetVariable":
			var payload2 := _parse_payload(action)
			var key := StringName(str(payload2.get("key", "")).strip_edges())
			if key != &"":
				_workbench.register_workplace_data(key, payload2.get("value", null))
		"EmitSignal":
			var payload3 := _parse_payload(action)
			var sig := str(payload3.get("signal", "")).strip_edges()
			if not sig.is_empty():
				_workbench.send({
					"type": ActorFramework.TYPE_RUNTIME_EVENT_SIGNAL,
					"signal": sig,
					"source_domain": "Meta",
					"from_event": source_event_id
				})
		"StartDialogue":
			var payload4 := _parse_payload(action)
			if not _try_start_dialogue(payload4):
				_workbench.send({
					"type": ActorFramework.TYPE_LEVEL_EVENT_REQUEST,
					"event": StringName(str(payload4.get("dialogueId", "start_dialogue"))),
					"room": str(payload4.get("room", ""))
				})
		"StartCombat":
			var payload5 := _parse_payload(action)
			_workbench.send({
				"type": ActorFramework.TYPE_LEVEL_EVENT_REQUEST,
				"event": StringName(str(payload5.get("combatId", "start_combat"))),
				"room": str(payload5.get("room", ""))
			})
		"CompleteQuest":
			var payload6 := _parse_payload(action)
			_workbench.send({
				"type": ActorFramework.TYPE_LEVEL_EVENT_REQUEST,
				"event": StringName("quest_complete_" + str(payload6.get("questId", ""))),
				"room": str(payload6.get("room", ""))
			})
		"CustomScript":
			var payload7 := _parse_payload(action)
			_workbench.send({
				"type": ActorFramework.TYPE_LEVEL_EVENT_REQUEST,
				"event": StringName(str(payload7.get("scriptEvent", "custom_script"))),
				"room": str(payload7.get("room", ""))
			})

func _parse_payload(action: Dictionary) -> Dictionary:
	var raw := str(action.get("payloadJson", "")).strip_edges()
	if raw.is_empty():
		return {}
	var parsed = JSON.parse_string(raw)
	if parsed is Dictionary:
		return parsed
	return {}

func _try_start_dialogue(payload: Dictionary) -> bool:
	if payload.is_empty():
		return false
	var dialogue_id := StringName(str(payload.get("dialogueId", "")).strip_edges())
	var speaker := str(payload.get("speaker", "")).strip_edges()
	var story_text := str(payload.get("storyText", "")).strip_edges()
	var npc_text := str(payload.get("npcText", "")).strip_edges()
	if dialogue_id == &"":
		return false
	if story_text.is_empty() and npc_text.is_empty():
		return false
	var manager: Node = _get_dialogue_manager()
	if manager == null:
		return false
	manager.request_dialogue(dialogue_id, speaker, story_text, npc_text)
	return true

func _get_dialogue_manager() -> Node:
	var tree := _workbench.get_tree()
	if tree == null:
		return null
	return tree.root.find_child("DialogueManagerActor", true, false)
