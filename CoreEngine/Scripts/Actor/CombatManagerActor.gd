extends RefCounted
class_name CombatManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const CombatFlowProducersScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowProducers.gd")
const CombatFlowRouterScript = preload("res://CoreEngine/Scripts/Signal/CombatFlow/CombatFlowRouter.gd")
const CombatStateSnapshotScript = preload("res://CoreEngine/Scripts/Helper/Combat/CombatStateSnapshot.gd")
const CombatantPortScript = preload("res://CoreEngine/Scripts/Actor/CombatantPort.gd")

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
	var port: CombatantPort = CombatantPortScript.from_target(target)
	return port.apply_damage(amount, attacker_dir)

func _get_target_hp(target: Node) -> float:
	var port: CombatantPort = CombatantPortScript.from_target(target)
	return port.hp()

func _sync_global_combat_data(target: Node) -> void:
	if _workbench == null:
		return
	var global_wp := _workbench.get_workplace()
	if global_wp == null:
		return
	if not target.is_in_group(&"player"):
		return
	var port: CombatantPort = CombatantPortScript.from_target(target)
	if not port.is_valid():
		return
	var data: CombatData = global_wp.combat
	data.hp = port.hp()
	data.max_hp = port.max_hp()
	data.defense = port.total_armor()

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
	var port: CombatantPort = CombatantPortScript.from_target(target)
	if not port.is_valid():
		return
	var state := port.snapshot()
	var hp := float(state.get("hp", 0.0))
	var max_hp := float(state.get("max_hp", 0.0))
	var armor := float(state.get("armor", 0.0))
	var key := str(target.get_path())
	var last: Dictionary = _last_state_by_target_path.get(key, {}) as Dictionary
	var snapshot := CombatStateSnapshotScript.make(hp, max_hp, armor)
	if not CombatStateSnapshotScript.should_emit(last, snapshot, force):
		return
	_last_state_by_target_path[key] = snapshot
	_workbench.send({
		"type": MessageTypes.TYPE_COMBAT_STATE_CHANGED,
		"target_path": key,
		"hp": hp,
		"max_hp": max_hp,
		"armor": armor,
	})
