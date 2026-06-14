extends RefCounted
class_name AreaFlowActor

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")
const AreaCatalog = preload("res://CoreEngine/Scripts/World/AreaCatalog.gd")
const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")

const KEY_CURRENT_AREA_ID: StringName = &"current_area_id"
const KEY_TRANSITION_STYLE: StringName = &"current_transition_style"
const KEY_MAP_TYPE: StringName = &"current_map_type"
const KEY_INPUT_MODE: StringName = &"current_input_mode"

var _workbench: WorkbenchService
var _pending_area_loaded: Dictionary = {}

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench
	if _workbench:
		_workbench.register_actor(self, [
			ActorFramework.TYPE_LOAD_AREA_REQUEST,
			ActorFramework.TYPE_ROOM_LOADED,
		], &"_on_workplace")

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	match t:
		ActorFramework.TYPE_LOAD_AREA_REQUEST:
			_apply_load_area_request(msg)
		ActorFramework.TYPE_ROOM_LOADED:
			_apply_room_loaded()

func _apply_load_area_request(msg: Dictionary) -> void:
	var area_id := msg.get("area_id", &"") as StringName
	if area_id == &"":
		return
	var def := AreaCatalog.get_area_def(area_id) as AreaDef
	if def == null:
		return
	var entry_room := str(msg.get("entry_room", ""))
	if entry_room.is_empty():
		entry_room = def.starting_room
	if entry_room.is_empty():
		return
	_pending_area_loaded = {
		"area_id": area_id,
		"entry_room": entry_room,
	}
	_workbench.register_workplace_data(KEY_CURRENT_AREA_ID, area_id)
	_workbench.register_workplace_data(KEY_TRANSITION_STYLE, def.transition_style)
	_workbench.register_workplace_data(KEY_MAP_TYPE, def.map_type)
	_workbench.register_workplace_data(KEY_INPUT_MODE, def.input_mode)
	_workbench.send({
		"type": ActorFramework.TYPE_INPUT_MODE_CHANGE_REQUEST,
		"mode": _to_input_mode_name(def.input_mode)
	})
	var room_req := {
		"type": ActorFramework.TYPE_LOAD_ROOM_REQUEST,
		"target_map": entry_room
	}
	if msg.get("after", null) is Dictionary:
		room_req["after"] = msg.get("after", {}) as Dictionary
	_workbench.send(room_req)
	_workbench.send({
		"type": ActorFramework.TYPE_RESET_MAP_STARTING_COORDS_REQUEST
	})

func _apply_room_loaded() -> void:
	if _pending_area_loaded.is_empty():
		return
	var payload := _pending_area_loaded
	_pending_area_loaded = {}
	_workbench.send({
		"type": ActorFramework.TYPE_AREA_LOADED,
		"area_id": payload.get("area_id", &""),
		"entry_room": payload.get("entry_room", ""),
	})

func _to_input_mode_name(input_mode: int) -> StringName:
	match input_mode:
		AreaDef.InputMode.TOP_DOWN:
			return &"top_down"
		AreaDef.InputMode.TOP_DOWN_SHOOTER:
			return &"top_down_shooter"
		_:
			return &"side_scrolling"
