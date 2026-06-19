extends RefCounted
class_name WorkPlace

const PlayerDataScript = preload("res://CoreEngine/Scripts/Data/PlayerData.gd")
const CombatDataScript = preload("res://CoreEngine/Scripts/Data/CombatData.gd")
const InventoryDataScript = preload("res://CoreEngine/Scripts/Data/InventoryData.gd")
const QuestDataScript = preload("res://CoreEngine/Scripts/Data/QuestData.gd")

var type: StringName = &""
var payload: Dictionary = {}
var meta: Dictionary = {}
var player: PlayerData = PlayerDataScript.new()
var combat: CombatData = CombatDataScript.new()
var inventory: InventoryData = InventoryDataScript.new()
var quest: QuestData = QuestDataScript.new()
var data: Dictionary = {}

func set_data(key: StringName, value: Variant) -> void:
	data[key] = value

func get_data(key: StringName, default_value: Variant = null) -> Variant:
	return data.get(key, default_value)

func has_data(key: StringName) -> bool:
	return data.has(key)

func remove_data(key: StringName) -> void:
	data.erase(key)
