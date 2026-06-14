extends RefCounted

var _available: Dictionary = {}
var _active: Dictionary = {}

func acquire(pool_key: StringName, scene: PackedScene) -> Node:
	var list: Array
	if _available.has(pool_key):
		list = _available[pool_key] as Array
	else:
		list = []
	
	var node: Node
	if list.is_empty():
		node = scene.instantiate()
	else:
		node = list.pop_back()
	_available[pool_key] = list
	
	_active[node] = pool_key
	return node

func release(node: Node) -> void:
	if node == null:
		return
	if not _active.has(node):
		node.queue_free()
		return
	
	var pool_key: StringName = _active[node]
	_active.erase(node)
	
	if node.is_inside_tree():
		node.get_parent().remove_child(node)
	
	var list: Array
	if _available.has(pool_key):
		list = _available[pool_key] as Array
	else:
		list = []
	list.append(node)
	_available[pool_key] = list
