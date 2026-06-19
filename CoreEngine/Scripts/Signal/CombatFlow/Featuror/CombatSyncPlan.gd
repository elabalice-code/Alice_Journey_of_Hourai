extends RefCounted
class_name CombatSyncPlan

const CombatFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowIntent.gd")

static func build_sync_intent(msg: Dictionary) -> CombatFlowIntent:
	return CombatFlowIntentScript.make(CombatFlowIntentScript.KIND_SYNC, {
		"target": msg.get("target") as Node
	})
