extends RefCounted
class_name ItemDef

var id: StringName
var display_name: String
var kind: StringName
var equip_slot: StringName
var icon_path: String
var stats: Dictionary
var _icon: Texture2D

static func make(p_id: StringName, p_name: String, p_kind: StringName, p_slot: StringName, p_icon_path: String, p_stats: Dictionary = {}) -> ItemDef:
	var it := ItemDef.new()
	it.id = p_id
	it.display_name = p_name
	it.kind = p_kind
	it.equip_slot = p_slot
	it.icon_path = p_icon_path
	it.stats = p_stats
	return it

func get_icon() -> Texture2D:
	if icon_path.is_empty():
		return null
	if _icon == null:
		_icon = load(icon_path) as Texture2D
	return _icon

func get_stat(key: StringName, default_value: Variant = 0.0) -> Variant:
	return stats.get(key, default_value)
