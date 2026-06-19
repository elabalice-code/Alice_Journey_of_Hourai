extends RefCounted
class_name RoomPropertyPlan

const RoomPropertyCatalog = preload("res://CoreEngine/Scripts/World/RoomPropertyCatalog.gd")
const MapPropertyIntentScript = preload("res://CoreEngine/Scripts/Signal/MapPropertyFlow/MapPropertyIntent.gd")
const RoomPathKeyScript = preload("res://CoreEngine/Scripts/Helper/MapProperty/RoomPathKey.gd")

static func build_room_loaded_intents(msg: Dictionary) -> Array[MapPropertyIntent]:
	var intents: Array[MapPropertyIntent] = []
	var room_path: String = str(msg.get("room_path", ""))
	if room_path.is_empty():
		return intents
	var key: String = RoomPathKeyScript.normalize(room_path)
	var all: Dictionary = RoomPropertyCatalog.get_room_properties()
	var entry: Dictionary = all.get(key, {}) as Dictionary
	if entry.is_empty():
		return intents
	var ops: Array = entry.get("ops", []) as Array
	for raw in ops:
		if not (raw is Dictionary):
			continue
		var intent: MapPropertyIntent = build_op_intent(raw as Dictionary)
		if intent.is_valid():
			intents.append(intent)
	return intents

static func build_op_intent(op: Dictionary) -> MapPropertyIntent:
	var op_name := StringName(str(op.get("op", "")))
	match op_name:
		MapPropertyIntentScript.KIND_SET_PROPS:
			return MapPropertyIntentScript.make(MapPropertyIntentScript.KIND_SET_PROPS, op)
		MapPropertyIntentScript.KIND_SET_RESOURCES:
			return MapPropertyIntentScript.make(MapPropertyIntentScript.KIND_SET_RESOURCES, op)
		MapPropertyIntentScript.KIND_SET_SHAPE:
			return MapPropertyIntentScript.make(MapPropertyIntentScript.KIND_SET_SHAPE, op)
		MapPropertyIntentScript.KIND_REPLACE_SOURCE_ID:
			return MapPropertyIntentScript.make(MapPropertyIntentScript.KIND_REPLACE_SOURCE_ID, op)
		MapPropertyIntentScript.KIND_REPLACE_TILE:
			return MapPropertyIntentScript.make(MapPropertyIntentScript.KIND_REPLACE_TILE, op)
	return MapPropertyIntentScript.make(MapPropertyIntentScript.KIND_NONE)
