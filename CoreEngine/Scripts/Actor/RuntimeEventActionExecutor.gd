extends RefCounted
class_name RuntimeEventActionExecutor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench

func execute_intents(intents: Array[RuntimeEventActionIntent], fire_event_callback: Callable) -> void:
	for intent in intents:
		_execute_intent(intent, fire_event_callback)

func _execute_intent(intent: RuntimeEventActionIntent, fire_event_callback: Callable) -> void:
	if intent == null or not intent.is_valid() or _workbench == null:
		return
	if intent.delay_ms > 0:
		await _workbench.get_tree().create_timer(float(intent.delay_ms) / 1000.0).timeout
	match intent.kind:
		RuntimeEventActionIntent.KIND_START_EVENT:
			var target_id: String = str(intent.payload.get("target_event_id", "")).strip_edges()
			if not target_id.is_empty() and fire_event_callback.is_valid():
				fire_event_callback.call(target_id)
		RuntimeEventActionIntent.KIND_LOAD_ROOM:
			var target_map: String = str(intent.payload.get("target_map", "")).strip_edges()
			if not target_map.is_empty():
				_workbench.send({
					"type": MessageTypes.TYPE_LOAD_ROOM_REQUEST,
					"target_map": target_map
				})
		RuntimeEventActionIntent.KIND_SET_VARIABLE:
			var key: StringName = intent.payload.get("key", &"")
			if key != &"":
				_workbench.register_workplace_data(key, intent.payload.get("value", null))
		RuntimeEventActionIntent.KIND_EMIT_SIGNAL:
			var signal_name: String = str(intent.payload.get("signal", "")).strip_edges()
			if not signal_name.is_empty():
				_workbench.send({
					"type": MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
					"signal": signal_name,
					"source_domain": str(intent.payload.get("source_domain", "Meta")),
					"from_event": str(intent.payload.get("from_event", ""))
				})
		RuntimeEventActionIntent.KIND_START_DIALOGUE:
			if not _try_start_dialogue(intent.payload):
				_send_level_event(
					StringName(str(intent.payload.get("dialogue_id", "start_dialogue"))),
					str(intent.payload.get("room", ""))
				)
		RuntimeEventActionIntent.KIND_LEVEL_EVENT:
			_send_level_event(
				intent.payload.get("event", &"") as StringName,
				str(intent.payload.get("room", ""))
			)
		RuntimeEventActionIntent.KIND_COMPLETE_QUEST:
			var quest_id: StringName = intent.payload.get("quest_id", &"")
			if quest_id != &"":
				_workbench.send({
					"type": MessageTypes.TYPE_QUEST_ACTION_REQUEST,
					"action": &"complete",
					"quest_id": quest_id
				})

func _send_level_event(event_name: StringName, room: String) -> void:
	if event_name == &"":
		return
	_workbench.send({
		"type": MessageTypes.TYPE_LEVEL_EVENT_REQUEST,
		"event": event_name,
		"room": room
	})

func _try_start_dialogue(payload: Dictionary) -> bool:
	if payload.is_empty():
		return false
	var dialogue_id: StringName = payload.get("dialogue_id", &"")
	var speaker: String = str(payload.get("speaker", "")).strip_edges()
	var story_text: String = str(payload.get("story_text", "")).strip_edges()
	var npc_text: String = str(payload.get("npc_text", "")).strip_edges()
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
