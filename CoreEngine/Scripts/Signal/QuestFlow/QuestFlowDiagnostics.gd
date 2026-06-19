extends RefCounted
class_name QuestFlowDiagnostics

static func intent_summary(intent: QuestFlowIntent) -> Dictionary:
	if intent == null:
		return {"valid": false}
	return {
		"valid": intent.is_valid(),
		"kind": intent.kind,
		"payload_keys": intent.payload.keys(),
	}
