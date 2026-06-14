extends Node
class_name ExtensionRegistryService

const EXTENSIONS_ROOT := "res://Extensions"

var _entries: Array[Dictionary] = []

static func get_singleton() -> ExtensionRegistryService:
	return (Engine.get_main_loop() as SceneTree).root.get_node_or_null(^"Extensions") as ExtensionRegistryService

func _ready() -> void:
	reload()

func reload() -> void:
	_entries.clear()
	
	if not DirAccess.dir_exists_absolute(EXTENSIONS_ROOT):
		return
	
	for dir_name in DirAccess.get_directories_at(EXTENSIONS_ROOT):
		var extension_script_path := EXTENSIONS_ROOT.path_join(dir_name).path_join("extension.gd")
		if not ResourceLoader.exists(extension_script_path):
			continue
		
		var extension_script := load(extension_script_path) as Script
		if not extension_script:
			continue
		var extension_instance: Object = extension_script.new()
		var id: StringName = StringName(dir_name)
		var order := 0
		var modules: Array[String] = []
		
		if extension_instance.has_method("get_id"):
			id = extension_instance.get_id()
		if extension_instance.has_method("get_order"):
			order = int(extension_instance.get_order())
		if extension_instance.has_method("get_metsys_modules"):
			modules.assign(extension_instance.get_metsys_modules())
		
		var entry := {
			"id": id,
			"order": order,
			"dir": dir_name,
			"instance": extension_instance,
			"modules": modules,
		}
		_entries.append(entry)
	
	_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return int(a["order"]) < int(b["order"]))
	
	var workbench := WorkbenchService.get_singleton()
	for entry in _entries:
		var inst: Object = entry["instance"]
		if inst and inst.has_method("setup"):
			inst.setup(workbench)

func get_entries() -> Array[Dictionary]:
	return _entries.duplicate(true)

func install_into_game(game: Node) -> void:
	var installed: Dictionary = {}
	for entry in _entries:
		var dir_name: String = entry["dir"]
		for module_path in entry["modules"]:
			var resolved := _resolve_module_path(dir_name, module_path)
			if installed.has(resolved):
				continue
			installed[resolved] = true
			if game.has_method("add_module"):
				game.add_module(resolved)

func _resolve_module_path(dir_name: String, module_path: String) -> String:
	if module_path.is_absolute_path():
		return module_path
	return EXTENSIONS_ROOT.path_join(dir_name).path_join(module_path)
