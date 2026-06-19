extends RefCounted
class_name RoomPropertyManagerActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const MapPropertyProducersScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertyProducers.gd")
const MapPropertyRouterScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertyRouter.gd")

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench != null:
		_workbench.register_actor(self, [MessageTypes.TYPE_ROOM_LOADED], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	
	var game := _workbench.get_service(&"game") as Game
	if game == null or game.map == null:
		return
	
	var frame: MapPropertySignalFrame = MapPropertyProducersScript.from_workplace(workplace)
	var intents: Array[MapPropertyIntent] = MapPropertyRouterScript.route(frame)
	for intent in intents:
		_execute_intent(game.map, intent)

func _execute_intent(map_root: Node, intent: MapPropertyIntent) -> void:
	if intent == null or not intent.is_valid():
		return
	match intent.kind:
		MapPropertyIntent.KIND_SET_PROPS:
			_op_set_props(map_root, intent.payload)
		MapPropertyIntent.KIND_SET_RESOURCES:
			_op_set_resources(map_root, intent.payload)
		MapPropertyIntent.KIND_SET_SHAPE:
			_op_set_shape(map_root, intent.payload)
		MapPropertyIntent.KIND_REPLACE_SOURCE_ID:
			_op_replace_source_id(map_root, intent.payload)
		MapPropertyIntent.KIND_REPLACE_TILE:
			_op_replace_tile(map_root, intent.payload)

func _get_target_node(map_root: Node, op: Dictionary) -> Node:
	var node_path := str(op.get("path", ""))
	if node_path.is_empty():
		return null
	if node_path == ".":
		return map_root
	return map_root.get_node_or_null(NodePath(node_path))

func _node_has_property(obj: Object, prop: StringName) -> bool:
	if obj == null:
		return false
	for p in obj.get_property_list():
		if StringName(p.get("name", "")) == prop:
			return true
	return false

func _op_set_props(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if target == null:
		return
	var props := op.get("props", {}) as Dictionary
	for k in props.keys():
		var prop := StringName(str(k))
		if not _node_has_property(target, prop):
			continue
		target.set(prop, props[k])

func _op_set_resources(map_root: Node, op: Dictionary) -> void:
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

func _op_set_shape(map_root: Node, op: Dictionary) -> void:
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

func _op_replace_source_id(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if target == null:
		return
	if not target.has_method(&"get_used_cells") or not target.has_method(&"get_cell_source_id") or not target.has_method(&"set_cell"):
		return
	
	var from_id := int(op.get("from", -1))
	var to_id := int(op.get("to", -1))
	if from_id < 0 or to_id < 0:
		return
	
	var used: Array = target.call(&"get_used_cells") as Array
	for raw_cell in used:
		if not (raw_cell is Vector2i):
			continue
		var cell := raw_cell as Vector2i
		var src := int(target.call(&"get_cell_source_id", cell))
		if src != from_id:
			continue
		
		var atlas := Vector2i.ZERO
		var alt := 0
		if target.has_method(&"get_cell_atlas_coords"):
			atlas = target.call(&"get_cell_atlas_coords", cell) as Vector2i
		if target.has_method(&"get_cell_alternative_tile"):
			alt = int(target.call(&"get_cell_alternative_tile", cell))
		
		target.call(&"set_cell", cell, to_id, atlas, alt)

func _op_replace_tile(map_root: Node, op: Dictionary) -> void:
	var target := _get_target_node(map_root, op)
	if target == null:
		return
	if not target.has_method(&"get_used_cells") or not target.has_method(&"get_cell_source_id") or not target.has_method(&"set_cell"):
		return
	
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
	
	var used: Array = target.call(&"get_used_cells") as Array
	for raw_cell in used:
		if not (raw_cell is Vector2i):
			continue
		var cell := raw_cell as Vector2i
		var src := int(target.call(&"get_cell_source_id", cell))
		if src != from_src:
			continue
		
		var atlas := Vector2i.ZERO
		var alt := 0
		if target.has_method(&"get_cell_atlas_coords"):
			atlas = target.call(&"get_cell_atlas_coords", cell) as Vector2i
		if target.has_method(&"get_cell_alternative_tile"):
			alt = int(target.call(&"get_cell_alternative_tile", cell))
		
		if atlas != from_atlas or alt != from_alt:
			continue
		
		target.call(&"set_cell", cell, to_src, to_atlas, to_alt)
