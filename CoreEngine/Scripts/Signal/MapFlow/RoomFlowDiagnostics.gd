extends RefCounted
class_name RoomFlowDiagnostics

static func intent_summary(intent: RoomFlowIntent) -> Dictionary:
	if intent == null:
		return {"valid": false}
	return {
		"valid": intent.is_valid(),
		"kind": intent.kind,
		"payload_keys": intent.payload.keys(),
	}
