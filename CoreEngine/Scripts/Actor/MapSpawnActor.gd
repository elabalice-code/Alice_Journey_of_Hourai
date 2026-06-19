extends RefCounted
class_name MapSpawnActor

const AliceNPCScene = preload("res://CoreEngine/Objects/AliceNPC.tscn")

static func teleport_player_to_save_point_if_any(map_root: Node, player: Node2D, custom_run: bool) -> void:
	if custom_run:
		return
	if map_root == null or not is_instance_valid(player):
		return
	var start := map_root.get_node_or_null(^"SavePoint") as Node2D
	if start != null:
		player.position = start.position

static func ensure_alice(map_root: Node, player: Node2D, initial_room_path: String) -> void:
	if map_root == null:
		return
	var is_starting_map := str(map_root.scene_file_path) == initial_room_path
	var alice := map_root.get_node_or_null(^"Alice") as AliceNPC
	if not is_starting_map:
		if alice != null:
			alice.queue_free()
		return

	if alice == null:
		alice = AliceNPCScene.instantiate() as AliceNPC
		alice.name = "Alice"
		var spawn := map_root.get_node_or_null(^"AliceSpawn") as Node2D
		if spawn != null:
			alice.position = spawn.position
		elif is_instance_valid(player):
			alice.position = player.position + Vector2(96, 0)
		map_root.add_child(alice)

	alice.random_spawn_enabled = false
	alice.is_enemy = false
	alice.set_enemy_mode(false)
