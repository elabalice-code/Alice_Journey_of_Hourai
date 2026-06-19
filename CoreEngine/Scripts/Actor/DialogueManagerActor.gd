extends Node
class_name DialogueManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const DialogueActionTypesScript = preload("res://CoreEngine/Scripts/Contract/DialogueActionTypes.gd")
const DialogueEventKeyScript = preload("res://CoreEngine/Scripts/Helper/Dialogue/DialogueEventKey.gd")
const DialogueFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/DialogueFlow/DialogueFlowIntent.gd")
const DialogueChoicePlanScript = preload("res://CoreEngine/Scripts/Signal/DialogueFlow/Featuror/DialogueChoicePlan.gd")

var _npc_ui
var _story_ui
var _active_mode: StringName = &""
var _active_npc_id: StringName = &""

func _ready() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.register_actor(self, [
			MessageTypes.TYPE_DIALOGUE_ACTION_REQUEST
		], &"_on_workplace")
	if not InputMap.has_action("interact"):
		InputMap.add_action("interact")
		var ev = InputEventKey.new()
		ev.physical_keycode = KEY_E
		InputMap.action_add_event("interact", ev)
	
	_npc_ui = preload("res://CoreEngine/UI/Dialogue/NPCDialogueUI.tscn").instantiate()
	_story_ui = preload("res://CoreEngine/UI/Dialogue/StoryDialogueUI.tscn").instantiate()
	add_child(_npc_ui)
	add_child(_story_ui)
	_npc_ui.hide_dialogue()
	_npc_ui.hide_prompt()
	_story_ui.hide_dialogue()

func _exit_tree() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.unregister_actor(self)

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var msg: Dictionary = workplace.payload
	var action: StringName = msg.get("action", &"")
	match action:
		DialogueActionTypesScript.SHOW_PROMPT:
			show_prompt(str(msg.get("text", "")))
		DialogueActionTypesScript.HIDE_PROMPT:
			hide_prompt()
		DialogueActionTypesScript.REQUEST_DIALOGUE:
			request_dialogue(
				msg.get("dialogue_id", &"") as StringName,
				str(msg.get("speaker", "")),
				str(msg.get("story_text", "")),
				str(msg.get("npc_text", ""))
			)
		DialogueActionTypesScript.END_DIALOGUE:
			end_dialogue()

func _unhandled_input(event: InputEvent) -> void:
	if _active_mode != &"story":
		return
	if event.is_action_pressed(&"interact") or event.is_action_pressed(&"ui_accept"):
		end_dialogue()
		get_viewport().set_input_as_handled()
		return
	var mouse_event := event as InputEventMouseButton
	if mouse_event != null and mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_LEFT:
		end_dialogue()
		get_viewport().set_input_as_handled()

func show_prompt(text: String) -> void:
	if _active_mode != &"":
		return
	_npc_ui.show_prompt(text)

func hide_prompt() -> void:
	_npc_ui.hide_prompt()

func request_dialogue(npc_id: StringName, speaker_name: String, story_text: String, npc_text: String) -> void:
	var intent: DialogueFlowIntent = DialogueChoicePlanScript.build_request_intent(
		npc_id,
		_active_npc_id,
		_active_mode,
		_is_story_done(npc_id)
	)
	match intent.kind:
		DialogueFlowIntentScript.KIND_START_STORY:
			if bool(intent.payload.get("mark_story_done", false)):
				_mark_story_done(npc_id)
			_start_story(npc_id, speaker_name, story_text)
		DialogueFlowIntentScript.KIND_START_NPC:
			_start_npc(npc_id, speaker_name, npc_text)
		DialogueFlowIntentScript.KIND_END_ACTIVE:
			end_dialogue()

func end_dialogue() -> void:
	var last_npc_id := _active_npc_id
	var last_mode := _active_mode
	_active_mode = &""
	_active_npc_id = &""
	_story_ui.hide_dialogue()
	_npc_ui.hide_dialogue()
	_emit_runtime_signal("Dialogue.End", {
		"dialogue_id": String(last_npc_id),
		"mode": String(last_mode),
	})

func is_dialogue_open() -> bool:
	return _active_mode != &""

func _start_story(npc_id: StringName, speaker_name: String, text: String) -> void:
	_active_mode = &"story"
	_active_npc_id = npc_id
	_npc_ui.hide_prompt()
	_npc_ui.hide_dialogue()
	_story_ui.show_dialogue(text, speaker_name)
	_emit_runtime_signal("Dialogue.Start", {
		"dialogue_id": String(npc_id),
		"mode": "story",
	})

func _start_npc(npc_id: StringName, speaker_name: String, text: String) -> void:
	_active_mode = &"npc"
	_active_npc_id = npc_id
	_npc_ui.hide_prompt()
	_story_ui.hide_dialogue()
	_npc_ui.show_dialogue(text, speaker_name)
	_emit_runtime_signal("Dialogue.Start", {
		"dialogue_id": String(npc_id),
		"mode": "npc",
	})

func _story_event_key(npc_id: StringName) -> StringName:
	return DialogueEventKeyScript.story_done(npc_id)

func _is_story_done(npc_id: StringName) -> bool:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return false
	var progress := workbench.get_workplace_data(&"progress") as ProgressData
	if progress == null:
		return false
	return progress.has_event(_story_event_key(npc_id))

func _mark_story_done(npc_id: StringName) -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var progress := workbench.get_workplace_data(&"progress") as ProgressData
	if progress == null:
		return
	progress.add_event(_story_event_key(npc_id))

func _emit_runtime_signal(signal_name: String, extra: Dictionary = {}) -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var payload := {
		"type": MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
		"signal": signal_name,
		"source_domain": "Dialogue",
	}
	for k in extra.keys():
		payload[k] = extra[k]
	workbench.send(payload)
