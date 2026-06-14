extends Node
class_name PlayerInventory

signal changed()

const ItemCatalog = preload("res://CoreEngine/Scripts/Items/ItemCatalog.gd")
const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

const BAG_SIZE: int = 64
const RUNE_SIZE: int = 6
const LOCKED_RUNE_SLOT_COUNT: int = 3

var bag: Array[ItemDef] = []
var equipped: Dictionary = {}
var runes: Array[ItemDef] = []

var _workbench: WorkbenchService
var _inventory_manager: InventoryManagerActor

func _ready() -> void:
	bag.resize(BAG_SIZE)
	runes.resize(RUNE_SIZE)
	_workbench = WorkbenchService.get_singleton()
	if _workbench != null:
		_inventory_manager = _workbench.get_service(&"inventory_manager") as InventoryManagerActor
		_workbench.message_published.connect(_on_message)
	_bootstrap_workplace()
	if _inventory_manager != null:
		_inventory_manager.ensure_initialized()
	_sync_from_workplace()
	changed.emit()

func _exit_tree() -> void:
	if _workbench != null and _workbench.message_published.is_connected(_on_message):
		_workbench.message_published.disconnect(_on_message)

func get_bag_item(index: int) -> ItemDef:
	_sync_from_workplace()
	if index < 0 or index >= bag.size():
		return null
	return bag[index]

func set_bag_item(index: int, item: ItemDef) -> void:
	if index < 0 or index >= bag.size():
		return
	if _workbench != null:
		_workbench.send({
			"type": ActorFramework.TYPE_ITEM_ACTION_REQUEST,
			"action": &"set_bag_item",
			"index": index,
			"item": item,
		})
		return
	bag[index] = item
	changed.emit()

func equip_from_bag(index: int, slot: StringName) -> bool:
	_sync_from_workplace()
	if index < 0 or index >= bag.size():
		return false
	var item := bag[index]
	if item == null:
		return false
	if item.equip_slot != slot:
		return false
	if _workbench != null:
		_workbench.send({
			"type": ActorFramework.TYPE_ITEM_ACTION_REQUEST,
			"action": &"equip_from_bag",
			"index": index,
			"slot": slot,
		})
		return true
	return false

func unequip_to_bag(slot: StringName) -> bool:
	_sync_from_workplace()
	var item := equipped.get(slot) as ItemDef
	if item == null:
		return false
	if not bag.any(func(it): return it == null):
		return false
	if _workbench != null:
		_workbench.send({
			"type": ActorFramework.TYPE_ITEM_ACTION_REQUEST,
			"action": &"unequip_to_bag",
			"slot": slot,
		})
		return true
	return false

func place_rune_from_bag(index: int, rune_slot_index: int) -> bool:
	_sync_from_workplace()
	if rune_slot_index < 0 or rune_slot_index >= runes.size():
		return false
	if rune_slot_index < LOCKED_RUNE_SLOT_COUNT:
		return false
	if index < 0 or index >= bag.size():
		return false
	var item := bag[index]
	if item == null or item.kind != &"rune":
		return false
	if _workbench != null:
		_workbench.send({
			"type": ActorFramework.TYPE_ITEM_ACTION_REQUEST,
			"action": &"place_rune_from_bag",
			"index": index,
			"rune_slot_index": rune_slot_index,
		})
		return true
	return false

func remove_rune_to_bag(rune_slot_index: int) -> bool:
	_sync_from_workplace()
	if rune_slot_index < 0 or rune_slot_index >= runes.size():
		return false
	if rune_slot_index < LOCKED_RUNE_SLOT_COUNT:
		return false
	if runes[rune_slot_index] == null:
		return false
	if not bag.any(func(it): return it == null):
		return false
	if _workbench != null:
		_workbench.send({
			"type": ActorFramework.TYPE_ITEM_ACTION_REQUEST,
			"action": &"remove_rune_to_bag",
			"rune_slot_index": rune_slot_index,
		})
		return true
	return false

func apply_equipment_to_combatant(combatant: Node) -> void:
	if combatant == null:
		return
	_sync_from_workplace()
	var slots: Array[StringName] = [ &"head", &"clothes", &"shoes", &"weapon" ]
	for s in slots:
		if combatant.has_method("unequip"):
			combatant.call("unequip", s)
	for s in slots:
		var item := equipped.get(s) as ItemDef
		if item == null:
			continue
		if combatant.has_method("equip"):
			combatant.call("equip", s, _to_equipment_dict(item))

func _to_equipment_dict(item: ItemDef) -> Dictionary:
	var d := {
		"id": item.id,
		"name": item.display_name,
		"kind": item.kind,
	}
	for k in item.stats.keys():
		d[k] = item.stats[k]
	return d

func _on_message(message: Dictionary) -> void:
	var t: StringName = message.get("type", &"")
	if t != ActorFramework.TYPE_INVENTORY_UPDATED:
		return
	_sync_from_workplace()
	changed.emit()

func _sync_from_workplace() -> void:
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var data: ActorFramework.InventoryData = global_wp.inventory
	if data == null:
		return
	if data.bag.size() != BAG_SIZE:
		data.bag.resize(BAG_SIZE)
	if data.runes.size() != RUNE_SIZE:
		data.runes.resize(RUNE_SIZE)
	bag = data.bag
	equipped = data.equipped
	runes = data.runes

func _bootstrap_workplace() -> void:
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var data: ActorFramework.InventoryData = global_wp.inventory
	if data == null:
		return
	if data.bag.size() != BAG_SIZE:
		data.bag.resize(BAG_SIZE)
	if data.runes.size() != RUNE_SIZE:
		data.runes.resize(RUNE_SIZE)
	if _has_any_inventory(data):
		return
	if _has_any_local_inventory():
		data.bag = bag
		data.equipped = equipped
		data.runes = runes

func _has_any_inventory(data: ActorFramework.InventoryData) -> bool:
	if data == null:
		return false
	if not data.equipped.is_empty():
		return true
	for it in data.bag:
		if it != null:
			return true
	for r in data.runes:
		if r != null:
			return true
	return false

func _has_any_local_inventory() -> bool:
	if not equipped.is_empty():
		return true
	for it in bag:
		if it != null:
			return true
	for r in runes:
		if r != null:
			return true
	return false
