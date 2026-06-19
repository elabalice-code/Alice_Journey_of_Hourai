extends RefCounted
class_name InventoryManagerActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")
const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const ItemCatalog = preload("res://CoreEngine/Scripts/Items/ItemCatalog.gd")
const PlayerInventory = preload("res://CoreEngine/Scripts/Items/PlayerInventory.gd")
const InventoryFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowProducers.gd")
const InventoryFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/InventoryFlow/InventoryFlowRouter.gd")
const InventorySlotsScript = preload("res://CoreEngine/Scripts/Helper/Inventory/InventorySlots.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		ensure_initialized()
		_workbench.register_actor(self, [
			MessageTypes.TYPE_ITEM_ACTION_REQUEST
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var inv_data: ActorFramework.InventoryData = global_wp.inventory
	_ensure_inventory_shape(inv_data)
	var frame: InventoryFlowSignalFrame = InventoryFlowProducersScript.from_workplace(workplace)
	var intent: InventoryFlowIntent = InventoryFlowRouterScript.route(frame)
	_execute_intent(intent)

func _execute_intent(intent: InventoryFlowIntent) -> void:
	if intent == null or not intent.is_valid():
		return
	match intent.kind:
		InventoryFlowIntent.KIND_PICKUP:
			pickup(
				intent.payload.get("item_id", &"") as StringName,
				int(intent.payload.get("count", 1))
			)
		InventoryFlowIntent.KIND_SET_BAG_ITEM:
			set_bag_item(
				int(intent.payload.get("index", -1)),
				intent.payload.get("item") as ItemDef
			)
		InventoryFlowIntent.KIND_SWAP_BAG:
			swap_bag_items(
				int(intent.payload.get("a_index", -1)),
				int(intent.payload.get("b_index", -1))
			)
		InventoryFlowIntent.KIND_EQUIP_FROM_BAG:
			equip_from_bag(
				int(intent.payload.get("index", -1)),
				intent.payload.get("slot", &"") as StringName
			)
		InventoryFlowIntent.KIND_UNEQUIP_TO_BAG:
			unequip_to_bag(intent.payload.get("slot", &"") as StringName)
		InventoryFlowIntent.KIND_PLACE_RUNE_FROM_BAG:
			place_rune_from_bag(
				int(intent.payload.get("index", -1)),
				int(intent.payload.get("rune_slot_index", -1))
			)
		InventoryFlowIntent.KIND_REMOVE_RUNE_TO_BAG:
			remove_rune_to_bag(int(intent.payload.get("rune_slot_index", -1)))
		InventoryFlowIntent.KIND_ENSURE_INITIALIZED:
			ensure_initialized()
		InventoryFlowIntent.KIND_COMPOSE:
			pass
		InventoryFlowIntent.KIND_DECOMPOSE:
			pass

func ensure_initialized() -> void:
	var data := _get_data()
	if data == null:
		return
	_ensure_inventory_shape(data)
	if data.bag.any(func(it): return it != null):
		return
	var starting := ItemCatalog.get_starting_bag_items()
	for it in starting:
		_add_to_first_empty(data, it)
	_notify_update()

func get_bag_item(index: int) -> ItemDef:
	var data := _get_data()
	if data == null:
		return null
	_ensure_inventory_shape(data)
	if not InventorySlotsScript.is_valid_index(data.bag, index):
		return null
	return data.bag[index]

func set_bag_item(index: int, item: ItemDef) -> void:
	var data := _get_data()
	if data == null:
		return
	_ensure_inventory_shape(data)
	if not InventorySlotsScript.is_valid_index(data.bag, index):
		return
	data.bag[index] = item
	_notify_update()

func swap_bag_items(a_index: int, b_index: int) -> bool:
	var data := _get_data()
	if data == null:
		return false
	_ensure_inventory_shape(data)
	if not InventorySlotsScript.is_valid_index(data.bag, a_index):
		return false
	if not InventorySlotsScript.is_valid_index(data.bag, b_index):
		return false
	if a_index == b_index:
		return true
	var a := data.bag[a_index]
	var b := data.bag[b_index]
	data.bag[a_index] = b
	data.bag[b_index] = a
	_notify_update()
	return true

func equip_from_bag(index: int, slot: StringName) -> bool:
	var data := _get_data()
	if data == null:
		return false
	_ensure_inventory_shape(data)
	if not InventorySlotsScript.is_valid_index(data.bag, index):
		return false
	var item := data.bag[index]
	if item == null:
		return false
	if item.equip_slot != slot:
		return false
	var prev := data.equipped.get(slot) as ItemDef
	data.equipped[slot] = item
	data.bag[index] = prev
	_notify_update()
	return true

func unequip_to_bag(slot: StringName) -> bool:
	var data := _get_data()
	if data == null:
		return false
	_ensure_inventory_shape(data)
	var item := data.equipped.get(slot) as ItemDef
	if item == null:
		return false
	if not _add_to_first_empty(data, item):
		return false
	data.equipped.erase(slot)
	_notify_update()
	return true

func place_rune_from_bag(index: int, rune_slot_index: int) -> bool:
	var data := _get_data()
	if data == null:
		return false
	_ensure_inventory_shape(data)
	if not InventorySlotsScript.is_valid_index(data.runes, rune_slot_index):
		return false
	if rune_slot_index < PlayerInventory.LOCKED_RUNE_SLOT_COUNT:
		return false
	if not InventorySlotsScript.is_valid_index(data.bag, index):
		return false
	var item := data.bag[index]
	if item == null or item.kind != &"rune":
		return false
	var prev := data.runes[rune_slot_index]
	data.runes[rune_slot_index] = item
	data.bag[index] = prev
	_notify_update()
	return true

func remove_rune_to_bag(rune_slot_index: int) -> bool:
	var data := _get_data()
	if data == null:
		return false
	_ensure_inventory_shape(data)
	if not InventorySlotsScript.is_valid_index(data.runes, rune_slot_index):
		return false
	if rune_slot_index < PlayerInventory.LOCKED_RUNE_SLOT_COUNT:
		return false
	var item := data.runes[rune_slot_index]
	if item == null:
		return false
	if not _add_to_first_empty(data, item):
		return false
	data.runes[rune_slot_index] = null
	_notify_update()
	return true

func pickup(item_id: StringName, count: int = 1) -> void:
	var data := _get_data()
	if data == null:
		return
	_ensure_inventory_shape(data)
	var defs := ItemCatalog.get_item_defs()
	if not defs.has(item_id):
		return
	var item := defs[item_id] as ItemDef
	if item == null:
		return
	var n: int = maxi(1, count)
	for _i in range(n):
		if not _add_to_first_empty(data, item):
			break
	_notify_update()

func _get_data() -> ActorFramework.InventoryData:
	if _workbench == null:
		return null
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return null
	return global_wp.inventory

func _ensure_inventory_shape(data: ActorFramework.InventoryData) -> void:
	InventorySlotsScript.ensure_items_size(data.bag, PlayerInventory.BAG_SIZE)
	InventorySlotsScript.ensure_items_size(data.runes, PlayerInventory.RUNE_SIZE)

func _add_to_first_empty(data: ActorFramework.InventoryData, item: ItemDef) -> bool:
	var index: int = InventorySlotsScript.first_empty_index(data.bag)
	if index < 0:
		return false
	data.bag[index] = item
	return true

func _notify_update() -> void:
	if _workbench:
		_apply_equipment_to_player_combatant()
		_workbench.send({
			"type": MessageTypes.TYPE_INVENTORY_UPDATED
		})

func _apply_equipment_to_player_combatant() -> void:
	if _workbench == null:
		return
	var player := _workbench.get_service(&"player") as Node
	if not is_instance_valid(player):
		return
	var combatant := player.get_node_or_null(^"Combatant")
	if not is_instance_valid(combatant):
		return
	var data := _get_data()
	if data == null:
		return
	var slots: Array[StringName] = [ &"head", &"clothes", &"shoes", &"weapon" ]
	for s in slots:
		if combatant.has_method(&"unequip"):
			combatant.call(&"unequip", s)
	for s in slots:
		var item := data.equipped.get(s) as ItemDef
		if item == null:
			continue
		if combatant.has_method(&"equip"):
			combatant.call(&"equip", s, _to_equipment_dict(item))

func _to_equipment_dict(item: ItemDef) -> Dictionary:
	var d := {
		"id": item.id,
		"name": item.display_name,
		"kind": item.kind,
	}
	for k in item.stats.keys():
		d[k] = item.stats[k]
	return d
