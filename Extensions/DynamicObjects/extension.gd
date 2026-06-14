extends RefCounted

func get_id() -> StringName:
	return &"dynamic_objects"

func get_order() -> int:
	return 50

func get_metsys_modules() -> Array[String]:
	return [
		"Scripts/DynamicObjectModule.gd"
	]
