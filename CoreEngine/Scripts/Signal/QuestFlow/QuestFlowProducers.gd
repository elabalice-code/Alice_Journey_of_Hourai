extends RefCounted
class_name QuestFlowProducers

const QuestFlowSignalFrameScript = preload("res://CoreEngine/Scripts/Signal/QuestFlow/QuestFlowSignalFrame.gd")

static func from_workplace(workplace) -> QuestFlowSignalFrame:
	if workplace == null:
		return QuestFlowSignalFrameScript.make(&"", {})
	return QuestFlowSignalFrameScript.make(workplace.type, workplace.payload)
