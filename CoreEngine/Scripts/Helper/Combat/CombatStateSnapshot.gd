extends RefCounted
class_name CombatStateSnapshot

static func make(hp: float, max_hp: float, armor: float) -> Dictionary:
	return {
		"hp": hp,
		"max_hp": max_hp,
		"armor": armor
	}

static func should_emit(last_snapshot: Dictionary, current_snapshot: Dictionary, force: bool) -> bool:
	return force or last_snapshot != current_snapshot
