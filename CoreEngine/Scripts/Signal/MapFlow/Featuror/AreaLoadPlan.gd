extends RefCounted
class_name AreaLoadPlan

const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")
const AreaCatalog = preload("res://CoreEngine/Scripts/World/AreaCatalog.gd")
const AreaFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/AreaFlowIntent.gd")
const MapInputModeNameScript = preload("res://CoreEngine/Scripts/Helper/Map/InputModeName.gd")

const KEY_CURRENT_AREA_ID: StringName = &"current_area_id"
const KEY_TRANSITION_STYLE: StringName = &"current_transition_style"
const KEY_MAP_TYPE: StringName = &"current_map_type"
const KEY_INPUT_MODE: StringName = &"current_input_mode"

static func build_load_area_intents(msg: Dictionary) -> Array[AreaFlowIntent]:
	var intents: Array[AreaFlowIntent] = []
	var area_id: StringName = msg.get("area_id", &"") as StringName
	if area_id == &"":
		return intents
	var def: AreaDef = AreaCatalog.get_area_def(area_id) as AreaDef
	if def == null:
		return intents
	var entry_room: String = str(msg.get("entry_room", ""))
	if entry_room.is_empty():
		entry_room = def.starting_room
	if entry_room.is_empty():
		return intents
	var pending := {
		"area_id": area_id,
		"entry_room": entry_room,
	}
	intents.append(AreaFlowIntentScript.make(AreaFlowIntentScript.KIND_PREPARE_AREA_LOADED, pending))
	intents.append(AreaFlowIntentScript.make(AreaFlowIntentScript.KIND_SET_AREA_STATE, {
		KEY_CURRENT_AREA_ID: area_id,
		KEY_TRANSITION_STYLE: def.transition_style,
		KEY_MAP_TYPE: def.map_type,
		KEY_INPUT_MODE: def.input_mode,
	}))
	intents.append(AreaFlowIntentScript.make(AreaFlowIntentScript.KIND_REQUEST_INPUT_MODE, {
		"mode": MapInputModeNameScript.from_area_input_mode(def.input_mode)
	}))
	var room_req := {
		"target_map": entry_room
	}
	if msg.get("after", null) is Dictionary:
		room_req["after"] = msg.get("after", {}) as Dictionary
	intents.append(AreaFlowIntentScript.make(AreaFlowIntentScript.KIND_LOAD_ENTRY_ROOM, room_req))
	intents.append(AreaFlowIntentScript.make(AreaFlowIntentScript.KIND_RESET_MAP_STARTING_COORDS))
	return intents

static func build_area_loaded_intents(pending_area_loaded: Dictionary) -> Array[AreaFlowIntent]:
	var intents: Array[AreaFlowIntent] = []
	if pending_area_loaded.is_empty():
		return intents
	intents.append(AreaFlowIntentScript.make(AreaFlowIntentScript.KIND_EMIT_AREA_LOADED, {
		"area_id": pending_area_loaded.get("area_id", &""),
		"entry_room": pending_area_loaded.get("entry_room", ""),
	}))
	return intents
