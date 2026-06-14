extends Node

signal health_changed(current_hp: float, max_hp: float)
signal equipment_changed(total_armor: float)
signal died()

@export var max_hp: float = 100.0
@export var hp: float = 100.0
@export var base_armor: float = 0.0

var equipment: Dictionary = {}
var blocking: bool = false

func _ready() -> void:
	hp = clampf(hp, 0.0, max_hp)

func get_total_armor() -> float:
	var total := base_armor
	for slot in equipment.keys():
		if StringName(slot) == &"shield":
			continue
		var item: Variant = equipment.get(slot)
		if item is Dictionary:
			total += float(item.get("armor", 0.0))
	return maxf(0.0, total)

func get_block_armor() -> float:
	var item: Variant = equipment.get(&"shield")
	if item is Dictionary:
		var d := item as Dictionary
		if d.has("block_armor"):
			return float(d.get("block_armor", 0.0))
		return float(d.get("armor", 0.0))
	return 0.0

func set_blocking(value: bool) -> void:
	blocking = value

func equip(slot: StringName, item: Dictionary) -> void:
	equipment[slot] = item
	equipment_changed.emit(get_total_armor())

func unequip(slot: StringName) -> void:
	if equipment.has(slot):
		equipment.erase(slot)
		equipment_changed.emit(get_total_armor())

func get_equipped(slot: StringName) -> Dictionary:
	var item: Variant = equipment.get(slot)
	if item is Dictionary:
		return item as Dictionary
	return {}

func apply_raw_damage(raw_damage: float) -> float:
	return apply_hit(float(raw_damage), Vector2.ZERO)

func apply_hit(raw_damage: float, attacker_dir: Vector2) -> float:
	var armor := get_total_armor()
	if blocking and _is_front_attack(attacker_dir):
		armor += get_block_armor()
	var actual := float(raw_damage) * 100.0 / (100.0 + armor)
	apply_damage_from_actor(actual)
	return actual

func heal(value: float) -> void:
	hp = minf(max_hp, hp + float(value))
	health_changed.emit(hp, max_hp)

func apply_damage_from_actor(amount: float) -> float:
	var actual := maxf(0.0, float(amount))
	if actual <= 0.0:
		return 0.0
	hp = maxf(0.0, hp - actual)
	health_changed.emit(hp, max_hp)
	if hp <= 0.0:
		died.emit()
	return actual

func _is_front_attack(attacker_dir: Vector2) -> bool:
	if attacker_dir.length_squared() <= 0.0001:
		return false
	var facing := Vector2.RIGHT
	var owner := get_parent()
	if owner != null and owner.has_method("get_facing_dir"):
		facing = owner.call("get_facing_dir") as Vector2
	if facing.length_squared() <= 0.0001:
		facing = Vector2.RIGHT
	return attacker_dir.normalized().dot(facing.normalized()) > 0.1
