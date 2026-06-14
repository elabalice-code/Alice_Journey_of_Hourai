extends RefCounted
class_name QuestManagerActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			ActorFramework.TYPE_QUEST_ACTION_REQUEST
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var quest_data: ActorFramework.QuestData = global_wp.quest
	
	match t:
		ActorFramework.TYPE_QUEST_ACTION_REQUEST:
			var action: StringName = msg.get("action", &"")
			var quest_id: StringName = msg.get("quest_id", &"")
			
			match action:
				&"accept":
					if not quest_data.active_quests.has(quest_id) and not quest_id in quest_data.completed_quests:
						quest_data.active_quests[quest_id] = {"stage": 0, "data": {}}
						_notify_update(quest_id, &"started")
				&"advance":
					if quest_data.active_quests.has(quest_id):
						var q: Dictionary = quest_data.active_quests[quest_id] as Dictionary
						q["stage"] += 1
						_notify_update(quest_id, &"advanced")
				&"complete":
					if quest_data.active_quests.has(quest_id):
						quest_data.active_quests.erase(quest_id)
						quest_data.completed_quests.append(quest_id)
						_notify_update(quest_id, &"completed")

func _notify_update(quest_id: StringName, status: StringName) -> void:
	if _workbench:
		_workbench.send({
			"type": ActorFramework.TYPE_QUEST_UPDATED,
			"quest_id": quest_id,
			"status": status
		})
