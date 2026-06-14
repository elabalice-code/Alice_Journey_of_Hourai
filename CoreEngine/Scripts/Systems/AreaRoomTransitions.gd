extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysModule.gd"

const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")

var scroll_time: float = 0.5

var _player: Node2D
var _workbench: WorkbenchService
var _prev_cell: Vector3i

func _initialize():
	_player = game.player
	assert(_player)
	_workbench = WorkbenchService.get_singleton()
	MetSys.room_changed.connect(_on_room_changed, CONNECT_DEFERRED)
	MetSys.cell_changed.connect(_on_cell_changed)

func _on_cell_changed(new_cell: Vector3i):
	_prev_cell = new_cell

func _on_room_changed(target_room: String):
	if target_room == MetSys.get_current_room_id():
		return
	
	match _get_transition_style():
		AreaDef.TransitionStyle.SCROLL:
			await _transition_scroll(target_room)
		AreaDef.TransitionStyle.SNAP:
			await _transition_snap(target_room)
		_:
			await _transition_immediate(target_room)

func _get_transition_style() -> int:
	if _workbench == null:
		return AreaDef.TransitionStyle.IMMEDIATE
	return int(_workbench.get_workplace_data(&"current_transition_style", AreaDef.TransitionStyle.IMMEDIATE))

func _transition_immediate(target_room: String) -> void:
	var prev_room_instance := MetSys.get_current_room_instance()
	if prev_room_instance:
		prev_room_instance.get_parent().remove_child(prev_room_instance)
	await game.load_room(target_room)
	if prev_room_instance:
		_player.position -= MetSys.get_current_room_instance().get_room_position_offset(prev_room_instance)
		prev_room_instance.queue_free()

func _transition_snap(target_room: String) -> void:
	await _transition_immediate(target_room)

func _transition_scroll(target_room: String) -> void:
	var camera: Camera2D = _player.get_viewport().get_camera_2d()
	var prev_room_instance := MetSys.get_current_room_instance()
	if prev_room_instance:
		prev_room_instance.get_parent().remove_child(prev_room_instance)
	var prev_map := game.map
	game.map = null
	await game.load_room(target_room)
	if not prev_room_instance:
		return
	var offset := MetSys.get_current_room_instance().get_room_position_offset(prev_room_instance)
	var screen_delta := _player.get_global_transform_with_canvas().origin
	_player.position -= offset
	prev_room_instance.queue_free()
	if not is_instance_valid(prev_map):
		return
	if camera == null:
		prev_map.queue_free()
		return
	game.get_tree().paused = true
	await game.get_tree().process_frame
	screen_delta = _player.get_global_transform_with_canvas().origin - screen_delta
	prev_map.position -= offset
	var tween := game.create_tween().set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)
	tween.tween_property(camera, ^"offset", screen_delta, 0)
	if is_zero_approx(screen_delta.x) or is_zero_approx(screen_delta.y):
		tween.tween_property(camera, ^"offset", Vector2(), scroll_time)
	else:
		var total := 1.0 / (absf(screen_delta.x) + absf(screen_delta.y))
		tween.tween_property(camera, ^"offset:x", 0.0, absf(screen_delta.x) * total * scroll_time)
		tween.tween_property(camera, ^"offset:y", 0.0, absf(screen_delta.y) * total * scroll_time)
	await tween.finished
	prev_map.queue_free()
	game.get_tree().paused = false
