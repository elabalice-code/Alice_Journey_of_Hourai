# Overrides room transitions to create confusing maps. Takes effect when player is inside the area. It has to be placed in the middle of room transition.
extends Area2D
const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

## Target room for the loop.
@export_file("room_link") var loop_target
## How much the player should be moved after looping. You can't rely on automatic repositioning from transitions, this has to be set manually.
@export var loop_shift: Vector2
## Use Teleport when the loop goes out of map and Replace when it connects to another room.
@export_enum("Teleport", "Replace") var loop_mode: int

## Hack. See [method on_room_changed].
static var block_loop: bool
## If [code]true[/code], the room transition will be overridden.
var loop_active: bool

func _ready() -> void:
	if loop_mode == 0:
		MetSys.cell_changed.connect(on_cell_changed.unbind(1))
	else:
		MetSys.room_changed.connect(on_room_changed.unbind(1))

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group(&"player"):
		loop_active = true
		if loop_mode == 1:
			var workbench := WorkbenchService.get_singleton()
			if workbench == null:
				return
			workbench.send({
				"type": MessageTypes.TYPE_SET_LOOP_TARGET,
				"loop_target": loop_target
			})

func _on_body_exited(body: Node2D) -> void:
	if body.is_in_group(&"player"):
		loop_active = false
		var workbench := WorkbenchService.get_singleton()
		if workbench == null:
			return
		var game := workbench.get_service(&"game") as Game
		if game == null:
			return
		if loop_mode == 1 and not game.map_changing:
			workbench.send({"type": MessageTypes.TYPE_CLEAR_LOOP_TARGET})

func on_cell_changed():
	if loop_active:
		loop_active = false
		
		if loop_target != MetSys.get_current_room_id():
			var workbench := WorkbenchService.get_singleton()
			if workbench == null:
				return
			workbench.send({
				"type": MessageTypes.TYPE_LOAD_ROOM_REQUEST,
				"target_map": loop_target
			})
			workbench.send({
				"type": MessageTypes.TYPE_SHIFT_PLAYER_REQUEST,
				"delta": loop_shift
			})

func on_room_changed():
	if loop_active and not block_loop:
		loop_active = false
		# Prevents automatically shifting the player.
		MetSys.current_room.queue_free()
		MetSys.current_room = null
		
		var workbench := WorkbenchService.get_singleton()
		if workbench == null:
			return
		workbench.send({
			"type": MessageTypes.TYPE_SHIFT_PLAYER_REQUEST,
			"delta": loop_shift
		})
		# Ugly workaround for physics bug that causes area to detect at wrong position.
		block_loop = true
		get_tree().create_timer(0.05).timeout.connect(get_script().set.bind(&"block_loop", false))
