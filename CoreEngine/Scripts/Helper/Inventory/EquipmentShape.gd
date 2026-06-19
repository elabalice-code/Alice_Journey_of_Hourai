extends RefCounted
class_name EquipmentShape

const EquipmentSlotTypesScript = preload("res://CoreEngine/Scripts/Contract/EquipmentSlotTypes.gd")

static func slots() -> Array[StringName]:
	return EquipmentSlotTypesScript.inventory_slots()

static func can_equip_to_slot(item: ItemDef, slot: StringName) -> bool:
	return item != null and item.equip_slot == slot

static func to_equipment_dict(item: ItemDef) -> Dictionary:
	if item == null:
		return {}
	var d := {
		"id": item.id,
		"name": item.display_name,
		"kind": item.kind,
	}
	for k in item.stats.keys():
		d[k] = item.stats[k]
	return d
