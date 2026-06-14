extends Node2D

@export var tile_size: int = 32
@export var room_tiles_w: int = 27
@export var room_tiles_h: int = 15

@export var template: Texture2D
@export var foreground_texture: Texture2D
@export var background_texture: Texture2D

@export var default_ground_y: int = 9
@export var default_ground_rows: int = 2

@export var empty_alpha_threshold: float = 0.1
@export var color_tolerance: float = 0.04

@export var ground_color: Color = Color(1, 1, 1, 1)
@export var air_block_color: Color = Color(0.5, 0.5, 0.5, 1)

@export var ground_source_id: int = 1
@export var ground_atlas_coords: Vector2i = Vector2i(3, 6)
@export var ground_alternative_tile: int = 0

@export var air_source_id: int = 1
@export var air_atlas_coords: Vector2i = Vector2i(3, 6)
@export var air_alternative_tile: int = 0

@onready var _background_rect: TextureRect = $BackgroundLayer/BackgroundTexture
@onready var _fallback_rect: ColorRect = $BackgroundLayer/FallbackColor
@onready var _foreground_texture_rect: TextureRect = get_node_or_null("ForegroundTextureLayer/ForegroundTexture") as TextureRect
@onready var _foreground: TileMapLayer = $TileMap/Foreground

func _ready() -> void:
	_apply_foreground_texture()
	_apply_background()
	_apply_template_to_tiles()
	if template == null:
		_build_default_floor()

func _apply_foreground_texture() -> void:
	if _foreground_texture_rect != null:
		_foreground_texture_rect.texture = foreground_texture
		_foreground_texture_rect.visible = _foreground_texture_rect.texture != null

func _apply_background() -> void:
	if _background_rect != null:
		_background_rect.texture = background_texture if background_texture != null else template
		_background_rect.visible = _background_rect.texture != null
	if _fallback_rect != null:
		_fallback_rect.visible = _background_rect == null or _background_rect.texture == null

func _apply_template_to_tiles() -> void:
	if _foreground == null:
		return
	_foreground.clear()
	if template == null:
		return
	var img := template.get_image()
	if img == null:
		return
	img.decompress()
	img.lock()
	var w := img.get_width()
	var h := img.get_height()
	var step_x := _compute_step(w, room_tiles_w)
	var step_y := _compute_step(h, room_tiles_h)
	for y in range(room_tiles_h):
		for x in range(room_tiles_w):
			var px := _sample_pixel(img, x, y, step_x, step_y)
			if px.a <= empty_alpha_threshold:
				continue
			if _color_close(px, ground_color, color_tolerance):
				_foreground.set_cell(Vector2i(x, y), ground_source_id, ground_atlas_coords, ground_alternative_tile)
			elif _color_close(px, air_block_color, color_tolerance):
				_foreground.set_cell(Vector2i(x, y), air_source_id, air_atlas_coords, air_alternative_tile)
	img.unlock()

func _compute_step(size_px: int, tiles: int) -> int:
	if tiles <= 0:
		return 1
	if size_px == tiles:
		return 1
	return maxi(1, size_px / tiles)

func _sample_pixel(img: Image, x: int, y: int, step_x: int, step_y: int) -> Color:
	var w := img.get_width()
	var h := img.get_height()
	if w == room_tiles_w and h == room_tiles_h:
		return img.get_pixel(x, y)
	var sx := clampi(x * step_x + step_x / 2, 0, w - 1)
	var sy := clampi(y * step_y + step_y / 2, 0, h - 1)
	return img.get_pixel(sx, sy)

func _color_close(a: Color, b: Color, tol: float) -> bool:
	return absf(a.r - b.r) <= tol and absf(a.g - b.g) <= tol and absf(a.b - b.b) <= tol and absf(a.a - b.a) <= tol

func _build_default_floor() -> void:
	if _foreground == null:
		return
	var ground_y := clampi(default_ground_y, 0, maxi(0, room_tiles_h - 1))
	var rows := clampi(default_ground_rows, 1, room_tiles_h - ground_y)
	for y in range(ground_y, ground_y + rows):
		for x in range(0, room_tiles_w):
			_foreground.set_cell(Vector2i(x, y), ground_source_id, ground_atlas_coords, ground_alternative_tile)
