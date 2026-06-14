extends Node
class_name AreaCatalog

const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")
const INITIAL_AREA_ID: StringName = &"story_dungeon_rooms"
static var _defs_cache: Dictionary

static func get_area_ids_in_order() -> Array[StringName]:
	return [
		&"story_vertical",
		&"story_starsky",
		&"story_dungeon_rooms",
		&"story_overworld",
	]

static func get_initial_area_id() -> StringName:
	return INITIAL_AREA_ID

static func get_area_defs() -> Dictionary:
	if not _defs_cache.is_empty():
		return _defs_cache
	_defs_cache = {
		&"story_vertical": AreaDef.make(
			&"story_vertical",
			AreaDef.MapType.VERTICAL,
			AreaDef.TransitionStyle.SCROLL,
			AreaDef.InputMode.SIDE_SCROLLING,
			"res://CoreEngine/Maps/StartingPoint.tscn"
		),
		&"story_starsky": AreaDef.make(
			&"story_starsky",
			AreaDef.MapType.VERTICAL,
			AreaDef.TransitionStyle.SCROLL,
			AreaDef.InputMode.SIDE_SCROLLING,
			"res://CoreEngine/Maps/map_starsky.tscn"
		),
		&"story_dungeon_rooms": AreaDef.make(
			&"story_dungeon_rooms",
			AreaDef.MapType.ROOM_GRID,
			AreaDef.TransitionStyle.SNAP,
			AreaDef.InputMode.TOP_DOWN_SHOOTER,
			"res://CoreEngine/Maps/DiceRoom.tscn"
		),
		&"story_overworld": AreaDef.make(
			&"story_overworld",
			AreaDef.MapType.OVERWORLD,
			AreaDef.TransitionStyle.IMMEDIATE,
			AreaDef.InputMode.TOP_DOWN,
			"res://CoreEngine/Maps/PortalRoom.tscn"
		),
	}
	return _defs_cache

static func get_area_def(area_id: StringName) -> AreaDef:
	var defs := get_area_defs()
	if defs.has(area_id):
		return defs[area_id] as AreaDef
	return null
