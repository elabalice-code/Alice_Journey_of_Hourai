extends RefCounted
class_name MapRuntimeSurface

const CollisionLayoutScript = preload("res://CoreEngine/Scripts/Helper/Map/CollisionLayout.gd")

const BACKGROUND_PERSPECTIVE_SHADER_CODE := "shader_type canvas_item;\n\nuniform float upscale = 1.0;\nuniform vec2 focus = vec2(0.5, 0.5);\n\nvoid fragment() {\n\tvec2 tex_size = vec2(1.0) / TEXTURE_PIXEL_SIZE;\n\tvec2 screen_size = vec2(1.0) / SCREEN_PIXEL_SIZE;\n\tfloat cover_scale = max(screen_size.x / tex_size.x, screen_size.y / tex_size.y);\n\tvec2 cover_span = (screen_size / cover_scale) / tex_size;\n\tvec2 cover_offset = (vec2(1.0) - cover_span) * 0.5;\n\tfloat s = max(upscale, 1.0);\n\tvec2 zoom_span = cover_span / s;\n\tvec2 max_move = cover_span - zoom_span;\n\tvec2 f = clamp(focus, vec2(0.0), vec2(1.0));\n\tvec2 zoom_offset = cover_offset + max_move * f;\n\tvec2 uv = zoom_offset + UV * zoom_span;\n\tCOLOR = texture(TEXTURE, uv);\n}\n"

static func apply_surface_metadata(map_root: Node, player: Node2D) -> Dictionary:
	apply_foreground_collision_from_metadata(map_root)
	apply_foreground_texture_transform_from_metadata(map_root)
	return apply_background_texture_transform_from_metadata(map_root, player)

static func ensure_world_foreground_texture_sprite(map_root: Node) -> Sprite2D:
	if map_root == null:
		return null

	var layer := map_root.get_node_or_null(^"ForegroundTextureLayer")
	if layer is Node2D:
		var s := map_root.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as Sprite2D
		if s != null:
			return s

		var tr := map_root.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as TextureRect
		if tr == null or tr.texture == null:
			return null

		tr.name = "ForegroundTexture_UI"
		tr.visible = false

		var ns := Sprite2D.new()
		ns.name = "ForegroundTexture"
		ns.texture = tr.texture
		ns.centered = false
		ns.position = Vector2.ZERO
		(layer as Node2D).add_child(ns)
		return ns

	if layer is CanvasLayer:
		var texture: Texture2D = null
		var tr2 := map_root.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as TextureRect
		if tr2 != null:
			texture = tr2.texture
		var s2 := map_root.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as Sprite2D
		if s2 != null:
			texture = s2.texture

		if texture == null:
			return null

		(layer as CanvasLayer).name = "ForegroundTextureLayer_UI"
		(layer as CanvasLayer).visible = false

		var nl := Node2D.new()
		nl.name = "ForegroundTextureLayer"
		nl.z_index = -1
		map_root.add_child(nl)

		var ns2 := Sprite2D.new()
		ns2.name = "ForegroundTexture"
		ns2.texture = texture
		ns2.centered = false
		ns2.position = Vector2.ZERO
		nl.add_child(ns2)
		return ns2

	return map_root.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as Sprite2D

static func apply_foreground_collision_from_metadata(map_root: Node) -> void:
	if map_root == null:
		return
	var existing := map_root.get_node_or_null(^"CollisionFromJson")
	if existing != null:
		existing.queue_free()

	var mode := str(map_root.get_meta(&"collision_mode", ""))
	if not CollisionLayoutScript.uses_foreground_texture(mode):
		return

	var path := str(map_root.get_meta(&"collision_fgtex_path", ""))
	if path.is_empty():
		return
	var data := load_collision_json(path)
	if data.is_empty():
		return
	build_collision_from_json_data(map_root, data)

static func apply_foreground_texture_transform_from_metadata(map_root: Node) -> void:
	if map_root == null:
		return
	var fg := ensure_world_foreground_texture_sprite(map_root)
	if fg == null or fg.texture == null:
		return

	var mode := str(map_root.get_meta(&"collision_mode", "")).to_lower()
	if not CollisionLayoutScript.uses_foreground_texture(mode):
		return

	var upscale := _float_meta(map_root, &"foreground_texture_upscale", 1.0)
	if upscale <= 0.0:
		upscale = 1.0

	fg.centered = false
	fg.region_enabled = false
	fg.scale = Vector2(upscale, upscale)

	var room_sz := room_world_size_from_collision_json(map_root)
	if room_sz == Vector2.ZERO:
		fg.position = Vector2.ZERO
		return

	var anchor := str(map_root.get_meta(&"foreground_texture_anchor", "TopLeft")).to_lower()
	fg.position = CollisionLayoutScript.anchor_position(anchor, room_sz, fg.texture.get_size() * upscale)

static func apply_background_texture_transform_from_metadata(map_root: Node, player: Node2D) -> Dictionary:
	if map_root == null:
		return {}

	var bg := map_root.get_node_or_null(^"BackgroundLayer/BackgroundTexture") as TextureRect
	if bg == null or bg.texture == null:
		return {}

	var upscale := _float_meta(map_root, &"background_texture_upscale", 1.0)
	if upscale < 1.0:
		upscale = 1.0

	var shader := Shader.new()
	shader.code = BACKGROUND_PERSPECTIVE_SHADER_CODE

	var mat := ShaderMaterial.new()
	mat.shader = shader
	mat.set_shader_parameter("upscale", upscale)
	mat.set_shader_parameter("focus", player_focus_in_foreground(map_root, player))
	bg.material = mat

	return {
		"background_rect": bg,
		"background_material": mat,
		"background_upscale": upscale
	}

static func print_background_diagnostics(map_root: Node, viewport_size: Vector2) -> void:
	if map_root == null:
		return
	var bg_layer := map_root.get_node_or_null(NodePath("BackgroundLayer")) as CanvasLayer
	var bg := map_root.get_node_or_null(NodePath("BackgroundLayer/BackgroundTexture")) as TextureRect
	if bg_layer == null:
		print("Game.init_room BackgroundLayer not found")
	elif bg_layer.visible == false:
		print("Game.init_room BackgroundLayer visible=false layer=%d" % int(bg_layer.layer))
	else:
		print("Game.init_room BackgroundLayer visible=true layer=%d" % int(bg_layer.layer))
	if bg == null:
		print("Game.init_room BackgroundTexture not found")
		return

	var tex_path := ""
	if bg.texture != null:
		tex_path = str(bg.texture.resource_path)
	var tex_size := Vector2.ZERO
	if bg.texture != null:
		tex_size = bg.texture.get_size()
	print("Game.init_room BackgroundTexture visible=%s modulate=%s z_index=%d expand_mode=%d stretch_mode=%d tex=%s tex_size=%s viewport=%s rect_size=%s" % [
		str(bg.visible),
		str(bg.modulate),
		int(bg.z_index),
		int(bg.expand_mode),
		int(bg.stretch_mode),
		tex_path,
		str(tex_size),
		str(viewport_size),
		str(bg.size)
	])

static func update_background_texture_focus(surface_state: Dictionary, map_root: Node, player: Node2D) -> void:
	var rect := surface_state.get("background_rect", null) as TextureRect
	if not is_instance_valid(rect):
		return
	var mat := surface_state.get("background_material", null) as ShaderMaterial
	if mat == null:
		return
	mat.set_shader_parameter("focus", player_focus_in_foreground(map_root, player))

static func apply_camera_limits_from_metadata(map_root: Node, camera: Camera2D, has_metroidvania_cells: bool) -> void:
	if map_root == null or camera == null or has_metroidvania_cells:
		return

	var bounds := map_world_bounds_rect(map_root)
	if bounds.size == Vector2.ZERO:
		return
	camera.limit_left = int(floor(bounds.position.x))
	camera.limit_top = int(floor(bounds.position.y))
	camera.limit_right = int(ceil(bounds.end.x))
	camera.limit_bottom = int(ceil(bounds.end.y))

static func room_world_size_from_collision_json(map_root: Node) -> Vector2:
	if map_root == null:
		return Vector2.ZERO
	var path := CollisionLayoutScript.selected_path_from_metadata(_collision_metadata(map_root))
	if path.is_empty():
		return Vector2.ZERO
	var data := load_collision_json(path)
	if data.is_empty():
		return Vector2.ZERO
	return CollisionLayoutScript.room_world_size(data)

static func map_world_bounds_rect(map_root: Node) -> Rect2:
	return CollisionLayoutScript.union_bounds(room_world_size_from_collision_json(map_root), foreground_world_rect(map_root))

static func foreground_world_rect(map_root: Node) -> Rect2:
	var fg := ensure_world_foreground_texture_sprite(map_root)
	if fg == null or fg.texture == null:
		return Rect2()
	var sz := fg.texture.get_size() * fg.scale
	if fg.region_enabled:
		sz = fg.region_rect.size * fg.scale
	if sz.x <= 0.0 or sz.y <= 0.0:
		return Rect2()
	return Rect2(fg.position, sz)

static func player_focus_in_foreground(map_root: Node, player: Node2D) -> Vector2:
	if map_root == null or not is_instance_valid(player):
		return Vector2(0.5, 0.5)

	var world_pos := player.global_position
	var pos := (map_root as Node2D).to_local(world_pos) if map_root is Node2D else world_pos

	var fg_rect := foreground_world_rect(map_root)
	if fg_rect.size != Vector2.ZERO:
		var rel := (pos - fg_rect.position) / fg_rect.size
		return Vector2(clampf(rel.x, 0.0, 1.0), clampf(rel.y, 0.0, 1.0))

	var room_sz := room_world_size_from_collision_json(map_root)
	if room_sz == Vector2.ZERO:
		return Vector2(0.5, 0.5)

	var rel2 := pos / room_sz
	return Vector2(clampf(rel2.x, 0.0, 1.0), clampf(rel2.y, 0.0, 1.0))

static func load_collision_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var txt := f.get_as_text()
	var parsed_any: Variant = JSON.parse_string(txt)
	if typeof(parsed_any) != TYPE_DICTIONARY:
		return {}
	return parsed_any as Dictionary

static func build_collision_from_json_data(map_root: Node, data: Dictionary) -> void:
	if map_root == null:
		return
	var root := StaticBody2D.new()
	root.name = "CollisionFromJson"
	root.collision_layer = 1
	root.collision_mask = 1
	map_root.add_child(root)

	var polygons: Array = CollisionLayoutScript.polygon_list(data)
	if not polygons.is_empty():
		for points in polygons:
			var cp := CollisionPolygon2D.new()
			cp.polygon = points as PackedVector2Array
			root.add_child(cp)
		return

	for rect in CollisionLayoutScript.solid_grid_rects(data):
		var shape := RectangleShape2D.new()
		shape.size = rect.size
		var cs := CollisionShape2D.new()
		cs.shape = shape
		cs.position = rect.position + rect.size / 2.0
		root.add_child(cs)

static func _collision_metadata(map_root: Node) -> Dictionary:
	if map_root == null:
		return {}
	return {
		"collision_mode": str(map_root.get_meta(&"collision_mode", "")),
		"collision_tile_path": str(map_root.get_meta(&"collision_tile_path", "")),
		"collision_fgtex_path": str(map_root.get_meta(&"collision_fgtex_path", ""))
	}

static func _float_meta(map_root: Node, key: StringName, default_value: float) -> float:
	if map_root == null:
		return default_value
	var value: Variant = map_root.get_meta(key, default_value)
	if typeof(value) == TYPE_INT or typeof(value) == TYPE_FLOAT:
		return float(value)
	return float(str(value))
