extends Node2D

@export var tile_size: int = 32
@export var room_tiles_w: int = 27
@export var ground_y: int = 13
@export var ground_thickness: int = 2
@export var ground_source_id: int = 1
@export var ground_atlas_coords: Vector2i = Vector2i(3, 6)

@onready var _foreground: TileMapLayer = $TileMap/Foreground

func _ready() -> void:
	if _foreground == null:
		return
	_foreground.clear()
	_build_ground()

func _build_ground() -> void:
	var start_x := -2
	var end_x := room_tiles_w + 2
	for y in range(ground_y, ground_y + ground_thickness):
		for x in range(start_x, end_x):
			_foreground.set_cell(Vector2i(x, y), ground_source_id, ground_atlas_coords, 0)
