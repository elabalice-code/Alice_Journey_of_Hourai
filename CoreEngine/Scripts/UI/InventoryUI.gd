extends Control
class_name InventoryUI

const PlayerInventory = preload("res://CoreEngine/Scripts/Items/PlayerInventory.gd")
const EquipmentShapeScript = preload("res://CoreEngine/Scripts/Helper/Inventory/EquipmentShape.gd")
const EquipmentSlotTypesScript = preload("res://CoreEngine/Scripts/Contract/EquipmentSlotTypes.gd")

@onready var _dim: ColorRect = $Dim
@onready var _tab_equipment: Button = $Window/RootVBox/Header/TabEquipment
@onready var _tab_runes: Button = $Window/RootVBox/Header/TabRunes
@onready var _close_btn: Button = $Window/RootVBox/Header/Close
@onready var _equip_page: Control = $Window/RootVBox/Content/RightVBox/EquipmentPage
@onready var _rune_page: Control = $Window/RootVBox/Content/RightVBox/RunePage
@onready var _bag_grid: GridContainer = $Window/RootVBox/Content/BagVBox/BagGrid
@onready var _equip_grid: GridContainer = $Window/RootVBox/Content/RightVBox/EquipmentPage/EquipGrid
@onready var _rune_grid: GridContainer = $Window/RootVBox/Content/RightVBox/RunePage/RuneGrid
@onready var _selected_label: Label = $Window/RootVBox/SelectedLabel

var _bag_buttons: Array[Button] = []
var _equip_buttons: Dictionary = {}
var _rune_buttons: Array[Button] = []

var _selected_origin: StringName = &""
var _selected_index: int = -1

var _active_page: StringName = &"equipment"

var _player: Node
var _inventory: PlayerInventory
var _combatant: Node
var _inventory_changed_cb: Callable

func _ready() -> void:
	visible = false
	_inventory_changed_cb = Callable(self, &"_refresh")
	_dim.gui_input.connect(_on_dim_gui_input)
	_tab_equipment.pressed.connect(_on_tab_equipment)
	_tab_runes.pressed.connect(_on_tab_runes)
	_close_btn.pressed.connect(close)
	_build_ui()
	_bind_player()
	_show_equipment_page()
	_refresh()

func _exit_tree() -> void:
	if is_instance_valid(_inventory) and _inventory.changed.is_connected(_inventory_changed_cb):
		_inventory.changed.disconnect(_inventory_changed_cb)

func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event is InputEventKey and (event as InputEventKey).pressed:
		var k := event as InputEventKey
		if k.keycode == KEY_ESCAPE:
			close()
			get_viewport().set_input_as_handled()

func toggle() -> void:
	if visible:
		close()
	else:
		open()

func toggle_bag() -> void:
	toggle()

func toggle_equipment() -> void:
	if not visible:
		open()
		_show_equipment_page()
		_refresh()
		return
	if _active_page != &"equipment":
		_show_equipment_page()
		_refresh()
		return
	close()

func open() -> void:
	_bind_player()
	visible = true
	grab_focus()
	if is_instance_valid(_player) and "event" in _player:
		_player.event = true
	_refresh()

func close() -> void:
	visible = false
	_clear_selection()
	if is_instance_valid(_player) and "event" in _player:
		_player.event = false

func _bind_player() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	if is_instance_valid(_inventory) and _inventory.changed.is_connected(_inventory_changed_cb):
		_inventory.changed.disconnect(_inventory_changed_cb)
	_player = workbench.get_service(&"player")
	if not is_instance_valid(_player):
		return
	_inventory = _player.get_node_or_null(^"Inventory") as PlayerInventory
	_combatant = _player.get_node_or_null(^"Combatant")
	if not is_instance_valid(_inventory):
		_inventory = null
		return
	if not _inventory.changed.is_connected(_inventory_changed_cb):
		_inventory.changed.connect(_inventory_changed_cb)

func _build_ui() -> void:
	_build_bag_slots()
	_build_equip_slots()
	_build_rune_slots()

func _build_bag_slots() -> void:
	_bag_buttons.clear()
	for c in _bag_grid.get_children():
		c.queue_free()
	for i in range(64):
		var b := _make_slot_button()
		b.pressed.connect(Callable(self, &"_on_bag_slot_pressed").bind(i))
		_bag_grid.add_child(b)
		_bag_buttons.append(b)

func _build_equip_slots() -> void:
	_equip_buttons.clear()
	for c in _equip_grid.get_children():
		c.queue_free()
	var slots: Array[StringName] = EquipmentShapeScript.slots()
	var names := {
		&"head": "头饰",
		&"clothes": "衣服",
		&"shoes": "鞋子",
		&"weapon": "武器",
	}
	for s in slots:
		var b := _make_slot_button()
		b.text = names.get(s, EquipmentSlotTypesScript.display_name(s))
		b.pressed.connect(Callable(self, &"_on_equip_slot_pressed").bind(s))
		_equip_grid.add_child(b)
		_equip_buttons[s] = b

func _build_rune_slots() -> void:
	_rune_buttons.clear()
	for c in _rune_grid.get_children():
		c.queue_free()
	for i in range(6):
		var b := _make_slot_button()
		b.pressed.connect(Callable(self, &"_on_rune_slot_pressed").bind(i))
		_rune_grid.add_child(b)
		_rune_buttons.append(b)

func _make_slot_button() -> Button:
	var b := Button.new()
	b.custom_minimum_size = Vector2(36, 36)
	b.focus_mode = Control.FOCUS_NONE
	b.expand_icon = true
	return b

func _on_tab_equipment() -> void:
	_show_equipment_page()

func _on_tab_runes() -> void:
	_show_rune_page()

func _show_equipment_page() -> void:
	_equip_page.visible = true
	_rune_page.visible = false
	_active_page = &"equipment"

func _show_rune_page() -> void:
	_equip_page.visible = false
	_rune_page.visible = true
	_active_page = &"runes"

func _on_dim_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and (event as InputEventMouseButton).pressed:
		close()

func _on_bag_slot_pressed(index: int) -> void:
	if _inventory == null:
		return
	if _selected_origin == &"bag" and _selected_index >= 0 and _selected_index != index:
		var a := _inventory.get_bag_item(_selected_index)
		var b := _inventory.get_bag_item(index)
		_inventory.set_bag_item(_selected_index, b)
		_inventory.set_bag_item(index, a)
		_clear_selection()
		return
	_selected_origin = &"bag"
	_selected_index = index
	_refresh_selected_label()
	_refresh()

func _on_equip_slot_pressed(slot: StringName) -> void:
	if _inventory == null:
		return
	if _selected_origin == &"bag" and _selected_index >= 0:
		_inventory.equip_from_bag(_selected_index, slot)
		_clear_selection()
		return
	if _selected_origin == &"":
		_inventory.unequip_to_bag(slot)
	_refresh()

func _on_rune_slot_pressed(index: int) -> void:
	if _inventory == null:
		return
	if index < InventoryData.LOCKED_RUNE_SLOT_COUNT:
		return
	if _selected_origin == &"bag" and _selected_index >= 0:
		_inventory.place_rune_from_bag(_selected_index, index)
		_clear_selection()
		return
	if _selected_origin == &"":
		_inventory.remove_rune_to_bag(index)
	_refresh()

func _clear_selection() -> void:
	_selected_origin = &""
	_selected_index = -1
	_refresh_selected_label()
	_refresh()

func _refresh_selected_label() -> void:
	if _selected_origin == &"bag" and _selected_index >= 0 and _inventory != null:
		var it := _inventory.get_bag_item(_selected_index)
		if it != null:
			_selected_label.text = "已选择：%s" % it.display_name
			return
	_selected_label.text = "已选择：无"

func _refresh() -> void:
	if not is_instance_valid(_inventory):
		return
	for i in range(_bag_buttons.size()):
		var b := _bag_buttons[i]
		if not is_instance_valid(b):
			continue
		var bag_item := _inventory.get_bag_item(i)
		_apply_item_to_button(b, bag_item, "")
		if _selected_origin == &"bag" and _selected_index == i:
			b.disabled = false
	for slot in _equip_buttons.keys():
		var btn := _equip_buttons[slot] as Button
		if not is_instance_valid(btn):
			continue
		var equip_item := _inventory.equipped.get(slot) as ItemDef
		var fallback := btn.text
		_apply_item_to_button(btn, equip_item, fallback)
	for i in range(_rune_buttons.size()):
		var rune_btn := _rune_buttons[i]
		if not is_instance_valid(rune_btn):
			continue
		if i < InventoryData.LOCKED_RUNE_SLOT_COUNT:
			rune_btn.icon = null
			rune_btn.text = "锁定"
			rune_btn.disabled = true
		else:
			rune_btn.disabled = false
			var rune_item := _inventory.runes[i]
			_apply_item_to_button(rune_btn, rune_item, "符文")

func _apply_item_to_button(btn: Button, item: ItemDef, fallback_text: String) -> void:
	if not is_instance_valid(btn):
		return
	if item == null:
		btn.icon = null
		btn.text = fallback_text
		return
	var icon := item.get_icon()
	btn.icon = icon if is_instance_valid(icon) else null
	btn.text = ""
