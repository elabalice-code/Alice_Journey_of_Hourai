extends RefCounted
class_name RuntimeEventActionPlan

const RuntimeEventActionIntentScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/RuntimeEventActionIntent.gd")

static func build_action_intents(event_dict: Dictionary, source_event_id: String) -> Array[RuntimeEventActionIntent]:
	var intents: Array[RuntimeEventActionIntent] = []
	var actions = event_dict.get("actions", [])
	if not (actions is Array):
		return intents
	for a in actions:
		if not (a is Dictionary):
			continue
		var intent: RuntimeEventActionIntent = build_action_intent(a as Dictionary, source_event_id)
		if intent.is_valid():
			intents.append(intent)
	return intents

static func build_action_intent(action: Dictionary, source_event_id: String) -> RuntimeEventActionIntent:
	var action_type := str(action.get("type", ""))
	var delay_ms := int(action.get("delayMs", 0))
	match action_type:
		"StartEvent":
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_START_EVENT, {
				"target_event_id": str(action.get("targetEventId", "")).strip_edges()
			}, delay_ms)
		"ChangeMap":
			var payload := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_LOAD_ROOM, {
				"target_map": str(payload.get("map", "")).strip_edges()
			}, delay_ms)
		"SetVariable":
			var payload2 := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_SET_VARIABLE, {
				"key": StringName(str(payload2.get("key", "")).strip_edges()),
				"value": payload2.get("value", null)
			}, delay_ms)
		"EmitSignal":
			var payload3 := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_EMIT_SIGNAL, {
				"signal": str(payload3.get("signal", "")).strip_edges(),
				"source_domain": "Meta",
				"from_event": source_event_id
			}, delay_ms)
		"StartDialogue":
			var payload4 := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_START_DIALOGUE, {
				"dialogue_id": StringName(str(payload4.get("dialogueId", "")).strip_edges()),
				"speaker": str(payload4.get("speaker", "")).strip_edges(),
				"story_text": str(payload4.get("storyText", "")).strip_edges(),
				"npc_text": str(payload4.get("npcText", "")).strip_edges(),
				"room": str(payload4.get("room", "")),
			}, delay_ms)
		"StartCombat":
			var payload5 := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_LEVEL_EVENT, {
				"event": StringName(str(payload5.get("combatId", "start_combat"))),
				"room": str(payload5.get("room", ""))
			}, delay_ms)
		"CompleteQuest":
			var payload6 := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_COMPLETE_QUEST, {
				"quest_id": StringName(str(payload6.get("questId", payload6.get("quest_id", ""))).strip_edges())
			}, delay_ms)
		"CustomScript":
			var payload7 := parse_payload(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_LEVEL_EVENT, {
				"event": StringName(str(payload7.get("scriptEvent", "custom_script"))),
				"room": str(payload7.get("room", ""))
			}, delay_ms)
	return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_NONE)

static func parse_payload(action: Dictionary) -> Dictionary:
	var raw := str(action.get("payloadJson", "")).strip_edges()
	if raw.is_empty():
		return {}
	var parsed = JSON.parse_string(raw)
	if parsed is Dictionary:
		return parsed
	return {}
