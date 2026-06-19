extends RefCounted
class_name InventoryActionPlan

const InventoryActionTypesScript = preload("res://CoreEngine/Scripts/Contract/InventoryActionTypes.gd")
const InventoryFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowIntent.gd")

static func build_action_intent(msg: Dictionary) -> InventoryFlowIntent:
	var action: StringName = msg.get("action", &"")
	match action:
		InventoryActionTypesScript.PICKUP:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_PICKUP, {
				"item_id": msg.get("item_id", &"") as StringName,
				"count": int(msg.get("count", 1))
			})
		InventoryActionTypesScript.SET_BAG_ITEM:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_SET_BAG_ITEM, {
				"index": int(msg.get("index", -1)),
				"item": msg.get("item")
			})
		InventoryActionTypesScript.SWAP_BAG:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_SWAP_BAG, {
				"a_index": int(msg.get("a_index", -1)),
				"b_index": int(msg.get("b_index", -1))
			})
		InventoryActionTypesScript.EQUIP_FROM_BAG:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_EQUIP_FROM_BAG, {
				"index": int(msg.get("index", -1)),
				"slot": msg.get("slot", &"") as StringName
			})
		InventoryActionTypesScript.UNEQUIP_TO_BAG:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_UNEQUIP_TO_BAG, {
				"slot": msg.get("slot", &"") as StringName
			})
		InventoryActionTypesScript.PLACE_RUNE_FROM_BAG:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_PLACE_RUNE_FROM_BAG, {
				"index": int(msg.get("index", -1)),
				"rune_slot_index": int(msg.get("rune_slot_index", -1))
			})
		InventoryActionTypesScript.REMOVE_RUNE_TO_BAG:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_REMOVE_RUNE_TO_BAG, {
				"rune_slot_index": int(msg.get("rune_slot_index", -1))
			})
		InventoryActionTypesScript.ENSURE_INITIALIZED:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_ENSURE_INITIALIZED)
		InventoryActionTypesScript.COMPOSE:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_COMPOSE, {
				"recipe_id": msg.get("recipe_id", &"") as StringName,
				"source_indices": msg.get("source_indices", [])
			})
		InventoryActionTypesScript.DECOMPOSE:
			return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_DECOMPOSE, {
				"index": int(msg.get("index", -1)),
				"recipe_id": msg.get("recipe_id", &"") as StringName
			})
	return InventoryFlowIntentScript.make(InventoryFlowIntentScript.KIND_NONE)
