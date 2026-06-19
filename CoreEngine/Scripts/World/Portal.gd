# A portal object that transports to another world layer.
extends Area2D
const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
# The target map after entering the portal.
@export_file("room_link") var target_map: String
@export var target_area: StringName = &""
@export var target_entry_node: StringName = &""
@export_dir var portal_anim_dir: String = ""
@export var portal_anim_fps: float = 12.0
@export var portal_upscale: float = 0.25
@onready var _fallback_sprite: Sprite2D = get_node_or_null(^"VisualRoot/Sprite2D") as Sprite2D
@onready var _anim_sprite: AnimatedSprite2D = get_node_or_null(^"VisualRoot/Visual") as AnimatedSprite2D
@onready var _visual_root: Node2D = get_node_or_null(^"VisualRoot") as Node2D
@onready var _rot_player: AnimationPlayer = get_node_or_null(^"VisualRoot/AnimationPlayer") as AnimationPlayer

func _ready() -> void:
	_apply_portal_animation()
	_apply_portal_upscale()
	_apply_rotation_policy()

func _apply_portal_upscale() -> void:
	if _visual_root == null:
		return
	var s := maxf(0.001, portal_upscale)
	_visual_root.scale = Vector2(s, s)

func _apply_rotation_policy() -> void:
	if not _has_custom_skin():
		return
	if _rot_player != null:
		_rot_player.stop()
	rotation = 0.0
	if _visual_root != null:
		_visual_root.rotation = 0.0

func _has_custom_skin() -> bool:
	if not _resolve_portal_anim_dir().is_empty():
		return true
	if _fallback_sprite != null and _fallback_sprite.texture != null:
		var rp := _fallback_sprite.texture.resource_path
		if not rp.is_empty() and rp != "res://CoreEngine/Sprites/Objects/Portal.png":
			return true
	return false

func _apply_portal_animation() -> void:
	if _anim_sprite == null:
		return
	var dir := _resolve_portal_anim_dir()
	if dir.is_empty():
		if _fallback_sprite != null:
			_fallback_sprite.visible = true
		_anim_sprite.visible = false
		return
	var frames := _load_textures_from_dir(dir)
	if frames.is_empty():
		if _fallback_sprite != null:
			_fallback_sprite.visible = true
		_anim_sprite.visible = false
		return
	var sf := SpriteFrames.new()
	sf.add_animation(&"idle")
	sf.set_animation_loop(&"idle", false)
	sf.set_animation_speed(&"idle", 1.0)
	sf.add_frame(&"idle", frames[0])
	sf.add_animation(&"activate")
	sf.set_animation_loop(&"activate", false)
	sf.set_animation_speed(&"activate", maxf(1.0, portal_anim_fps))
	for t in frames:
		sf.add_frame(&"activate", t)
	_anim_sprite.sprite_frames = sf
	_anim_sprite.visible = true
	if _fallback_sprite != null:
		_fallback_sprite.visible = false
	_anim_sprite.animation = &"idle"
	_anim_sprite.play(&"idle")
	_anim_sprite.stop()
	_anim_sprite.frame = 0

func _resolve_portal_anim_dir() -> String:
	if not portal_anim_dir.is_empty():
		return portal_anim_dir
	var meta_any: Variant = get_meta(&"portal_anim_dir", "")
	var meta := str(meta_any)
	if not meta.is_empty():
		return meta
	return ""

func _load_textures_from_dir(dir_path: String) -> Array[Texture2D]:
	var out: Array[Texture2D] = []
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return out
	dir.list_dir_begin()
	while true:
		var name := dir.get_next()
		if name.is_empty():
			break
		if dir.current_is_dir():
			continue
		var ext := name.get_extension().to_lower()
		if ext != "png" and ext != "jpg" and ext != "jpeg" and ext != "webp":
			continue
		out.append(load(dir_path.path_join(name)) as Texture2D)
	dir.list_dir_end()
	out = out.filter(func(t): return t != null)
	out.sort_custom(func(a, b): return a.resource_path.naturalnocasecmp_to(b.resource_path) < 0)
	return out

func _play_activate_animation_if_any() -> void:
	var anim := _anim_sprite
	if anim == null or not anim.visible:
		return
	if anim.sprite_frames == null:
		return
	if not anim.sprite_frames.has_animation(&"activate"):
		return
	if anim.sprite_frames.get_frame_count(&"activate") <= 1:
		return
	anim.play(&"activate")
	await anim.animation_finished
	anim.stop()
	anim.frame = anim.sprite_frames.get_frame_count(&"activate") - 1

func _on_body_entered(body: Node2D) -> void:
	# If player entered and isn't doing an event (event in this case is entering the portal).
	if body.is_in_group(&"player") and not body.event:
		if bool(body.get_meta(&"IsTransferred", false)):
			return
		body.event = true
		body.velocity = Vector2()
		await _play_activate_animation_if_any()
		# Tween the player position into the portal.
		var tween := create_tween()
		tween.tween_property(body, ^"position", position, 0.5).set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_SINE)
		await tween.finished
		var workbench := WorkbenchService.get_singleton()
		if workbench == null:
			return
		var resolved := ResourceUID.uid_to_path(target_map) if str(target_map).begins_with("uid://") else str(target_map)
		var current_map_path := ""
		var game := Game.get_singleton()
		if game != null and game.map != null:
			current_map_path = str(game.map.scene_file_path)
		print("Portal enter from_room=%s to_target=%s resolved=%s area_id=%s" % [str(MetSys.get_current_room_id()), str(target_map), str(resolved), str(target_area)])
		workbench.send({
			"type": MessageTypes.TYPE_RUNTIME_EVENT_SIGNAL,
			"signal": "Map.Exit",
			"source_domain": "Map",
			"portal_name": name,
			"from_room": str(MetSys.get_current_room_id()),
			"from_room_path": current_map_path,
			"to_room": str(target_map),
			"to_room_path": resolved,
			"target_area": String(target_area)
		})
		var after := {
			"action": StringName("move_player_to_matching_portal"),
			"from_room": MetSys.get_current_room_id(),
			"fallback_node": StringName("Portal"),
			"clear_player_event_after_sec": 0.1
		}
		if target_entry_node != &"":
			after["action"] = StringName("move_player_to_node")
			after["node"] = target_entry_node
		if target_area != &"":
			workbench.send({
				"type": MessageTypes.TYPE_LOAD_AREA_REQUEST,
				"area_id": target_area,
				"entry_room": target_map,
				"after": after
			})
		else:
			workbench.send({
				"type": MessageTypes.TYPE_LOAD_ROOM_REQUEST,
				"target_map": target_map,
				"after": after
			})
		workbench.send({
			"type": MessageTypes.TYPE_RESET_MAP_STARTING_COORDS_REQUEST
		})
		# A trick to reset player's event variable when it's safe to do so (i.e. after some frames).
		get_tree().create_timer(0.25).timeout.connect(body.set.bind(&"event", false))
		
