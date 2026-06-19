extends RefCounted
class_name InventoryFlowRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const InventoryActionPlanScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/Featuror/InventoryActionPlan.gd")
const InventoryFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowIntent.gd")

static func route(frame: InventoryFlowSignalFrame) -> InventoryFlowIntent:
	if frame == null or not frame.is_valid():
		return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
	match frame.source_type:
		MessageTypes.TYPE_ITEM_ACTION_REQUEST:
			return InventoryActionPlanScript.build_action_intent(frame.payload)
		_:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
