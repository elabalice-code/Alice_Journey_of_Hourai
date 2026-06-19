extends RefCounted
class_name DialogueChoicePlan

const DialogueFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/DialogueFlow/DialogueFlowIntent.gd")

static func build_request_intent(
	npc_id: StringName,
	active_npc_id: StringName,
	active_mode: StringName,
	story_done: bool
) -> DialogueFlowIntent:
	if npc_id == &"":
		return DialogueFlowIntentScript.make(DialogueFlowIntentScript.KIND_NONE)
	if active_npc_id == npc_id and active_mode == &"story":
		return DialogueFlowIntentScript.make(DialogueFlowIntentScript.KIND_START_NPC, {
			"dialogue_id": npc_id
		})
	if active_mode != &"":
		return DialogueFlowIntentScript.make(DialogueFlowIntentScript.KIND_END_ACTIVE, {
			"dialogue_id": active_npc_id,
			"mode": active_mode
		})
	if not story_done:
		return DialogueFlowIntentScript.make(DialogueFlowIntentScript.KIND_START_STORY, {
			"dialogue_id": npc_id,
			"mark_story_done": true
		})
	return DialogueFlowIntentScript.make(DialogueFlowIntentScript.KIND_START_NPC, {
		"dialogue_id": npc_id
	})
