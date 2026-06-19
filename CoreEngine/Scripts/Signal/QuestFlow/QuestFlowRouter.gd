extends RefCounted
class_name QuestFlowRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const QuestLifecyclePlanScript = preload("res://CoreEngine/Scripts/Signal/QuestFlow/Featuror/QuestLifecyclePlan.gd")
const QuestFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/QuestFlow/QuestFlowIntent.gd")

static func route(frame: QuestFlowSignalFrame, quest_data) -> QuestFlowIntent:
	if frame == null or not frame.is_valid():
		return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_NONE)
	match frame.source_type:
		MessageTypes.TYPE_QUEST_ACTION_REQUEST:
			return QuestLifecyclePlanScript.build_lifecycle_intent(frame.payload, quest_data)
		_:
			return QuestFlowIntentScript.make(QuestFlowIntentScript.KIND_NONE)
