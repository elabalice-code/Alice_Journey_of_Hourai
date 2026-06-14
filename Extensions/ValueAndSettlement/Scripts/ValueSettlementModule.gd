extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysModule.gd"

const SAVE_KEY_VALUES := "_ValueAndSettlement_values"
const ValueSettlementActor = preload("res://CoreEngine/Scripts/Actor/ValueSettlementActor.gd")

var _workbench
var _actor: ValueSettlementActor

func _initialize():
	_workbench = WorkbenchService.get_singleton()
	_actor = ValueSettlementActor.new(game, _workbench)
	_actor.set_initial_collectibles(float(game.collectibles))

func _get_save_data() -> Dictionary:
	return {
		SAVE_KEY_VALUES: _actor.to_values_dict(),
	}

func _set_save_data(data: Dictionary):
	if data.has(SAVE_KEY_VALUES):
		_actor.from_values_dict(data[SAVE_KEY_VALUES])
