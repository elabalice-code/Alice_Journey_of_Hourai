extends RefCounted
class_name PlayerData

var base_speed_min: float = 300.0
var base_speed_max: float = 400.0
var base_jump_velocity: float = -450.0

var speed_min: float = base_speed_min
var speed_max: float = base_speed_max
var jump_velocity: float = base_jump_velocity

func reset_to_base() -> void:
	speed_min = base_speed_min
	speed_max = base_speed_max
	jump_velocity = base_jump_velocity

func apply_multiplier(multiplier: float) -> void:
	speed_min = base_speed_min * multiplier
	speed_max = base_speed_max * multiplier
	jump_velocity = base_jump_velocity * multiplier
