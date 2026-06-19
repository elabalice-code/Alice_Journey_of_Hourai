extends RefCounted
class_name InventoryFlowRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const InventoryActionPlanScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/Featuror/InventoryActionPlan.gd")
const InventoryOperationPlanScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/Featuror/InventoryOperationPlan.gd")
const InventoryFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowIntent.gd")

static func route(frame: InventoryFlowSignalFrame, data: InventoryData = null) -> InventoryFlowIntent:
	if frame == null or not frame.is_valid():
		return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
	var intent: InventoryFlowIntent
	match frame.source_type:
		MessageTypes.TYPE_ITEM_ACTION_REQUEST:
			intent = InventoryActionPlanScript.build_action_intent(frame.payload)
		_:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
	return InventoryOperationPlanScript.validate_intent(intent, data)
