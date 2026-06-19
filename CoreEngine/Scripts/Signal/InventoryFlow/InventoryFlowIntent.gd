extends RefCounted
class_name InventoryFlowIntent

const KIND_NONE: StringName = &"none"
const KIND_PICKUP: StringName = &"pickup"
const KIND_SET_BAG_ITEM: StringName = &"set_bag_item"
const KIND_SWAP_BAG: StringName = &"swap_bag"
const KIND_EQUIP_FROM_BAG: StringName = &"equip_from_bag"
const KIND_UNEQUIP_TO_BAG: StringName = &"unequip_to_bag"
const KIND_PLACE_RUNE_FROM_BAG: StringName = &"place_rune_from_bag"
const KIND_REMOVE_RUNE_TO_BAG: StringName = &"remove_rune_to_bag"
const KIND_ENSURE_INITIALIZED: StringName = &"ensure_initialized"
const KIND_COMPOSE: StringName = &"compose"
const KIND_DECOMPOSE: StringName = &"decompose"

var kind: StringName = KIND_NONE
var payload: Dictionary = {}

static func make(p_kind: StringName, p_payload: Dictionary = {}) -> InventoryFlowIntent:
	var intent := InventoryFlowIntent.new()
	intent.kind = p_kind
	intent.payload = p_payload.duplicate(true)
	return intent

func is_valid() -> bool:
	return kind != KIND_NONE
