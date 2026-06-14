extends RefCounted
class_name ValueSettlementActor

const ValueStore = preload("res://Extensions/ValueAndSettlement/Scripts/ValueStore.gd")
const SettlementEngine = preload("res://Extensions/ValueAndSettlement/Scripts/SettlementEngine.gd")

var _game
var _workbench: WorkbenchService
var _store
var _engine

func _init(p_game, p_workbench: WorkbenchService) -> void:
	_game = p_game
	_workbench = p_workbench
	_store = ValueStore.new()
	_engine = SettlementEngine.new(_store, _workbench)
	
	_workbench.register_service(&"values", _store)
	_workbench.register_service(&"settlement", _engine)
	
	_register_stages()
	
	_workbench.register_actor(self, [ &"value_tx", &"value_set", &"value_sync" ], &"_on_workplace")
	_workbench.tick.connect(_on_tick)
	_store.value_changed.connect(_on_value_changed)

func set_initial_collectibles(value: float) -> void:
	_store.set_value(&"collectibles", value, {"origin": &"init"})

func to_values_dict() -> Dictionary:
	return _store.to_dict()

func from_values_dict(values: Dictionary) -> void:
	_store.from_dict(values)
	_sync_collectibles_to_game()

func _sync_collectibles_to_game() -> void:
	var collectibles := int(_store.get_value(&"collectibles", 0.0))
	if _game != null and _game.collectibles != collectibles:
		_game.collectibles = collectibles

func _register_stages() -> void:
	var root := "res://Extensions"
	if not DirAccess.dir_exists_absolute(root):
		return
	
	for dir_name in DirAccess.get_directories_at(root):
		var stage_dir := root.path_join(dir_name).path_join("SettlementStages")
		if not DirAccess.dir_exists_absolute(stage_dir):
			continue
		
		var files := DirAccess.get_files_at(stage_dir)
		files.sort()
		for file in files:
			if file.get_extension() != "gd":
				continue
			var stage_script := load(stage_dir.path_join(file)) as Script
			if not stage_script:
				continue
			var stage: Object = stage_script.new()
			_engine.register_stage(stage)

func _on_tick(_delta: float) -> void:
	_engine.flush()

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var message: Dictionary = workplace.payload
	var type: StringName = workplace.type
	match type:
		&"value_tx":
			_engine.enqueue(_normalize_tx(message))
		&"value_set", &"value_sync":
			var key: StringName = message.get("key", &"")
			var value: float = float(message.get("value", 0.0))
			var context: Dictionary = message.get("context", {})
			_store.set_value(key, value, context)

func _normalize_tx(message: Dictionary) -> Dictionary:
	var tx := message.duplicate(true)
	tx["type"] = &"value_tx"
	tx["key"] = tx.get("key", &"")
	tx["delta"] = float(tx.get("delta", 0.0))
	if not tx.has("context") or not tx["context"] is Dictionary:
		tx["context"] = {}
	return tx

func _on_value_changed(key: StringName, value: float, _delta: float, context: Dictionary) -> void:
	if key != &"collectibles":
		return
	if context.get("origin") == &"Game":
		return
	if _game != null:
		_game.collectibles = int(value)
