extends RefCounted
class_name AreaDef

enum MapType { VERTICAL, ROOM_GRID, OVERWORLD }
enum TransitionStyle { IMMEDIATE, SCROLL, SNAP }
enum InputMode { SIDE_SCROLLING = 0, TOP_DOWN = 1, TOP_DOWN_SHOOTER = 2 }

var id: StringName = &""
var map_type: int = MapType.VERTICAL
var transition_style: int = TransitionStyle.IMMEDIATE
var input_mode: int = InputMode.SIDE_SCROLLING
var starting_room: String = ""

static func make(
	p_id: StringName,
	p_map_type: int,
	p_transition_style: int,
	p_input_mode: int,
	p_starting_room: String
) -> AreaDef:
	var d := AreaDef.new()
	d.id = p_id
	d.map_type = p_map_type
	d.transition_style = p_transition_style
	d.input_mode = p_input_mode
	d.starting_room = p_starting_room
	return d
