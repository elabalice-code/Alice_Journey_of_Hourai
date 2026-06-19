extends RefCounted
class_name InventorySlots

static func ensure_items_size(items: Array, size: int) -> void:
	if items.is_empty():
		items.resize(size)

static func is_valid_index(items: Array, index: int) -> bool:
	return index >= 0 and index < items.size()

static func first_empty_index(items: Array) -> int:
	for i in range(items.size()):
		if items[i] == null:
			return i
	return -1

static func has_empty_slot(items: Array) -> bool:
	return first_empty_index(items) >= 0
