extends RefCounted
class_name MapAreaStateActor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const AreaCatalog = preload("res://CoreEngine/Scripts/World/AreaCatalog.gd")
const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")
const MapInputModeNameScript = preload("res://CoreEngine/Scripts/Helper/Map/InputModeName.gd")

const KEY_CURRENT_AREA_ID: StringName = &"current_area_id"
const KEY_MAP_TYPE: StringName = &"current_map_type"
const KEY_TRANSITION_STYLE: StringName = &"current_transition_style"
const KEY_INPUT_MODE: StringName = &"current_input_mode"

static func apply_defaults(workbench: WorkbenchService) -> Dictionary:
	if workbench == null:
		return {}
	var saved_area_id := workbench.get_workplace_data(KEY_CURRENT_AREA_ID, &"") as StringName
	if saved_area_id != &"":
		return apply_area(workbench, saved_area_id, false)
	return apply_initial_area(workbench)

static func apply_initial_area(workbench: WorkbenchService) -> Dictionary:
	return apply_area(workbench, AreaCatalog.get_initial_area_id(), true)

static func apply_area(workbench: WorkbenchService, area_id: StringName, also_set_starting_room: bool) -> Dictionary:
	if workbench == null:
		return {}
	var def := AreaCatalog.get_area_def(area_id) as AreaDef
	if def == null:
		return {}
	workbench.register_workplace_data(KEY_CURRENT_AREA_ID, area_id)
	workbench.register_workplace_data(KEY_TRANSITION_STYLE, def.transition_style)
	workbench.register_workplace_data(KEY_MAP_TYPE, def.map_type)
	workbench.register_workplace_data(KEY_INPUT_MODE, def.input_mode)
	workbench.send({
		"type": MessageTypes.TYPE_INPUT_MODE_CHANGE_REQUEST,
		"mode": MapInputModeNameScript.from_area_input_mode(def.input_mode)
	})
	return {
		"area_id": area_id,
		"starting_room": def.starting_room if also_set_starting_room else "",
	}

static func current_area_id(workbench: WorkbenchService) -> StringName:
	if workbench == null:
		return &""
	return workbench.get_workplace_data(KEY_CURRENT_AREA_ID, &"") as StringName
