extends RefCounted
class_name InventoryData

const BAG_SIZE: int = 64
const RUNE_SIZE: int = 6
const LOCKED_RUNE_SLOT_COUNT: int = 3

var bag: Array[ItemDef] = []
var equipped: Dictionary = {}
var runes: Array[ItemDef] = []

func ensure_shape() -> void:
	if bag.size() != BAG_SIZE:
		bag.resize(BAG_SIZE)
	if runes.size() != RUNE_SIZE:
		runes.resize(RUNE_SIZE)

func has_any_inventory() -> bool:
	if not equipped.is_empty():
		return true
	for it in bag:
		if it != null:
			return true
	for r in runes:
		if r != null:
			return true
	return false
