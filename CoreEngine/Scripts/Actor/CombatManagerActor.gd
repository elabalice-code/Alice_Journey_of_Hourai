extends RefCounted
class_name CombatManagerActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")
const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const CombatFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowProducers.gd")
const CombatFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowRouter.gd")
const DamageFormulaScript = preload("res://CoreEngine/Scripts/Helper/Combat/DamageFormula.gd")

var _workbench: WorkbenchService
var _last_state_by_target_path: Dictionary = {}

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			MessageTypes.TYPE_APPLY_DAMAGE_REQUEST,
			MessageTypes.TYPE_COMBAT_SYNC_REQUEST,
		], &"_on_workplace")
		if not _workbench.tick.is_connected(_on_tick):
			_workbench.tick.connect(_on_tick)
		_sync_player_combat_to_workplace()

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var frame: CombatFlowSignalFrame = CombatFlowProducersScript.from_workplace(workplace)
	var intent: CombatFlowIntent = CombatFlowRouterScript.route(frame)
	_execute_intent(intent)

func _execute_intent(intent: CombatFlowIntent) -> void:
	if intent == null or not intent.is_valid():
		return
	match intent.kind:
		CombatFlowIntent.KIND_APPLY_DAMAGE:
			apply_damage(
				intent.payload.get("target") as Node,
				float(intent.payload.get("amount", 0.0)),
				intent.payload.get("attacker_dir", Vector2.ZERO) as Vector2
			)
		CombatFlowIntent.KIND_SYNC:
			request_sync(intent.payload.get("target") as Node)

func _on_tick(_delta: float) -> void:
	_sync_player_combat_to_workplace()

func apply_damage(target: Node, amount: float, attacker_dir: Vector2 = Vector2.ZERO) -> float:
	if target == null or not is_instance_valid(target):
		return 0.0
	var dealt := _apply_damage_to_target_combatant(target, amount, attacker_dir)
	if dealt <= 0.0:
		return 0.0
	_sync_global_combat_data(target)
	if _workbench != null:
		var remaining := _get_target_hp(target)
		_workbench.send({
			"type": MessageTypes.TYPE_DAMAGE_APPLIED,
			"amount": dealt,
			"remaining_hp": remaining,
			"target": target,
		})
		_emit_combat_state(target, true)
		if remaining <= 0.0:
			_workbench.send({
				"type": MessageTypes.TYPE_BATTLE_RESULT_REQUEST,
				"text": "DEFEAT"
			})
	return dealt

func _apply_damage_to_target_combatant(target: Node, amount: float, attacker_dir: Vector2) -> float:
	var c := target.get_node_or_null(^"Combatant")
	if c != null and is_instance_valid(c):
		var raw := float(amount)
		if raw <= 0.0:
			return 0.0
		var armor := 0.0
		if c.has_method(&"get_total_armor"):
			armor = float(c.call(&"get_total_armor"))
		if c.has_variable(&"blocking") and bool(c.get("blocking")):
			if c.has_method(&"_is_front_attack") and bool(c.call(&"_is_front_attack", attacker_dir)):
				if c.has_method(&"get_block_armor"):
					armor += float(c.call(&"get_block_armor"))
		var actual := DamageFormulaScript.after_armor(raw, armor)
		if c.has_method(&"apply_damage_from_actor"):
			return float(c.call(&"apply_damage_from_actor", actual))
		if c.has_method(&"apply_hit"):
			return float(c.call(&"apply_hit", raw, attacker_dir))
		if c.has_method(&"apply_raw_damage"):
			return float(c.call(&"apply_raw_damage", raw))
	return 0.0

func _get_target_hp(target: Node) -> float:
	if target.has_variable(&"hp"):
		return float(target.get("hp"))
	var c := target.get_node_or_null(^"Combatant")
	if c != null and is_instance_valid(c) and c.has_variable(&"hp"):
		return float(c.get("hp"))
	return 0.0

func _sync_global_combat_data(target: Node) -> void:
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	if not target.is_in_group(&"player"):
		return
	var c := target.get_node_or_null(^"Combatant")
	if c == null or not is_instance_valid(c):
		return
	var data: ActorFramework.CombatData = global_wp.combat
	data.hp = float(c.get("hp"))
	data.max_hp = float(c.get("max_hp"))
	if c.has_method(&"get_total_armor"):
		data.defense = float(c.call(&"get_total_armor"))

func _sync_player_combat_to_workplace() -> void:
	if _workbench == null:
		return
	var player := _workbench.get_service(&"player") as Node
	if not is_instance_valid(player):
		return
	_sync_global_combat_data(player)
	_emit_combat_state(player, false)

func request_sync(target: Node) -> void:
	if target == null or not is_instance_valid(target):
		return
	_sync_global_combat_data(target)
	_emit_combat_state(target, true)

func _emit_combat_state(target: Node, force: bool) -> void:
	if _workbench == null or target == null or not is_instance_valid(target):
		return
	var c := target.get_node_or_null(^"Combatant")
	if c == null or not is_instance_valid(c):
		return
	var hp := float(c.get("hp"))
	var max_hp := float(c.get("max_hp"))
	var armor := 0.0
	if c.has_method(&"get_total_armor"):
		armor = float(c.call(&"get_total_armor"))
	var key := str(target.get_path())
	var last: Dictionary = _last_state_by_target_path.get(key, {}) as Dictionary
	var snapshot := {
		"hp": hp,
		"max_hp": max_hp,
		"armor": armor
	}
	if not force and last == snapshot:
		return
	_last_state_by_target_path[key] = snapshot
	_workbench.send({
		"type": MessageTypes.TYPE_COMBAT_STATE_CHANGED,
		"target_path": key,
		"hp": hp,
		"max_hp": max_hp,
		"armor": armor,
	})
