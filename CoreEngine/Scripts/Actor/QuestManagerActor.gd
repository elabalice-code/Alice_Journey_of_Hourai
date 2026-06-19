extends RefCounted
class_name QuestManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const QuestFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/QuestFlow/QuestFlowProducers.gd")
const QuestFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/QuestFlow/QuestFlowRouter.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			MessageTypes.TYPE_QUEST_ACTION_REQUEST
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var quest_data: QuestData = global_wp.quest
	var frame: QuestFlowSignalFrame = QuestFlowProducersScript.from_workplace(workplace)
	var intent: QuestFlowIntent = QuestFlowRouterScript.route(frame, quest_data)
	_execute_intent(quest_data, intent)

func _execute_intent(quest_data: QuestData, intent: QuestFlowIntent) -> void:
	if quest_data == null or intent == null or not intent.is_valid():
		return
	var quest_id: StringName = intent.payload.get("quest_id", &"")
	var status: StringName = intent.payload.get("status", &"")
	if quest_id == &"":
		return
	match intent.kind:
		QuestFlowIntent.KIND_ACCEPT_QUEST:
			quest_data.active_quests[quest_id] = {"stage": 0, "data": {}}
			_notify_update(quest_id, status)
		QuestFlowIntent.KIND_ADVANCE_QUEST:
			var q: Dictionary = quest_data.active_quests[quest_id] as Dictionary
			q["stage"] = int(q.get("stage", 0)) + 1
			_notify_update(quest_id, status)
		QuestFlowIntent.KIND_COMPLETE_QUEST:
			quest_data.active_quests.erase(quest_id)
			quest_data.completed_quests.append(quest_id)
			_notify_update(quest_id, status)

func _notify_update(quest_id: StringName, status: StringName) -> void:
	if _workbench:
		_workbench.send({
			"type": MessageTypes.TYPE_QUEST_UPDATED,
			"quest_id": quest_id,
			"status": status
		})
