extends RefCounted
class_name QuestLifecyclePlan

const QuestFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/QuestFlow/QuestFlowIntent.gd")

static func build_lifecycle_intent(msg: Dictionary, quest_data) -> QuestFlowIntent:
	if quest_data == null:
		return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_NONE)
	var action: StringName = msg.get("action", &"")
	var quest_id: StringName = msg.get("quest_id", &"")
	if quest_id == &"":
		return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_NONE)
	match action:
		&"accept":
			if can_accept(quest_data, quest_id):
				return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_ACCEPT_QUEST, {
					"quest_id": quest_id,
					"status": &"started"
				})
		&"advance":
			if can_advance(quest_data, quest_id):
				return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_ADVANCE_QUEST, {
					"quest_id": quest_id,
					"status": &"advanced"
				})
		&"complete":
			if can_complete(quest_data, quest_id):
				return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_COMPLETE_QUEST, {
					"quest_id": quest_id,
					"status": &"completed"
				})
	return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_NONE)

static func can_accept(quest_data, quest_id: StringName) -> bool:
	return not quest_data.active_quests.has(quest_id) and not quest_id in quest_data.completed_quests

static func can_advance(quest_data, quest_id: StringName) -> bool:
	return quest_data.active_quests.has(quest_id)

static func can_complete(quest_data, quest_id: StringName) -> bool:
	return quest_data.active_quests.has(quest_id)
