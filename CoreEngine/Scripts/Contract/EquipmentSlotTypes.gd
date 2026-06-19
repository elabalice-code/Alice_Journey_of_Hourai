extends RefCounted
class_name EquipmentSlotTypes

const HEAD: StringName = &"head"
const CLOTHES: StringName = &"clothes"
const SHOES: StringName = &"shoes"
const WEAPON: StringName = &"weapon"
const ARMOR: StringName = &"armor"
const SHIELD: StringName = &"shield"

const INVENTORY_SLOTS: Array[StringName] = [HEAD, CLOTHES, SHOES, WEAPON]

static func inventory_slots() -> Array[StringName]:
	return INVENTORY_SLOTS.duplicate()

static func display_name(slot: StringName) -> String:
	match slot:
		HEAD:
			return "Head"
		CLOTHES:
			return "Clothes"
		SHOES:
			return "Shoes"
		WEAPON:
			return "Weapon"
		ARMOR:
			return "Armor"
		SHIELD:
			return "Shield"
	return String(slot)
