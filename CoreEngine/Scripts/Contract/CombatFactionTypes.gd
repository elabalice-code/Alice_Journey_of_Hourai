extends RefCounted
class_name CombatFactionTypes

const PLAYER: StringName = &"player"
const ENEMY: StringName = &"enemy"

static func target_group_for_source(source_faction: StringName) -> StringName:
	match source_faction:
		PLAYER:
			return ENEMY
		ENEMY:
			return PLAYER
	return &""
