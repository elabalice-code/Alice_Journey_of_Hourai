extends RefCounted
class_name FireAngleRule

var threshold_seconds: float = 0.2
const COT_15_NUM: float = 3.732
const COT_15_DEN: float = 1.0

var _now_seconds: float = 0.0

var _last_pressed: Dictionary = {
	&"left": -1.0,
	&"right": -1.0,
	&"up": -1.0,
	&"down": -1.0,
}

func record_pressed(dir: StringName, now_seconds: float) -> void:
	if _last_pressed.has(dir):
		_last_pressed[dir] = now_seconds

func sync_pressed(dir: StringName, pressed: bool, now_seconds: float) -> void:
	if not pressed:
		return
	if not _last_pressed.has(dir):
		return
	if float(_last_pressed[dir]) < 0.0:
		_last_pressed[dir] = now_seconds

func set_now(now_seconds: float) -> void:
	_now_seconds = now_seconds

func resolve_direction(x_dir: int, y_dir: int, facing_x_dir: int) -> Vector2:
	if x_dir == 0 and y_dir == 0:
		return Vector2(float(facing_x_dir), 0.0)
	if x_dir != 0 and y_dir == 0:
		return Vector2(float(x_dir), 0.0)
	if y_dir != 0 and x_dir == 0:
		return Vector2(0.0, float(y_dir))
	
	var t_x := _get_axis_time_x(x_dir)
	var t_y := _get_axis_time_y(y_dir)
	
	if absf(t_x - t_y) <= threshold_seconds and (_now_seconds - maxf(t_x, t_y)) <= threshold_seconds:
		return Vector2(float(x_dir), float(y_dir)).normalized()
	
	var x_first := t_x <= t_y
	if x_first:
		return Vector2(float(x_dir) * COT_15_NUM, float(y_dir) * COT_15_DEN).normalized()
	return Vector2(float(x_dir) * COT_15_DEN, float(y_dir) * COT_15_NUM).normalized()

func _get_axis_time_x(x_dir: int) -> float:
	if x_dir < 0:
		return float(_last_pressed[&"left"])
	if x_dir > 0:
		return float(_last_pressed[&"right"])
	return INF

func _get_axis_time_y(y_dir: int) -> float:
	if y_dir < 0:
		return float(_last_pressed[&"up"])
	if y_dir > 0:
		return float(_last_pressed[&"down"])
	return INF
