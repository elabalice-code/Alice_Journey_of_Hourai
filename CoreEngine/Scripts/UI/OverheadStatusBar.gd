extends Node2D

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

@export var combatant_path: NodePath = ^"../Combatant"
@export var y_offset: float = -70.0

@onready var _bar: TextureProgressBar = $HPBar
@onready var _label: Label = $Label

var _combatant: Node
var _workbench: WorkbenchService
var _target_path_str: String = ""

func _ready() -> void:
	position = Vector2(0.0, y_offset)
	_apply_textures()
	_combatant = get_node_or_null(combatant_path)
	_workbench = WorkbenchService.get_singleton()
	if _workbench != null:
		_target_path_str = str(get_parent().get_path())
		if not _workbench.message_published.is_connected(_on_message):
			_workbench.message_published.connect(_on_message)
		_workbench.send({
			"type": MessageTypes.TYPE_COMBAT_SYNC_REQUEST,
			"target": get_parent(),
		})
		return
	_bind_legacy()
	_refresh_legacy()

func _apply_textures() -> void:
	var bg := load("res://CoreEngine/Sprites/UI/HPBar_BG.png") as Texture2D
	var fill := load("res://CoreEngine/Sprites/UI/HPBar_Fill_Enemy.png") as Texture2D
	if bg != null and _bar != null:
		_bar.texture_under = bg
		_bar.texture_progress = fill

func _bind_legacy() -> void:
	_combatant = get_node_or_null(combatant_path)
	if _combatant == null:
		return
	if _combatant.has_signal("health_changed"):
		var cb := Callable(self, &"_on_changed")
		if not _combatant.health_changed.is_connected(cb):
			_combatant.health_changed.connect(cb)
	if _combatant.has_signal("equipment_changed"):
		var cb2 := Callable(self, &"_on_changed")
		if not _combatant.equipment_changed.is_connected(cb2):
			_combatant.equipment_changed.connect(cb2)

func _on_changed(_a = null, _b = null) -> void:
	_refresh_legacy()

func _refresh_legacy() -> void:
	if _combatant == null:
		visible = false
		return
	visible = true
	var hp := float(_combatant.get("hp"))
	var max_hp := float(_combatant.get("max_hp"))
	_bar.max_value = max_hp
	_bar.value = hp
	var armor := 0.0
	if _combatant.has_method("get_total_armor"):
		armor = float(_combatant.get_total_armor())
	_label.text = "HP %d/%d  护甲 %d" % [int(round(hp)), int(round(max_hp)), int(round(armor))]

func _exit_tree() -> void:
	if _workbench != null and _workbench.message_published.is_connected(_on_message):
		_workbench.message_published.disconnect(_on_message)

func _on_message(message: Dictionary) -> void:
	var t: StringName = message.get("type", &"")
	if t != MessageTypes.TYPE_COMBAT_STATE_CHANGED:
		return
	var tp := str(message.get("target_path", ""))
	if tp.is_empty() or tp != _target_path_str:
		return
	var hp := float(message.get("hp", 0.0))
	var max_hp := float(message.get("max_hp", 0.0))
	var armor := float(message.get("armor", 0.0))
	visible = true
	_bar.max_value = max_hp
	_bar.value = hp
	_label.text = "HP %d/%d  护甲 %d" % [int(round(hp)), int(round(max_hp)), int(round(armor))]
