# This script is based on the default CharacterBody2D template. Not much interesting happening here.
extends CharacterBody2D

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")
const BulletScene = preload("res://CoreEngine/Objects/Bullet.tscn")
const FireAngleRule = preload("res://CoreEngine/Scripts/Input/FireAngleRule.gd")

const SPEED_MIN = 300.0
const SPEED_MAX = 400.0
const ACCEL = 50.0
const JUMP_VELOCITY = -450.0
const MAX_FALL_SPEED = 900.0
const COYOTE_TIME: float = .1
const SHORT_HOP: float = .5

var gravity: int = ProjectSettings.get_setting("physics/2d/default_gravity")
var animation: String

var reset_position: Vector2
# Indicates that the player has an event happening and can't be controlled.
var event: bool
var IsTransferred: bool = false

var abilities: Array[StringName]
var double_jump: bool
var prev_on_floor: bool
var airtime: float = 0
var speed: float = SPEED_MIN
var speed_min_current: float = SPEED_MIN
var speed_max_current: float = SPEED_MAX
var jump_velocity_current: float = JUMP_VELOCITY
var _shoot_cooldown_left: float = 0.0
var _defending: bool = false
var _input_mode: StringName = &"side_scrolling"

@export var shoot_cooldown_seconds: float = 0.25
@export var bullet_speed: float = 520.0
@export var bullet_spawn_offset: Vector2 = Vector2.ZERO
@export var aim_simultaneous_threshold_seconds: float = 0.2
@export var top_down_speed: float = 320.0

@onready var combatant: Node = $Combatant
@onready var _sprite: Sprite2D = $Sprite2D
@onready var _collision_shape: CollisionShape2D = $CollisionShape2D

var _fire_angle_rule: FireAngleRule = FireAngleRule.new()
var _workbench: WorkbenchService

func _ensure_action_key(action: StringName, physical_keycode: Key) -> void:
	if not InputMap.has_action(action):
		InputMap.add_action(action)
	else:
		InputMap.action_erase_events(action)
	
	var ev := InputEventKey.new()
	ev.physical_keycode = physical_keycode
	InputMap.action_add_event(action, ev)

func _ensure_action_keys(action: StringName, physical_keycodes: Array[Key]) -> void:
	if not InputMap.has_action(action):
		InputMap.add_action(action)
	else:
		InputMap.action_erase_events(action)
	for k in physical_keycodes:
		var ev := InputEventKey.new()
		ev.physical_keycode = k
		InputMap.action_add_event(action, ev)

func _setup_input_map() -> void:
	_ensure_action_key(&"move_left", KEY_A)
	_ensure_action_key(&"move_right", KEY_D)
	_ensure_action_key(&"move_down", KEY_S)
	_ensure_action_key(&"move_up", KEY_W)
	_ensure_action_keys(&"jump", [KEY_SPACE, KEY_K])
	_ensure_action_key(&"attack", KEY_J)
	_ensure_action_key(&"elevator_up", KEY_W)
	_ensure_action_key(&"aim_up", KEY_W)
	_ensure_action_key(&"shoot_left", KEY_LEFT)
	_ensure_action_key(&"shoot_right", KEY_RIGHT)
	_ensure_action_key(&"shoot_up", KEY_UP)
	_ensure_action_key(&"shoot_down", KEY_DOWN)
	_ensure_action_key(&"toggle_bag", KEY_X)
	_ensure_action_key(&"toggle_equipment", KEY_Z)

func _ready() -> void:
	_setup_input_map()
	_fire_angle_rule.threshold_seconds = aim_simultaneous_threshold_seconds
	_bind_workbench()
	_setup_combatant()
	on_enter()

func _bind_workbench() -> void:
	_workbench = WorkbenchService.get_singleton()
	if _workbench == null:
		return
	_workbench.register_actor(self, [
		ActorFramework.TYPE_PLAYER_DATA_CHANGED,
		ActorFramework.TYPE_INPUT_MODE_CHANGED,
	], &"_on_workplace")
	var global_wp = _workbench.get_workplace()
	if global_wp:
		_apply_player_data(global_wp.player)

func _exit_tree() -> void:
	if _workbench != null:
		_workbench.unregister_actor(self)

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	match workplace.type:
		ActorFramework.TYPE_PLAYER_DATA_CHANGED:
			var message: Dictionary = workplace.payload
			speed_min_current = float(message.get("speed_min", SPEED_MIN))
			speed_max_current = float(message.get("speed_max", SPEED_MAX))
			jump_velocity_current = float(message.get("jump_velocity", JUMP_VELOCITY))
			speed = clampf(speed, speed_min_current, speed_max_current)
		ActorFramework.TYPE_INPUT_MODE_CHANGED:
			var message2: Dictionary = workplace.payload
			var new_mode := message2.get("mode", &"") as StringName
			if new_mode != &"":
				_input_mode = new_mode
				_defending = false
				if is_instance_valid(combatant) and combatant.has_method("set_blocking"):
					combatant.call("set_blocking", false)

func _apply_player_data(data: ActorFramework.PlayerData) -> void:
	if data == null:
		return
	speed_min_current = data.speed_min
	speed_max_current = data.speed_max
	jump_velocity_current = data.jump_velocity
	speed = clampf(speed, speed_min_current, speed_max_current)

func _physics_process(delta: float) -> void:
	if event:
		return
	_shoot_cooldown_left = maxf(0.0, _shoot_cooldown_left - delta)
	_update_fire_angle_rule()
	if _input_mode != &"top_down" and _input_mode != &"top_down_shooter":
		_update_defense_state()
	
	if _input_mode == &"top_down" or _input_mode == &"top_down_shooter":
		_physics_process_top_down(delta)
		if _input_mode == &"top_down_shooter" and not _defending:
			var want_shoot := Input.is_action_pressed("attack")
			if Input.is_action_pressed("shoot_left") or Input.is_action_pressed("shoot_right") or Input.is_action_pressed("shoot_up") or Input.is_action_pressed("shoot_down"):
				want_shoot = true
			if want_shoot:
				_try_fire_bullet()
		return
	
	if not is_on_floor():
		velocity.y = min(velocity.y + gravity * delta, MAX_FALL_SPEED)
		airtime += delta
	elif not prev_on_floor and &"double_jump" in abilities:
		# Some simple double jump implementation.
		double_jump = true
		airtime = 0
	
	var on_floor_ct: bool = is_on_floor() or airtime < COYOTE_TIME
	if Input.is_action_just_pressed("jump") and (on_floor_ct or double_jump):
		if not on_floor_ct:
			double_jump = false
		
		if Input.is_action_pressed("move_down"):
			position.y += 8
		else:
			velocity.y = jump_velocity_current
	
	if Input.is_action_just_released("jump"):
		if not is_on_floor() and velocity.y < 0:
			velocity.y = min(0, velocity.y - JUMP_VELOCITY * SHORT_HOP)
			
	
	if is_on_wall():
		speed = speed_min_current
	
	var direction := Input.get_axis("move_left", "move_right")
	if Input.is_action_pressed("aim_up") or Input.is_action_pressed("move_down"):
		direction = 0.0
	if direction:
		speed = min(speed_max_current, speed + ACCEL * delta)
		velocity.x = direction * speed
	else:
		velocity.x = move_toward(velocity.x, 0, speed_min_current)
		speed = speed_min_current

	if Input.is_action_just_pressed("attack"):
		if not _defending:
			_try_fire_bullet()
	
	prev_on_floor = is_on_floor()
	move_and_slide()
	
	var new_animation = &"Idle"
	if _defending:
		new_animation = &"Idle"
	elif velocity.y < 0:
		new_animation = &"Jump"
	elif velocity.y >= 0 and not is_on_floor():
		new_animation = &"Fall"
	elif absf(velocity.x) > 1:
		new_animation = &"Run"
	
	if new_animation != animation:
		animation = new_animation
		$AnimationPlayer.play(new_animation)
	
	if velocity.x > 1:
		$Sprite2D.flip_h = false
	elif velocity.x < -1:
		$Sprite2D.flip_h = true

func _physics_process_top_down(delta: float) -> void:
	var dir := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if dir.length_squared() > 1.0:
		dir = dir.normalized()
	velocity = dir * top_down_speed
	move_and_slide()
	
	var new_animation = &"Idle"
	if velocity.length_squared() > 1.0:
		new_animation = &"Run"
	if new_animation != animation:
		animation = new_animation
		$AnimationPlayer.play(new_animation)
	
	if velocity.x > 1:
		$Sprite2D.flip_h = false
	elif velocity.x < -1:
		$Sprite2D.flip_h = true

func kill():
	IsTransferred = true
	set_meta(&"IsTransferred", true)
	position = reset_position
	var workbench := _workbench
	if workbench == null:
		workbench = WorkbenchService.get_singleton()
	if workbench == null:
		return
	workbench.send({
		"type": ActorFramework.TYPE_LOAD_ROOM_REQUEST,
		"target_map": MetSys.get_current_room_id()
	})

func _input(p_event: InputEvent) -> void:
	if not IsTransferred:
		return
	if p_event is InputEventKey and p_event.pressed:
		IsTransferred = false
		set_meta(&"IsTransferred", false)
	elif p_event is InputEventMouseButton and p_event.pressed:
		IsTransferred = false
		set_meta(&"IsTransferred", false)
	elif p_event is InputEventJoypadButton and p_event.pressed:
		IsTransferred = false
		set_meta(&"IsTransferred", false)

func on_enter():
	# Position for kill system. Assigned when entering new room (see Game.gd).
	reset_position = position

func _setup_combatant() -> void:
	if not is_instance_valid(combatant):
		return
	if combatant.has_signal("died"):
		var cb := Callable(self, &"_on_player_died")
		if not combatant.died.is_connected(cb):
			combatant.died.connect(cb)
	if combatant.has_method("equip"):
		combatant.equip(&"armor", {"id": &"starter_armor", "name": "新手护甲", "armor": 10.0})
		combatant.equip(&"shield", {"id": &"starter_shield", "name": "新手盾牌", "block_armor": 100.0})

func _toggle_starter_armor() -> void:
	if not is_instance_valid(combatant):
		return
	if not combatant.has_method("get_equipped") or not combatant.has_method("equip") or not combatant.has_method("unequip"):
		return
	var current: Dictionary = combatant.get_equipped(&"armor") as Dictionary
	if StringName(current.get("id", &"")) == &"starter_armor":
		combatant.unequip(&"armor")
	else:
		combatant.equip(&"armor", {"id": &"starter_armor", "name": "新手护甲", "armor": 10.0})

func _on_player_died() -> void:
	call_deferred(&"_handle_player_died")

func _handle_player_died() -> void:
	var workbench := _workbench
	if workbench == null:
		workbench = WorkbenchService.get_singleton()
	if workbench != null:
		workbench.send({
			"type": ActorFramework.TYPE_BATTLE_RESULT_REQUEST,
			"text": "失败"
		})
	await get_tree().create_timer(0.6).timeout
	kill()

func apply_raw_damage(raw_damage: float) -> float:
	if is_instance_valid(combatant) and combatant.has_method("apply_raw_damage"):
		return float(combatant.apply_raw_damage(raw_damage))
	return float(raw_damage)

func apply_hit(raw_damage: float, attacker_dir: Vector2) -> float:
	if is_instance_valid(combatant) and combatant.has_method("apply_hit"):
		return float(combatant.apply_hit(raw_damage, attacker_dir))
	return apply_raw_damage(raw_damage)

func _try_fire_bullet() -> void:
	if _defending:
		return
	if _shoot_cooldown_left > 0.0:
		return
	_shoot_cooldown_left = maxf(0.05, shoot_cooldown_seconds)
	_fire_bullet()

func _fire_bullet() -> void:
	var b := BulletScene.instantiate() as Node2D
	if b == null:
		return
	var parent := get_parent()
	if parent == null:
		return
	parent.add_child(b)
	var center := global_position
	if is_instance_valid(_collision_shape):
		center = _collision_shape.global_position
	b.global_position = center + bullet_spawn_offset
	b.set("source_faction", &"player")
	b.set("damage", 10.0)
	b.set("speed", bullet_speed)
	var dir := _get_fire_direction()
	if b.has_method("fire"):
		b.call("fire", dir)

func _update_fire_angle_rule() -> void:
	var up_action := "aim_up"
	var left_action := "move_left"
	var right_action := "move_right"
	var down_action := "move_down"
	if _input_mode == &"top_down":
		up_action = "move_up"
	if _input_mode == &"top_down_shooter":
		up_action = "shoot_up"
		left_action = "shoot_left"
		right_action = "shoot_right"
		down_action = "shoot_down"
	var now := float(Time.get_ticks_msec()) / 1000.0
	_fire_angle_rule.set_now(now)
	if Input.is_action_just_pressed(left_action):
		_fire_angle_rule.record_pressed(&"left", now)
	if Input.is_action_just_pressed(right_action):
		_fire_angle_rule.record_pressed(&"right", now)
	if Input.is_action_just_pressed(up_action):
		_fire_angle_rule.record_pressed(&"up", now)
	if Input.is_action_just_pressed(down_action):
		_fire_angle_rule.record_pressed(&"down", now)
	
	_fire_angle_rule.sync_pressed(&"left", Input.is_action_pressed(left_action), now)
	_fire_angle_rule.sync_pressed(&"right", Input.is_action_pressed(right_action), now)
	_fire_angle_rule.sync_pressed(&"up", Input.is_action_pressed(up_action), now)
	_fire_angle_rule.sync_pressed(&"down", Input.is_action_pressed(down_action), now)

func _get_fire_direction() -> Vector2:
	var up_action := "aim_up"
	var left_action := "move_left"
	var right_action := "move_right"
	var down_action := "move_down"
	if _input_mode == &"top_down":
		up_action = "move_up"
	if _input_mode == &"top_down_shooter":
		up_action = "shoot_up"
		left_action = "shoot_left"
		right_action = "shoot_right"
		down_action = "shoot_down"
	var x_dir := int(signf(Input.get_axis(left_action, right_action)))
	var y_dir := 0
	if Input.is_action_pressed(up_action):
		y_dir = -1
	else:
		if Input.is_action_pressed(down_action):
			y_dir = 1
	var facing := 1
	if is_instance_valid(_sprite) and _sprite.flip_h:
		facing = -1
	return _fire_angle_rule.resolve_direction(x_dir, y_dir, facing)

func _update_defense_state() -> void:
	if _input_mode == &"top_down" or _input_mode == &"top_down_shooter":
		if _defending:
			_defending = false
			if is_instance_valid(combatant) and combatant.has_method("set_blocking"):
				combatant.call("set_blocking", false)
		return
	var locked := Input.is_action_pressed("aim_up") or Input.is_action_pressed("move_down")
	var facing := 1
	if is_instance_valid(_sprite) and _sprite.flip_h:
		facing = -1
	var reverse_pressed := false
	if facing > 0:
		reverse_pressed = Input.is_action_pressed("move_left") and not Input.is_action_pressed("move_right")
	else:
		reverse_pressed = Input.is_action_pressed("move_right") and not Input.is_action_pressed("move_left")
	var want_defense := locked and reverse_pressed
	if want_defense == _defending:
		return
	_defending = want_defense
	if is_instance_valid(combatant) and combatant.has_method("set_blocking"):
		combatant.call("set_blocking", _defending)

func get_facing_dir() -> Vector2:
	if is_instance_valid(_sprite) and _sprite.flip_h:
		return Vector2.LEFT
	return Vector2.RIGHT
