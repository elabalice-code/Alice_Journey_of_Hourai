extends RefCounted
class_name InventoryCraftState

static func can_compose(_recipe_id: StringName, _source_indices: Array, _inventory_data) -> bool:
	return false

static func can_decompose(_recipe_id: StringName, _index: int, _inventory_data) -> bool:
	return false
