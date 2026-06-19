extends RefCounted
class_name CombatantPort

const DamageFormulaScript = preload("res://CoreEngine/Scripts/Helper/Combat/DamageFormula.gd")
const EquipmentShapeScript = preload("res://CoreEngine/Scripts/Helper/Inventory/EquipmentShape.gd")

var target: Node
var combatant: Combatant

static func from_target(p_target: Node) -> CombatantPort:
	var port := CombatantPort.new()
	port.target = p_target
	if p_target != null and is_instance_valid(p_target):
		port.combatant = p_target.get_node_or_null(^"Combatant") as Combatant
	return port

func is_valid() -> bool:
	return target != null and is_instance_valid(target) and combatant != null and is_instance_valid(combatant)

func apply_damage(raw_damage: float, attacker_dir: Vector2) -> float:
	if not is_valid():
		return 0.0
	var raw := float(raw_damage)
	if raw <= 0.0:
		return 0.0
	var actual := DamageFormulaScript.after_armor(raw, armor_against(attacker_dir))
	return combatant.apply_damage_from_actor(actual)

func armor_against(attacker_dir: Vector2 = Vector2.ZERO) -> float:
	if not is_valid():
		return 0.0
	return combatant.get_armor_against(attacker_dir)

func total_armor() -> float:
	if not is_valid():
		return 0.0
	return combatant.get_total_armor()

func block_armor() -> float:
	if not is_valid():
		return 0.0
	return combatant.get_block_armor()

func sync_equipment(equipped: Dictionary) -> void:
	if not is_valid():
		return
	for slot in EquipmentShapeScript.slots():
		combatant.unequip(slot)
	for slot in EquipmentShapeScript.slots():
		var item := equipped.get(slot) as ItemDef
		if item == null:
			continue
		combatant.equip(slot, EquipmentShapeScript.to_equipment_dict(item))

func hp() -> float:
	if is_valid():
		return combatant.hp
	return 0.0

func max_hp() -> float:
	if is_valid():
		return combatant.max_hp
	return 0.0

func snapshot() -> Dictionary:
	return {
		"hp": hp(),
		"max_hp": max_hp(),
		"armor": total_armor()
	}
