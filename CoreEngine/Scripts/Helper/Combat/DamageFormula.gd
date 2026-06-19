extends RefCounted
class_name DamageFormula

static func after_armor(raw_damage: float, armor: float) -> float:
	return raw_damage * 100.0 / (100.0 + armor)
