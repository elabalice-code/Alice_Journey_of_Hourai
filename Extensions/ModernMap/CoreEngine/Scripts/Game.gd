# This is the main script of the game. It manages the current map and some other stuff.
extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysGame.gd"
class_name ModernMapGame

const SaveManager = preload("res://addons/MetroidvaniaSystem/Template/Scripts/SaveManager.gd")
const DialogueManagerActor = preload("res://CoreEngine/Scripts/Actor/DialogueManagerActor.gd")
const AliceNPC = preload("res://CoreEngine/Objects/AliceNPC.tscn")
const SAVE_PATH = "user://modern_example_save_data.sav"


# The game starts in this map. Uses special annotation that enabled dedicated inspector plugin.
@export_file("room_link") var starting_map: String

# Number of collected collectibles. Setting it also updates the counter.
var collectibles: int:
	set(count):
		collectibles = count
		%CollectibleCount.text = "%d/6" % count

# The coordinates of generated rooms. MetSys does not keep this list, so it needs to be done manually.
var generated_rooms: Array[Vector3i]
# The typical array of game events. It's supplementary to the storable objects.
var events: Array[String]
# For Custom Runner integration.
var custom_run: bool

func _ready() -> void:
	# A trick for static object reference (before static vars were a thing).
	get_script().set_meta(&"singleton", self)
	# Make sure MetSys is in initial state.
	# Does not matter in this project, but normally this ensures that the game works correctly when you exit to menu and start again.
	MetSys.reset_state()
	# Assign player for MetSysGame.
	set_player($Player)
	
	# Add module for map overlays and clearing fog.
	add_module("FogOfMystery.gd")
	
	if FileAccess.file_exists(SAVE_PATH):
		# If save data exists, load it using MetSys SaveManager.
		var save_manager := SaveManager.new()
		save_manager.load_from_text(SAVE_PATH)
		# Assign loaded values.
		collectibles = save_manager.get_value("collectible_count")
		generated_rooms.assign(save_manager.get_value("generated_rooms"))
		events.assign(save_manager.get_value("events"))
		player.abilities.assign(save_manager.get_value("abilities"))
		save_manager.retrieve_game(self)
		
		if not custom_run:
			var loaded_starting_map: String = save_manager.get_value("current_room")
			if not loaded_starting_map.is_empty(): # Some compatibility problem.
				starting_map = loaded_starting_map
	else:
		# If no data exists, set empty one.
		MetSys.set_save_data()
	
	# Initialize room when it changes.
	room_loaded.connect(init_room, CONNECT_DEFERRED)
	# Load the starting room.
	load_room(starting_map)
	# Reguired to unblock Fog of Mystery.
	MetSys.room_changed.emit(starting_map)
	
	# Find the save point and teleport the player to it, to start at the save point.
	var start := map.get_node_or_null(^"SavePoint")
	if start and not custom_run:
		player.position = start.position
	
	# Add module for room transitions.
	add_module("RoomTransitions.gd")

	# Add Dialogue Manager
	var dialogue_mgr = DialogueManagerActor.new()
	dialogue_mgr.name = "DialogueManagerActor"
	add_child(dialogue_mgr)

# Returns this node from anywhere.

static func get_singleton() -> ModernMapGame:
	return (ModernMapGame as Script).get_meta(&"singleton") as ModernMapGame

# Save game using MetSys SaveManager.
func save_game():
	var save_manager := SaveManager.new()
	save_manager.store_game(self)
	save_manager.set_value("collectible_count", collectibles)
	save_manager.set_value("generated_rooms", generated_rooms)
	save_manager.set_value("events", events)
	save_manager.set_value("current_room", MetSys.get_current_room_id())
	save_manager.set_value("abilities", player.abilities)
	save_manager.save_as_text(SAVE_PATH)

func init_room():
	MetSys.get_current_room_instance().adjust_camera_limits($Player/Camera2D)
	player.on_enter()
	_apply_room_collision_from_metadata()
	
	# Initializes MetSys.get_current_coords(), so you can use it from the beginning.
	if MetSys.last_player_position.x == Vector2i.MAX.x:
		MetSys.set_player_position(player.position)

	_spawn_alice()

func _apply_room_collision_from_metadata() -> void:
	if map == null:
		return
	var existing := map.get_node_or_null(^"CollisionFromJson")
	if existing != null:
		existing.queue_free()
	var mode := str(map.get_meta(&"collision_mode", ""))
	if mode.to_lower() == "fgtex" or mode.to_lower() == "foreground_texture":
		var path := str(map.get_meta(&"collision_fgtex_path", ""))
		if path.is_empty():
			return
		var data := _load_collision_json(path)
		if data.is_empty():
			return
		_build_collision_from_json_data(data)

func _load_collision_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var txt := f.get_as_text()
	var parsed = JSON.parse_string(txt)
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}
	return parsed as Dictionary

func _build_collision_from_json_data(data: Dictionary) -> void:
	if map == null:
		return
	var root := StaticBody2D.new()
	root.name = "CollisionFromJson"
	root.collision_layer = 1
	root.collision_mask = 1
	map.add_child(root)
	
	var polys = data.get("Polygons", data.get("polygons", []))
	if typeof(polys) == TYPE_ARRAY and (polys as Array).size() > 0:
		for poly in polys as Array:
			var points := _parse_polygon_points(poly)
			if points.size() < 3:
				continue
			var cp := CollisionPolygon2D.new()
			cp.polygon = points
			root.add_child(cp)
		return
	
	var solid = data.get("Solid", data.get("solid", []))
	var room_w := int(data.get("RoomWidth", data.get("roomWidth", 0)))
	var room_h := int(data.get("RoomHeight", data.get("roomHeight", 0)))
	if room_w <= 0 or room_h <= 0:
		return
	if typeof(solid) != TYPE_ARRAY:
		return
	_build_grid_collision_shapes(root, solid as Array, room_w, room_h, 32)

func _parse_polygon_points(poly) -> PackedVector2Array:
	var out := PackedVector2Array()
	if typeof(poly) != TYPE_ARRAY:
		return out
	for pt in poly as Array:
		if typeof(pt) == TYPE_DICTIONARY:
			var d := pt as Dictionary
			var x := float(d.get("X", d.get("x", 0.0)))
			var y := float(d.get("Y", d.get("y", 0.0)))
			out.append(Vector2(x, y))
		elif typeof(pt) == TYPE_ARRAY:
			var a := pt as Array
			if a.size() >= 2:
				out.append(Vector2(float(a[0]), float(a[1])))
	return out

func _build_grid_collision_shapes(root: StaticBody2D, solid: Array, room_w: int, room_h: int, tile_size: int) -> void:
	var expected := room_w * room_h
	if solid.size() < expected:
		return
	for y in range(room_h):
		var x := 0
		while x < room_w:
			var idx := y * room_w + x
			if idx < 0 or idx >= solid.size() or not bool(solid[idx]):
				x += 1
				continue
			var start_x := x
			while x < room_w and bool(solid[y * room_w + x]):
				x += 1
			var seg_len := x - start_x
			var shape := RectangleShape2D.new()
			shape.size = Vector2(seg_len * tile_size, tile_size)
			var cs := CollisionShape2D.new()
			cs.shape = shape
			cs.position = Vector2(start_x * tile_size + (seg_len * tile_size) / 2.0, y * tile_size + tile_size / 2.0)
			root.add_child(cs)

func _spawn_alice():
	var room_id = MetSys.get_current_room_id()
	# Check if Alice should spawn
	# Logic: Spawn in all rooms except maybe start/special ones? 
	# The prompt says: "In random rooms... enemy", "In other maps... NPC"
	# So she spawns everywhere? Or just where we want her?
	# Assuming she spawns in ALL rooms for this task.
	
	# Check if already exists (statically placed)
	if map.has_node("AliceNPC"):
		return
		
	var alice = AliceNPC.instantiate()
	var is_gen = room_id.begins_with("GEN")
	
	alice.is_enemy = is_gen
	
	# Add to map so she is part of the room
	map.add_child(alice)
	
	# Position is handled by Alice's _ready -> _random_spawn
	# which we modified to handle the constraints.

# Customized load function that handles maps generated in Dice.tscn.
func _load_room(path: String) -> Node:
	if not path.begins_with("GEN"):
		return load(path).instantiate()
	
	# Base scene that will be customized (Junction.tscn).
	var prototype := preload("res://CoreEngine/Maps/Junction.tscn").instantiate()
	prototype.scene_file_path = path
	
	var config := path.split("/")
	# Assign values to the scene (see the script in Junction.tscn).
	prototype.exits = config[2].to_int()
	prototype.has_collectible = config[3] == "true"
	# Apply the values. It has to happen before the scene enters tree.
	prototype.apply_config()
	
	return prototype
