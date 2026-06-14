extends Control

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

@onready var _player_bar: TextureProgressBar = $PlayerBar
@onready var _enemy_bar: TextureProgressBar = $EnemyBar
@onready var _player_label: Label = $PlayerLabel
@onready var _enemy_label: Label = $EnemyLabel
@onready var _result_label: Label = $ResultLabel

var _player: Node
var _player_path: String = ""
var _player_state_ready: bool = false
var _enemy: Node
var _enemy_path: String = ""
var _enemy_state_ready: bool = false
var _workbench: WorkbenchService

func _ready() -> void:
	_apply_textures()
	_workbench = WorkbenchService.get_singleton()
	if _workbench != null and not _workbench.message_published.is_connected(_on_message):
		_workbench.message_published.connect(_on_message)
	_bind_player()
	_bind_enemy()
	if get_tree().has_signal("node_added"):
		get_tree().node_added.connect(_on_tree_node_added)
	if get_tree().has_signal("node_removed"):
		get_tree().node_removed.connect(_on_tree_node_removed)
	_refresh()

func show_result(text: String, seconds: float = 2.0) -> void:
	if _result_label == null:
		return
	_result_label.text = text
	_result_label.visible = true
	await get_tree().create_timer(maxf(0.05, seconds)).timeout
	if is_instance_valid(_result_label):
		_result_label.visible = false

func _apply_textures() -> void:
	var bg := load("res://CoreEngine/Sprites/UI/HPBar_BG.png") as Texture2D
	var fill_p := load("res://CoreEngine/Sprites/UI/HPBar_Fill_Player.png") as Texture2D
	var fill_e := load("res://CoreEngine/Sprites/UI/HPBar_Fill_Enemy.png") as Texture2D
	if bg != null and _player_bar != null:
		_player_bar.texture_under = bg
		_player_bar.texture_progress = fill_p
	if bg != null and _enemy_bar != null:
		_enemy_bar.texture_under = bg
		_enemy_bar.texture_progress = fill_e

func _bind_player() -> void:
	_player = get_tree().get_first_node_in_group(&"player")
	_player_path = ""
	_player_state_ready = false
	if _player != null:
		_player_path = str(_player.get_path())
	_request_sync(_player)

func _bind_enemy() -> void:
	var enemies := get_tree().get_nodes_in_group(&"enemy")
	_enemy = enemies[0] if enemies.size() > 0 else null
	_enemy_path = ""
	_enemy_state_ready = false
	if _enemy != null:
		_enemy_path = str(_enemy.get_path())
	_request_sync(_enemy)

func _on_tree_node_added(node: Node) -> void:
	if _player == null and node.is_in_group(&"player"):
		_bind_player()
		_refresh()
		return
	if _enemy == null and node.is_in_group(&"enemy"):
		_bind_enemy()
		_refresh()

func _on_tree_node_removed(node: Node) -> void:
	if node == _enemy:
		_enemy = null
		_enemy_path = ""
		_enemy_state_ready = false
		_refresh()

func _refresh() -> void:
	_update_player_ui()
	_update_enemy_ui()

func _update_player_ui() -> void:
	if _player_path.is_empty() or not _player_state_ready:
		_player_bar.visible = false
		_player_label.visible = false
		return
	_player_bar.visible = true
	_player_label.visible = true
	var hp := float(_player_bar.value)
	var max_hp := float(_player_bar.max_value)
	_player_bar.max_value = max_hp
	_player_bar.value = hp
	var armor := float(_player_bar.get_meta(&"armor", 0.0))
	_player_label.text = "玩家 HP %d/%d  护甲 %d" % [int(round(hp)), int(round(max_hp)), int(round(armor))]

func _update_enemy_ui() -> void:
	if _enemy_path.is_empty() or not _enemy_state_ready:
		_enemy_bar.visible = false
		_enemy_label.visible = false
		return
	_enemy_bar.visible = true
	_enemy_label.visible = true
	var hp := float(_enemy_bar.value)
	var max_hp := float(_enemy_bar.max_value)
	_enemy_bar.max_value = max_hp
	_enemy_bar.value = hp
	var armor := float(_enemy_bar.get_meta(&"armor", 0.0))
	_enemy_label.text = "敌人 HP %d/%d  护甲 %d" % [int(round(hp)), int(round(max_hp)), int(round(armor))]

func _request_sync(target: Node) -> void:
	if _workbench == null or target == null or not is_instance_valid(target):
		return
	_workbench.send({
		"type": ActorFramework.TYPE_COMBAT_SYNC_REQUEST,
		"target": target,
	})

func _on_message(message: Dictionary) -> void:
	var t: StringName = message.get("type", &"")
	if t != ActorFramework.TYPE_COMBAT_STATE_CHANGED:
		return
	var tp := str(message.get("target_path", ""))
	if tp.is_empty():
		return
	var hp := float(message.get("hp", 0.0))
	var max_hp := float(message.get("max_hp", 0.0))
	var armor := float(message.get("armor", 0.0))
	if tp == _player_path:
		_player_state_ready = true
		_player_bar.max_value = max_hp
		_player_bar.value = hp
		_player_bar.set_meta(&"armor", armor)
		_update_player_ui()
		return
	if tp == _enemy_path:
		_enemy_state_ready = true
		_enemy_bar.max_value = max_hp
		_enemy_bar.value = hp
		_enemy_bar.set_meta(&"armor", armor)
		_update_enemy_ui()

func _exit_tree() -> void:
	if _workbench != null and _workbench.message_published.is_connected(_on_message):
		_workbench.message_published.disconnect(_on_message)
