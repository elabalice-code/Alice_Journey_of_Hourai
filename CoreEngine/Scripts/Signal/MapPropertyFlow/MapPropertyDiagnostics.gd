extends RefCounted
class_name MapPropertyDiagnostics

static func intent_summary(intent: MapPropertyIntent) -> Dictionary:
	if intent == null:
		return {"valid": false}
	return {
		"valid": intent.is_valid(),
		"kind": intent.kind,
		"payload_keys": intent.payload.keys(),
	}
