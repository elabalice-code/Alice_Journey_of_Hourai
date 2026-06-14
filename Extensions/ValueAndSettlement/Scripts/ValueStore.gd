extends RefCounted
class_name ValueStore

signal value_changed(key: StringName, value: float, delta: float, context: Dictionary)

var _values: Dictionary = {}

func get_value(key: StringName, default_value := 0.0) -> float:
	return float(_values.get(key, default_value))

func set_value(key: StringName, value: float, context: Dictionary = {}) -> void:
	var prev := get_value(key)
	if prev == value:
		return
	_values[key] = value
	value_changed.emit(key, value, value - prev, context)

func apply_delta(key: StringName, delta: float, context: Dictionary = {}) -> float:
	var value := get_value(key) + delta
	set_value(key, value, context)
	return get_value(key)

func to_dict() -> Dictionary:
	return _values.duplicate(true)

func from_dict(data: Dictionary) -> void:
	_values = data.duplicate(true)
