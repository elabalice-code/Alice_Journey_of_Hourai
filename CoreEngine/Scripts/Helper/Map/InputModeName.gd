extends RefCounted
class_name MapInputModeName

const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")

static func from_area_input_mode(input_mode: int) -> StringName:
	match input_mode:
		AreaDef.InputMode.TOP_DOWN:
			return &"top_down"
		AreaDef.InputMode.TOP_DOWN_SHOOTER:
			return &"top_down_shooter"
		_:
			return &"side_scrolling"
