extends RefCounted
class_name CombatFlowRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const CombatFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowIntent.gd")
const DamageApplicationPlanScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/Featuror/DamageApplicationPlan.gd")
const CombatSyncPlanScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/Featuror/CombatSyncPlan.gd")

static func route(frame: CombatFlowSignalFrame) -> CombatFlowIntent:
	if frame == null or not frame.is_valid():
		return CombatFlowIntentScript.make(CombatFlowIntentScript.KIND_NONE)
	match frame.source_type:
		MessageTypes.TYPE_APPLY_DAMAGE_REQUEST:
			return DamageApplicationPlanScript.build_apply_damage_intent(frame.payload)
		MessageTypes.TYPE_COMBAT_SYNC_REQUEST:
			return CombatSyncPlanScript.build_sync_intent(frame.payload)
		_:
			return CombatFlowIntentScript.make(CombatFlowIntentScript.KIND_NONE)
