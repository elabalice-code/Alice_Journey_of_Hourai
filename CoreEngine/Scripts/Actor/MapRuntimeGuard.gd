extends RefCounted
class_name MapRuntimeGuard

const MapRuntimeSurfaceScript = preload("res://CoreEngine/Scripts/Actor/MapRuntimeSurface.gd")

var _entry_player_global_pos: Vector2 = Vector2.ZERO
var _entry_set: bool = false
var _fall_out_cooldown_s: float = 0.0

func reset_entry(player: Node2D, cooldown_s: float = 0.75) -> void:
	_entry_player_global_pos = player.global_position if is_instance_valid(player) else Vector2.ZERO
	_entry_set = is_instance_valid(player)
	_fall_out_cooldown_s = cooldown_s

func tick(delta: float, map_root: Node2D, player: Node2D, map_changing: bool) -> bool:
	if map_root == null or map_changing:
		return false
	if not _entry_set:
		return false
	if not is_instance_valid(player):
		return false
	if player.process_mode == Node.PROCESS_MODE_DISABLED:
		return false

	_fall_out_cooldown_s = maxf(0.0, _fall_out_cooldown_s - delta)
	if _fall_out_cooldown_s > 0.0:
		return false

	var bounds := MapRuntimeSurfaceScript.map_world_bounds_rect(map_root)
	if bounds.size == Vector2.ZERO:
		return false

	var margin := 256.0
	var padded := bounds.grow(margin)
	var player_pos := map_root.to_local(player.global_position)
	if padded.has_point(player_pos):
		return false

	player.global_position = _entry_player_global_pos
	player.set_meta(&"IsTransferred", true)
	player.set(&"IsTransferred", true)
	_fall_out_cooldown_s = 0.75
	return true
