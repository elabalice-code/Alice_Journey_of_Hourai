extends RefCounted
class_name MapNodeFacts

static func collect(map: Node) -> Dictionary:
	var facts: Dictionary = {
		"node_positions": {},
		"portal_positions_by_target_map": {}
	}
	if map == null:
		return facts
	var node_positions: Dictionary = facts["node_positions"] as Dictionary
	var portal_positions_by_target_map: Dictionary = facts["portal_positions_by_target_map"] as Dictionary
	for node in map.find_children("*", "Node2D", true, false):
		var node2d := node as Node2D
		if node2d == null:
			continue
		_remember_node_position(map, node2d, node_positions)
		_remember_portal_position(node2d, portal_positions_by_target_map)
	return facts

static func _remember_node_position(map: Node, node: Node2D, node_positions: Dictionary) -> void:
	var position: Vector2 = node.position
	var relative_path: String = String(map.get_path_to(node))
	if not relative_path.is_empty() and not node_positions.has(relative_path):
		node_positions[relative_path] = position
	var node_name: String = String(node.name)
	if not node_name.is_empty() and not node_positions.has(node_name):
		node_positions[node_name] = position

static func _remember_portal_position(node: Node2D, portal_positions_by_target_map: Dictionary) -> void:
	var target_map: String = _get_string_property(node, &"target_map")
	if target_map.is_empty() or portal_positions_by_target_map.has(target_map):
		return
	portal_positions_by_target_map[target_map] = node.position

static func _get_string_property(obj: Object, prop: StringName) -> String:
	if obj == null:
		return ""
	for p in obj.get_property_list():
		var property_name: StringName = StringName(p.get("name", ""))
		if property_name == prop:
			return str(obj.get(String(prop)))
	return ""
