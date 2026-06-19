extends Node
class_name PlayerInventory

signal changed()

const ItemCatalog = preload("res://CoreEngine/Scripts/Items/ItemCatalog.gd")
const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const InventoryActionTypesScript = preload("res://CoreEngine/Scripts/Contract/InventoryActionTypes.gd")
const EquipmentShapeScript = preload("res://CoreEngine/Scripts/Helper/Inventory/EquipmentShape.gd")
const CombatantPortScript = preload("res://CoreEngine/Scripts/Actor/CombatantPort.gd")

const BAG_SIZE: int = InventoryData.BAG_SIZE
const RUNE_SIZE: int = InventoryData.RUNE_SIZE
const LOCKED_RUNE_SLOT_COUNT: int = InventoryData.LOCKED_RUNE_SLOT_COUNT

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
			"type": MessageTypes.TYPE_ITEM_ACTION_REQUEST,
			"action": InventoryActionTypesScript.SET_BAG_ITEM,
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
	if not EquipmentShapeScript.can_equip_to_slot(item, slot):
		return false
	if _workbench != null:
		_workbench.send({
			"type": MessageTypes.TYPE_ITEM_ACTION_REQUEST,
			"action": InventoryActionTypesScript.EQUIP_FROM_BAG,
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
			"type": MessageTypes.TYPE_ITEM_ACTION_REQUEST,
			"action": InventoryActionTypesScript.UNEQUIP_TO_BAG,
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
			"type": MessageTypes.TYPE_ITEM_ACTION_REQUEST,
			"action": InventoryActionTypesScript.PLACE_RUNE_FROM_BAG,
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
			"type": MessageTypes.TYPE_ITEM_ACTION_REQUEST,
			"action": InventoryActionTypesScript.REMOVE_RUNE_TO_BAG,
			"rune_slot_index": rune_slot_index,
		})
		return true
	return false

func apply_equipment_to_combatant(combatant: Combatant) -> void:
	_sync_from_workplace()
	var port := CombatantPortScript.new()
	port.target = combatant.get_parent() if combatant != null else null
	port.combatant = combatant
	port.sync_equipment(equipped)

func _on_message(message: Dictionary) -> void:
	var t: StringName = message.get("type", &"")
	if t != MessageTypes.TYPE_INVENTORY_UPDATED:
		return
	_sync_from_workplace()
	changed.emit()

func _sync_from_workplace() -> void:
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var data: InventoryData = global_wp.inventory
	if data == null:
		return
	data.ensure_shape()
	bag = data.bag
	equipped = data.equipped
	runes = data.runes

func _bootstrap_workplace() -> void:
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	var data: InventoryData = global_wp.inventory
	if data == null:
		return
	data.ensure_shape()
	if data.has_any_inventory():
		return
	if _has_any_local_inventory():
		data.bag = bag
		data.equipped = equipped
		data.runes = runes

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
