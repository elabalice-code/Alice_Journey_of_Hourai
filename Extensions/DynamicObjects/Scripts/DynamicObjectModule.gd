extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysModule.gd"

var _workbench: WorkbenchService
var _pool

func _initialize():
	_workbench = WorkbenchService.get_singleton()
	_pool = load("res://Extensions/DynamicObjects/Scripts/ObjectPool.gd").new()
	
	_workbench.register_service(&"object_pool", _pool)
	_workbench.register_actor(self, [ &"spawn_request", &"despawn_request" ], &"_on_workplace")

func _on_workplace(workplace) -> void:
	var msg: Dictionary = workplace.payload
	match workplace.type:
		&"spawn_request":
			_handle_spawn(msg)
		&"despawn_request":
			_handle_despawn(msg)

func _handle_spawn(msg: Dictionary) -> void:
	var scene_path: String = str(msg.get("scene", ""))
	if scene_path.is_empty():
		return
	var scene := load(scene_path) as PackedScene
	if scene == null:
		return
	
	var pool_key_raw = msg.get("pool_key", scene_path)
	var pool_key: StringName = StringName(str(pool_key_raw))
	
	var count: int = int(msg.get("count", 1))
	if count < 1:
		count = 1
	
	var parent: Node = null
	if msg.has("parent_path"):
		var pp := str(msg.get("parent_path", ""))
		if not pp.is_empty():
			parent = game.get_node_or_null(NodePath(pp))
	if parent == null:
		parent = game.get_tree().current_scene
	if parent == null:
		return
	
	var pos: Vector2 = msg.get("position", Vector2.ZERO)
	var rot: float = float(msg.get("rotation", 0.0))
	var props: Dictionary = msg.get("properties", {})
	
	var spawned: Array = []
	for i in count:
		var node: Node = _pool.acquire(pool_key, scene)
		parent.add_child(node)
		if node is Node2D:
			(node as Node2D).global_position = pos
			(node as Node2D).global_rotation = rot
		_apply_properties(node, props)
		spawned.append(node)
	
	var out: Dictionary = {}
	out["type"] = &"spawned"
	out["pool_key"] = pool_key
	out["nodes"] = spawned
	_workbench.send(out)

func _handle_despawn(msg: Dictionary) -> void:
	var node: Node = msg.get("node", null) as Node
	if node == null:
		return
	_pool.release(node)

func _apply_properties(node: Object, props: Dictionary) -> void:
	if node == null or props.is_empty():
		return
	
	var available: Dictionary = {}
	for p in node.get_property_list():
		var n = p.get("name", null)
		if n != null:
			available[StringName(str(n))] = true
	
	for k in props.keys():
		var key: StringName = StringName(str(k))
		if available.has(key):
			node.set(key, props[k])
