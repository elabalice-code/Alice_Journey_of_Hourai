extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysModule.gd"

func _initialize():
	var registry := ExtensionRegistryService.get_singleton()
	if registry:
		registry.install_into_game(game)
