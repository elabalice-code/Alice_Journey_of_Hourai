extends RefCounted
class_name RuntimeEventActionPlan

const RuntimeEventActionIntentScript = preload("res://CoreEngine/Scripts/Signal/RuntimeEvent/RuntimeEventActionIntent.gd")
const RuntimeEventActionPayloadScript = preload("res://CoreEngine/Scripts/Helper/RuntimeEvent/RuntimeEventActionPayload.gd")
const RuntimeEventActionTypesScript = preload("res://CoreEngine/Scripts/Contract/RuntimeEventActionTypes.gd")

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
		RuntimeEventActionTypesScript.START_EVENT:
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_START_EVENT, {
				"target_event_id": str(action.get("targetEventId", "")).strip_edges()
			}, delay_ms)
		RuntimeEventActionTypesScript.CHANGE_MAP:
			var payload := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_LOAD_ROOM, {
				"target_map": RuntimeEventActionPayloadScript.get_string(payload, "map")
			}, delay_ms)
		RuntimeEventActionTypesScript.SET_VARIABLE:
			var payload2 := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_SET_VARIABLE, {
				"key": RuntimeEventActionPayloadScript.get_string_name(payload2, "key"),
				"value": RuntimeEventActionPayloadScript.get_value(payload2, "value")
			}, delay_ms)
		RuntimeEventActionTypesScript.EMIT_SIGNAL:
			var payload3 := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_EMIT_SIGNAL, {
				"signal": RuntimeEventActionPayloadScript.get_string(payload3, "signal"),
				"source_domain": "Meta",
				"from_event": source_event_id
			}, delay_ms)
		RuntimeEventActionTypesScript.START_DIALOGUE:
			var payload4 := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_START_DIALOGUE, {
				"dialogue_id": RuntimeEventActionPayloadScript.get_string_name(payload4, "dialogueId"),
				"speaker": RuntimeEventActionPayloadScript.get_string(payload4, "speaker"),
				"story_text": RuntimeEventActionPayloadScript.get_string(payload4, "storyText"),
				"npc_text": RuntimeEventActionPayloadScript.get_string(payload4, "npcText"),
				"room": RuntimeEventActionPayloadScript.get_string(payload4, "room"),
			}, delay_ms)
		RuntimeEventActionTypesScript.START_COMBAT:
			var payload5 := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_LEVEL_EVENT, {
				"event": RuntimeEventActionPayloadScript.get_string_name(payload5, "combatId", "start_combat"),
				"room": RuntimeEventActionPayloadScript.get_string(payload5, "room")
			}, delay_ms)
		RuntimeEventActionTypesScript.COMPLETE_QUEST:
			var payload6 := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_COMPLETE_QUEST, {
				"quest_id": RuntimeEventActionPayloadScript.get_quest_id(payload6)
			}, delay_ms)
		RuntimeEventActionTypesScript.CUSTOM_SCRIPT:
			var payload7 := RuntimeEventActionPayloadScript.parse(action)
			return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_LEVEL_EVENT, {
				"event": RuntimeEventActionPayloadScript.get_string_name(payload7, "scriptEvent", "custom_script"),
				"room": RuntimeEventActionPayloadScript.get_string(payload7, "room")
			}, delay_ms)
	return RuntimeEventActionIntentScript.make(RuntimeEventActionIntentScript.KIND_NONE)
