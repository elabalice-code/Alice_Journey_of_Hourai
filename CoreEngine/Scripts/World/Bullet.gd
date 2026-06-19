extends Area2D
class_name Bullet

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const CombatFactionTypesScript = preload("res://CoreEngine/Scripts/Contract/CombatFactionTypes.gd")

@export var speed: float = 380.0
@export var lifetime_seconds: float = 4.0
@export var auto_scale_sprite: bool = true
@export var target_size_world: float = 14.0
@export var damage: float = 10.0
@export var source_faction: StringName = CombatFactionTypesScript.ENEMY

var velocity: Vector2 = Vector2.ZERO
var _life_left: float = 0.0

@onready var _sprite: Sprite2D = $Sprite2D

func _ready() -> void:
	_life_left = lifetime_seconds
	monitoring = true
	monitorable = true
	body_entered.connect(_on_body_entered)
	area_entered.connect(_on_area_entered)
	if auto_scale_sprite and is_instance_valid(_sprite) and _sprite.texture != null and target_size_world > 0.0:
		var tex_size := _sprite.texture.get_size()
		var longest := maxf(tex_size.x, tex_size.y)
		if longest > 0.0:
			var s := target_size_world / longest
			_sprite.scale = Vector2(s, s)
	_check_overlaps()

func _physics_process(delta: float) -> void:
	_life_left -= delta
	if _life_left <= 0.0:
		queue_free()
		return
	position += velocity * delta
	_check_overlaps()

func fire(direction: Vector2) -> void:
	if direction.length_squared() <= 0.0001:
		velocity = Vector2.RIGHT * speed
	else:
		velocity = direction.normalized() * speed

func _on_body_entered(body: Node) -> void:
	if body == null:
		return
	var target_group := CombatFactionTypesScript.target_group_for_source(source_faction)
	if target_group != &"" and not body.is_in_group(target_group):
		return
	var dealt := _try_apply_damage(body)
	if dealt > 0.0:
		queue_free()

func _on_area_entered(area: Area2D) -> void:
	if area == null:
		return
	var parent := area.get_parent()
	if parent != null:
		_on_body_entered(parent)

func _try_apply_damage(target: Node) -> float:
	var attacker_dir := Vector2.ZERO
	if velocity.length_squared() > 0.0001:
		attacker_dir = -velocity.normalized()
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		if _can_receive_damage(target):
			workbench.send({
				"type": MessageTypes.TYPE_APPLY_DAMAGE_REQUEST,
				"target": target,
				"amount": damage,
				"attacker_dir": attacker_dir,
				"source": self,
				"source_faction": source_faction,
			})
			return 1.0
	var c := _get_combatant(target)
	if c != null:
		return c.apply_hit(damage, attacker_dir)
	return 0.0

func _can_receive_damage(target: Node) -> bool:
	return _get_combatant(target) != null

func _get_combatant(target: Node) -> Combatant:
	if target == null or not is_instance_valid(target):
		return null
	return target.get_node_or_null(^"Combatant") as Combatant

func _check_overlaps() -> void:
	if not monitoring:
		return
	var bodies := get_overlapping_bodies()
	for b in bodies:
		_on_body_entered(b)
		if not is_inside_tree():
			return
