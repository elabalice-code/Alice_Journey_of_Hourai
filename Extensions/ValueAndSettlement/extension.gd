extends RefCounted

func get_id() -> StringName:
	return &"value_and_settlement"

func get_order() -> int:
	return 100

func get_metsys_modules() -> Array[String]:
	return [
		"Scripts/ValueSettlementModule.gd",
	]
