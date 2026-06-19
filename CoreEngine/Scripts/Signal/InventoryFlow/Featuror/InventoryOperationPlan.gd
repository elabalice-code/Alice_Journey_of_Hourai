extends RefCounted
class_name InventoryOperationPlan

const InventoryFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowIntent.gd")
const InventorySlotsScript = preload("res://CoreEngine/Scripts/Helper/Inventory/InventorySlots.gd")
const EquipmentShapeScript = preload("res://CoreEngine/Scripts/Helper/Inventory/EquipmentShape.gd")

static func validate_intent(intent: InventoryFlowIntent, data: InventoryData) -> InventoryFlowIntent:
	if intent == null or not intent.is_valid():
		return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
	if data == null:
		if intent.kind == InventoryFlowIntentScript.KIND_ENSURE_INITIALIZED:
			return intent
		return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
	data.ensure_shape()
	match intent.kind:
		InventoryFlowIntentScript.KIND_SET_BAG_ITEM:
			if not InventorySlotsScript.is_valid_index(data.bag, int(intent.payload.get("index", -1))):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
		InventoryFlowIntentScript.KIND_SWAP_BAG:
			if not InventorySlotsScript.is_valid_index(data.bag, int(intent.payload.get("a_index", -1))):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			if not InventorySlotsScript.is_valid_index(data.bag, int(intent.payload.get("b_index", -1))):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
		InventoryFlowIntentScript.KIND_EQUIP_FROM_BAG:
			var index := int(intent.payload.get("index", -1))
			if not InventorySlotsScript.is_valid_index(data.bag, index):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			if not EquipmentShapeScript.can_equip_to_slot(data.bag[index], intent.payload.get("slot", &"") as StringName):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
		InventoryFlowIntentScript.KIND_UNEQUIP_TO_BAG:
			var slot: StringName = intent.payload.get("slot", &"")
			if data.equipped.get(slot) == null:
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			if not InventorySlotsScript.has_empty_slot(data.bag):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
		InventoryFlowIntentScript.KIND_PLACE_RUNE_FROM_BAG:
			var bag_index := int(intent.payload.get("index", -1))
			var rune_index := int(intent.payload.get("rune_slot_index", -1))
			if not _is_unlocked_rune_index(data, rune_index):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			if not InventorySlotsScript.is_valid_index(data.bag, bag_index):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			var item := data.bag[bag_index] as ItemDef
			if item == null or item.kind != &"rune":
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
		InventoryFlowIntentScript.KIND_REMOVE_RUNE_TO_BAG:
			var remove_index := int(intent.payload.get("rune_slot_index", -1))
			if not _is_unlocked_rune_index(data, remove_index):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			if data.runes[remove_index] == null:
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
			if not InventorySlotsScript.has_empty_slot(data.bag):
				return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
	return intent

static func _is_unlocked_rune_index(data: InventoryData, index: int) -> bool:
	if not InventorySlotsScript.is_valid_index(data.runes, index):
		return false
	return index >= InventoryData.LOCKED_RUNE_SLOT_COUNT
