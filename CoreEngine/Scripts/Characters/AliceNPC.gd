extends CharacterBody2D
class_name AliceNPC

const BulletScene = preload("res://CoreEngine/Objects/Bullet.tscn")
const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const CombatFactionTypesScript = preload("res://CoreEngine/Scripts/Contract/CombatFactionTypes.gd")
const DialogueActionTypesScript = preload("res://CoreEngine/Scripts/Contract/DialogueActionTypes.gd")

@export var auto_tune_to_room: bool = true
@export var auto_scale_sprite: bool = true
@export var target_frame_height_world: float = 96.0
@export var auto_resize_collider: bool = true
@export var collider_height_ratio: float = 0.65
@export var collider_width_ratio: float = 0.28
@export var auto_detect_feet: bool = true
@export var feet_from_bottom_px: float = 0.0
@export var speed: float = 80.0
@export var speed_min: float = 70.0
@export var speed_max: float = 180.0
@export var spawn_range_x: float = 400.0
@export var spawn_ray_up: float = 300.0
@export var spawn_ray_down: float = 1200.0
@export var change_dir_seconds_min: float = 0.8
@export var change_dir_seconds_max: float = 2.0
@export var shoot_enabled: bool = true
@export var shoot_interval_seconds: float = 1.0
@export var bullet_speed: float = 520.0
@export var bullet_spawn_offset: Vector2 = Vector2(0.0, -28.0)

@export var is_enemy: bool = false
@export var random_spawn_enabled: bool = true
@export var speaker_name: String = "爱丽丝"
@export var story_dialogue_text: String = "下午好，今天天气也很不错。"
@export var npc_dialogue_text: String = "请问您需要买什么呢？"
@export var interaction_dist: float = 64.0
var _workbench: WorkbenchService
var _player: Node2D
var _in_interaction_range: bool = false


var _rng := RandomNumberGenerator.new()
var _dir: int = 1
var _change_dir_in: float = 0.0
var _gravity: float = float(ProjectSettings.get_setting("physics/2d/default_gravity"))
var _spawn_bounds: Rect2
var _has_bounds: bool = false
var _bounds_margin_x: float = 0.0
static var _cached_feet_pad_px: float = -1.0
var _shoot_in: float = 0.0

@onready var _anim: AnimationPlayer = $AnimationPlayer
@onready var _sprite: Sprite2D = $Sprite2D
@onready var _collider: CollisionShape2D = $CollisionShape2D
@onready var combatant: Combatant = $Combatant

signal defeated(npc: Node)

func _ready() -> void:
	_player = get_tree().get_first_node_in_group(&"player") as Node2D
	_workbench = WorkbenchService.get_singleton()
	_rng.randomize()
	_auto_tune()
	_apply_visual_tuning()
	set_enemy_mode(is_enemy)
	if random_spawn_enabled:
		_random_spawn()
	if is_enemy:
		_pick_direction()
		_play_walk_animation()
		_reset_shoot_timer()
	else:
		_set_idle_facing(_dir)
	_setup_combatant()

func set_enemy_mode(enemy_mode: bool) -> void:
	is_enemy = enemy_mode
	var status_bar := get_node_or_null(^"OverheadStatusBar") as CanvasItem
	if is_enemy:
		if not is_in_group(CombatFactionTypesScript.ENEMY):
			add_to_group(CombatFactionTypesScript.ENEMY)
		shoot_enabled = true
		_anim.play("Idle")
	else:
		if is_in_group(CombatFactionTypesScript.ENEMY):
			remove_from_group(CombatFactionTypesScript.ENEMY)
		shoot_enabled = false
		speed = 0.0
		velocity = Vector2.ZERO
		_anim.play("Idle")
	if status_bar != null:
		status_bar.visible = is_enemy
	_sync_combatant_facing()


func get_facing_dir() -> Vector2:
	return Vector2(float(_dir), 0.0)

func _physics_process(delta: float) -> void:
	if not is_enemy:
		_handle_interaction()
		velocity.x = 0.0
		velocity.y = minf(velocity.y + _gravity * delta, 2000.0)
		move_and_slide()
		return

	_handle_shooting(delta)
	
	_change_dir_in -= delta
	if _change_dir_in <= 0.0:
		_pick_direction()
	
	velocity.x = float(_dir) * speed
	velocity.y = minf(velocity.y + _gravity * delta, 2000.0)
	move_and_slide()
	
	if is_on_wall():
		_dir *= -1
		_sync_combatant_facing()
		_reset_change_timer()
	elif _has_bounds:
		var x := global_position.x
		var left := _spawn_bounds.position.x + _bounds_margin_x
		var right := _spawn_bounds.position.x + _spawn_bounds.size.x - _bounds_margin_x
		if x < left:
			global_position.x = left
			_dir = 1
			_sync_combatant_facing()
			_reset_change_timer()
		elif x > right:
			global_position.x = right
			_dir = -1
			_sync_combatant_facing()
			_reset_change_timer()
	
	_play_walk_animation()

func _handle_interaction() -> void:
	if not is_instance_valid(_player) or _workbench == null:
		return
		
	var dist = global_position.distance_to(_player.global_position)
	if dist < interaction_dist:
		if not _in_interaction_range:
			_in_interaction_range = true
			_send_dialogue_action({
				"action": DialogueActionTypesScript.SHOW_PROMPT,
				"text": "按 E 对话",
			})
		
		if Input.is_action_just_pressed("interact"):
			_face_player()
			_send_dialogue_action({
				"action": DialogueActionTypesScript.REQUEST_DIALOGUE,
				"dialogue_id": &"alice",
				"speaker": speaker_name,
				"story_text": story_dialogue_text,
				"npc_text": npc_dialogue_text,
			})
	else:
		if _in_interaction_range:
			_in_interaction_range = false
			_send_dialogue_action({
				"action": DialogueActionTypesScript.HIDE_PROMPT,
			})
			_send_dialogue_action({
				"action": DialogueActionTypesScript.END_DIALOGUE,
			})

func _send_dialogue_action(payload: Dictionary) -> void:
	if _workbench == null:
		return
	payload["type"] = MessageTypes.TYPE_DIALOGUE_ACTION_REQUEST
	payload["source"] = name
	_workbench.send(payload)

func _face_player() -> void:
	if not is_instance_valid(_player):
		return
	var dir_to_player := signf(_player.global_position.x - global_position.x)
	if dir_to_player == 0.0:
		return
	_dir = -1 if dir_to_player < 0.0 else 1
	_sync_combatant_facing()
	_set_idle_facing(_dir)

func _set_idle_facing(dir: int) -> void:
	if not is_instance_valid(_sprite):
		return
	_sprite.frame = 0 if dir < 0 else 13

func _handle_shooting(delta: float) -> void:
	if not shoot_enabled:
		return
	_shoot_in -= delta
	if _shoot_in > 0.0:
		return
	_reset_shoot_timer()
	_fire_bullet()

func _reset_shoot_timer() -> void:
	_shoot_in = maxf(0.05, shoot_interval_seconds)

func _fire_bullet() -> void:
	var b := BulletScene.instantiate() as Bullet
	if b == null:
		return
	
	var parent := get_parent()
	if parent == null:
		return
	parent.add_child(b)
	b.global_position = global_position + bullet_spawn_offset
	b.set("source_faction", CombatFactionTypesScript.ENEMY)
	b.set("damage", 10.0)
	
	var angle := _rng.randf_range(0.0, TAU)
	var dir := Vector2(cos(angle), sin(angle))
	
	b.set("speed", bullet_speed)
	b.fire(dir)

func _pick_direction() -> void:
	_dir = -1 if _rng.randf() < 0.5 else 1
	_sync_combatant_facing()
	_reset_change_timer()

func _reset_change_timer() -> void:
	_change_dir_in = _rng.randf_range(change_dir_seconds_min, change_dir_seconds_max)

func _play_walk_animation() -> void:
	if _dir < 0:
		if _anim.current_animation != "WalkLeft":
			_anim.play("WalkLeft")
	else:
		if _anim.current_animation != "WalkRight":
			_anim.play("WalkRight")

func _random_spawn() -> void:
	var base_pos := global_position
	var save_point := _find_node_in_ancestors(^"SavePoint")
	if save_point is Node2D:
		base_pos = (save_point as Node2D).global_position
	
	var x := base_pos.x
	
	if _has_bounds:
		var left := _spawn_bounds.position.x + _bounds_margin_x
		var right := _spawn_bounds.position.x + _spawn_bounds.size.x - _bounds_margin_x
		
		if is_enemy and is_instance_valid(_player):
			var map_width = _spawn_bounds.size.x
			var min_dist = map_width * 0.5
			var p_x = _player.global_position.x
			
			var valid_ranges = []
			if (p_x - min_dist) > left:
				valid_ranges.append(Vector2(left, p_x - min_dist))
			if (p_x + min_dist) < right:
				valid_ranges.append(Vector2(p_x + min_dist, right))
			
			if valid_ranges.size() > 0:
				var r = valid_ranges[_rng.randi() % valid_ranges.size()]
				x = _rng.randf_range(r.x, r.y)
			else:
				x = _rng.randf_range(left, right)
		else:
			x = _rng.randf_range(left, right)
			
		var ray_top_bounds = _spawn_bounds.position.y - spawn_ray_up
		var ray_bottom_bounds = _spawn_bounds.position.y + _spawn_bounds.size.y + spawn_ray_down
		
		var start := Vector2(x, ray_top_bounds)
		var end := Vector2(x, ray_bottom_bounds)
		
		_perform_spawn_raycast(start, end, base_pos)
		return

	x = base_pos.x + _rng.randf_range(-spawn_range_x, spawn_range_x)
	var ray_top := base_pos.y - spawn_ray_up
	var ray_bottom := base_pos.y + spawn_ray_down
	_perform_spawn_raycast(Vector2(x, ray_top), Vector2(x, ray_bottom), base_pos)

func _perform_spawn_raycast(start: Vector2, end: Vector2, fallback_pos: Vector2) -> void:
	var space := get_world_2d().direct_space_state
	var query := PhysicsRayQueryParameters2D.create(start, end)
	query.exclude = [self]
	query.collide_with_areas = false
	var hit := space.intersect_ray(query)
	if hit.is_empty():
		global_position = fallback_pos
		return
	
	var shape := $CollisionShape2D.shape as RectangleShape2D
	var half_height := 0.0
	if shape != null:
		half_height = shape.size.y * 0.5
	global_position = Vector2(hit.position.x, hit.position.y - half_height)


func _auto_tune() -> void:
	if not auto_tune_to_room:
		return
	
	var bounds := _get_room_bounds()
	if bounds.size.x <= 0.0 or bounds.size.y <= 0.0:
		return
	
	_spawn_bounds = bounds
	_has_bounds = true
	
	var shape := $CollisionShape2D.shape as RectangleShape2D
	if shape != null:
		_bounds_margin_x = maxf(16.0, shape.size.x)
	else:
		_bounds_margin_x = 24.0
	
	var width := _spawn_bounds.size.x
	speed = clampf(width / 8.0, speed_min, speed_max)
	spawn_range_x = width * 0.45
	
	var dist_min := clampf(width * 0.10, 160.0, 500.0)
	var dist_max := clampf(width * 0.25, 300.0, 900.0)
	if dist_max < dist_min:
		dist_max = dist_min
	
	change_dir_seconds_min = clampf(dist_min / speed, 0.6, 3.0)
	change_dir_seconds_max = clampf(dist_max / speed, change_dir_seconds_min, 4.5)

func _apply_visual_tuning() -> void:
	if not is_instance_valid(_sprite) or not is_instance_valid(_collider):
		return
	
	var vf: int = maxi(1, _sprite.vframes)
	var tex: Texture2D = _sprite.texture
	if auto_scale_sprite and tex != null:
		var frame_h_px := float(tex.get_height()) / float(vf)
		if frame_h_px > 0.0 and target_frame_height_world > 0.0:
			var s := target_frame_height_world / frame_h_px
			_sprite.scale = Vector2(s, s)
	
	var frame_h_world := 0.0
	if tex != null:
		frame_h_world = (float(tex.get_height()) / float(vf)) * _sprite.scale.y
	
	var shape := _collider.shape as RectangleShape2D
	if auto_resize_collider:
		if shape == null:
			shape = RectangleShape2D.new()
			_collider.shape = shape
		var h := maxf(16.0, target_frame_height_world * collider_height_ratio)
		var w := maxf(10.0, target_frame_height_world * collider_width_ratio)
		shape.size = Vector2(w, h)
	
	var half_height := 0.0
	shape = _collider.shape as RectangleShape2D
	if shape != null:
		half_height = shape.size.y * 0.5
		if _has_bounds:
			_bounds_margin_x = maxf(16.0, shape.size.x)
	
	if frame_h_world > 0.0:
		var pad_px := feet_from_bottom_px
		if auto_detect_feet and pad_px <= 0.0:
			pad_px = _get_cached_feet_pad_px()
		var feet_pad_world := pad_px * _sprite.scale.y
		_sprite.position.y = half_height - (frame_h_world * 0.5) + feet_pad_world

func _get_cached_feet_pad_px() -> float:
	if _cached_feet_pad_px >= 0.0:
		return _cached_feet_pad_px
	
	var tex: Texture2D = _sprite.texture
	if tex == null:
		_cached_feet_pad_px = 0.0
		return _cached_feet_pad_px
	
	var img := tex.get_image()
	if img == null:
		_cached_feet_pad_px = 0.0
		return _cached_feet_pad_px
	
	var hf: int = maxi(1, _sprite.hframes)
	var vf: int = maxi(1, _sprite.vframes)
	var frame_w := int(floor(float(tex.get_width()) / float(hf)))
	var frame_h := int(floor(float(tex.get_height()) / float(vf)))
	if frame_w <= 0 or frame_h <= 0:
		_cached_feet_pad_px = 0.0
		return _cached_feet_pad_px
	
	var pads: Array[int] = []
	var sample_cols_left: Array[int] = [0, int(hf / 2), hf - 1]
	var sample_cols_right: Array[int] = [0, int(min(2, hf - 1)), int(min(6, hf - 1))]
	
	for col in sample_cols_left:
		pads.append(_measure_pad_for_frame(img, col, 0, frame_w, frame_h))
	for col in sample_cols_right:
		pads.append(_measure_pad_for_frame(img, col, 1, frame_w, frame_h))
	
	var best := frame_h
	for p in pads:
		best = min(best, p)
	
	_cached_feet_pad_px = float(clampi(best, 0, frame_h))
	return _cached_feet_pad_px

func _measure_pad_for_frame(img: Image, col: int, row: int, frame_w: int, frame_h: int) -> int:
	var x0 := col * frame_w
	var y0 := row * frame_h
	
	for y in range(frame_h - 1, -1, -1):
		for x in range(frame_w):
			if img.get_pixel(x0 + x, y0 + y).a > 0.02:
				return (frame_h - 1) - y
	return 0

func _get_room_bounds() -> Rect2:
	var player := get_tree().get_first_node_in_group(&"player") as Node
	if player != null:
		var cam := player.get_node_or_null(^"Camera2D") as Camera2D
		if cam != null:
			var left := float(cam.limit_left)
			var right := float(cam.limit_right)
			var top := float(cam.limit_top)
			var bottom := float(cam.limit_bottom)
			if right > left and bottom > top:
				return Rect2(Vector2(left, top), Vector2(right - left, bottom - top))
	
	return Rect2()

func _find_node_in_ancestors(name: NodePath) -> Node:
	var p: Node = self
	while p != null:
		var found := p.get_node_or_null(name)
		if found != null:
			return found
		p = p.get_parent()
	return null

func _setup_combatant() -> void:
	if not is_instance_valid(combatant):
		return
	_sync_combatant_facing()
	if combatant.has_signal("died"):
		var cb := Callable(self, &"_on_died")
		if not combatant.died.is_connected(cb):
			combatant.died.connect(cb)

func _on_died() -> void:
	defeated.emit(self)
	queue_free()

func apply_raw_damage(raw_damage: float) -> float:
	if not is_enemy:
		return 0.0
	if is_instance_valid(combatant):
		return combatant.apply_raw_damage(raw_damage)
	return float(raw_damage)

func _sync_combatant_facing() -> void:
	if is_instance_valid(combatant):
		combatant.set_facing_dir(get_facing_dir())
