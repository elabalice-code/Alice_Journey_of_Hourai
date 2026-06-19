extends RefCounted
class_name MapPropertyIntentExecutor

const MapPropertyIntentScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertyIntent.gd")

static func execute(map_root: Node, intent: MapPropertyIntent) -> void:
	if map_root == null or intent == null or not intent.is_valid():
		return
	match intent.kind:
		MapPropertyIntentScript.KIND_SET_PROPS:
			_op_set_props(map_root, intent.payload)
		MapPropertyIntentScript.KIND_SET_RESOURCES:
			_op_set_resources(map_root, intent.payload)
		MapPropertyIntentScript.KIND_SET_SHAPE:
			_op_set_shape(map_root, intent.payload)
		MapPropertyIntentScript.KIND_REPLACE_SOURCE_ID:
			_op_replace_source_id(map_root, intent.payload)
		MapPropertyIntentScript.KIND_REPLACE_TILE:
			_op_replace_tile(map_root, intent.payload)

static func _get_target_node(map_root: Node, op: Dictionary) -> Node:
	var node_path := str(op.get("path", ""))
	if node_path.is_empty():
		return null
	if node_path == ".":
		return map_root
	return map_root.get_node_or_null(NodePath(node_path))

static func _node_has_property(obj: Object, prop: StringName) -> bool:
	if obj == null:
		return false
	for p in obj.get_property_list():
		if StringName(p.get("name", "")) == prop:
			return true
	return false

static func _op_set_props(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if target == null:
		return
	var props := op.get("props", {}) as Dictionary
	for k in props.keys():
		var prop := StringName(str(k))
		if not _node_has_property(target, prop):
			continue
		target.set(prop, props[k])

static func _op_set_resources(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if target == null:
		return
	var props := op.get("props", {}) as Dictionary
	for k in props.keys():
		var prop := StringName(str(k))
		if not _node_has_property(target, prop):
			continue
		var path := str(props[k])
		if path.is_empty():
			continue
		var res := load(path)
		if res == null:
			continue
		target.set(prop, res)

static func _op_set_shape(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if target == null:
		return
	if not _node_has_property(target, &"shape"):
		return

	var shape_def := op.get("shape", {}) as Dictionary
	var shape_class := str(shape_def.get("class", ""))
	if shape_class.is_empty():
		return
	if not ClassDB.can_instantiate(shape_class):
		return
	var shape = ClassDB.instantiate(shape_class)
	if shape == null:
		return

	for k in shape_def.keys():
		if String(k) == "class":
			continue
		var prop := StringName(str(k))
		if not _node_has_property(shape, prop):
			continue
		shape.set(prop, shape_def[k])

	target.set(&"shape", shape)

static func _op_replace_source_id(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if not (target is TileMapLayer):
		return
	var layer := target as TileMapLayer

	var from_id := int(op.get("from", -1))
	var to_id := int(op.get("to", -1))
	if from_id < 0 or to_id < 0:
		return

	for cell in layer.get_used_cells():
		var src := layer.get_cell_source_id(cell)
		if src != from_id:
			continue
		layer.set_cell(
			cell,
			to_id,
			layer.get_cell_atlas_coords(cell),
			layer.get_cell_alternative_tile(cell)
		)

static func _op_replace_tile(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if not (target is TileMapLayer):
		return
	var layer := target as TileMapLayer

	var from := op.get("from", {}) as Dictionary
	var to := op.get("to", {}) as Dictionary
	var from_src := int(from.get("source_id", -1))
	var to_src := int(to.get("source_id", -1))
	if from_src < 0 or to_src < 0:
		return

	var from_atlas := from.get("atlas_coords", Vector2i.ZERO) as Vector2i
	var from_alt := int(from.get("alternative_tile", 0))
	var to_atlas := to.get("atlas_coords", Vector2i.ZERO) as Vector2i
	var to_alt := int(to.get("alternative_tile", 0))

	for cell in layer.get_used_cells():
		var src := layer.get_cell_source_id(cell)
		if src != from_src:
			continue
		var atlas := layer.get_cell_atlas_coords(cell)
		var alt := layer.get_cell_alternative_tile(cell)
		if atlas != from_atlas or alt != from_alt:
			continue
		layer.set_cell(cell, to_src, to_atlas, to_alt)
