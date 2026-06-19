extends RefCounted
class_name DamageApplicationPlan

const CombatFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowIntent.gd")

static func build_apply_damage_intent(msg: Dictionary) -> CombatFlowIntent:
	return CombatFlowIntentScript.make(CombatFlowIntentScript.KIND_APPLY_DAMAGE, {
		"target": msg.get("target") as Node,
		"amount": float(msg.get("amount", 0.0)),
		"attacker_dir": msg.get("attacker_dir", Vector2.ZERO) as Vector2
	})
