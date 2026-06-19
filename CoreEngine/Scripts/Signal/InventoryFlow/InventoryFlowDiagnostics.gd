extends RefCounted
class_name InventoryFlowDiagnostics

static func intent_summary(intent: InventoryFlowIntent) -> Dictionary:
	if intent == null:
		return {"valid": false}
	return {
		"valid": intent.is_valid(),
		"kind": intent.kind,
		"payload_keys": intent.payload.keys(),
	}
