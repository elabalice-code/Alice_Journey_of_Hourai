extends Node
class_name ItemCatalog

static var _defs_cache: Dictionary

static func get_item_defs() -> Dictionary:
	if not _defs_cache.is_empty():
		return _defs_cache
	_defs_cache = {
		&"sword_1": ItemDef.make(&"sword_1", "武器1", &"weapon", &"weapon", "res://CoreEngine/Sprites/Items/Sword1.bmp"),
		&"sword_2": ItemDef.make(&"sword_2", "武器2", &"weapon", &"weapon", "res://CoreEngine/Sprites/Items/Sword2.bmp"),
		&"sword_3": ItemDef.make(&"sword_3", "武器3", &"weapon", &"weapon", "res://CoreEngine/Sprites/Items/Sword3.bmp"),
		&"sword_4": ItemDef.make(&"sword_4", "武器4", &"weapon", &"weapon", "res://CoreEngine/Sprites/Items/Sword4.bmp"),
		&"rune_1": ItemDef.make(&"rune_1", "符文1", &"rune", &"", "res://CoreEngine/Sprites/Items/Magic1.bmp"),
		&"rune_2": ItemDef.make(&"rune_2", "符文2", &"rune", &"", "res://CoreEngine/Sprites/Items/Magic2.bmp"),
		&"rune_3": ItemDef.make(&"rune_3", "符文3", &"rune", &"", "res://CoreEngine/Sprites/Items/Magic3.bmp"),
	}
	return _defs_cache

static func get_starting_bag_items() -> Array[ItemDef]:
	var defs := get_item_defs()
	return [
		defs[&"sword_1"],
		defs[&"sword_2"],
		defs[&"sword_3"],
		defs[&"sword_4"],
		defs[&"rune_1"],
		defs[&"rune_2"],
		defs[&"rune_3"],
	]
