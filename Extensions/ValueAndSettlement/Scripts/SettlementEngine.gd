extends RefCounted
class_name SettlementEngine

var store
var workbench

var _pending: Array[Dictionary] = []
var _stages: Array[Object] = []

func _init(p_store, p_workbench) -> void:
	store = p_store
	workbench = p_workbench

func register_stage(stage: Object) -> void:
	_stages.append(stage)

func enqueue(tx: Dictionary) -> void:
	_pending.append(tx)

func flush() -> void:
	if _pending.is_empty():
		return
	
	var batch := _pending
	_pending = []
	
	for tx in batch:
		var current_tx: Dictionary = tx
		for stage in _stages:
			if stage and stage.has_method("process"):
				current_tx = stage.process(current_tx, store)
				if current_tx == null:
					current_tx = {}
			if bool(current_tx.get("cancelled", false)):
				break
		
		if bool(current_tx.get("cancelled", false)):
			workbench.send({
				"type": &"value_tx_cancelled",
				"tx": tx,
				"result": current_tx
			})
			continue
		
		var key: StringName = current_tx.get("key", &"")
		var delta: float = float(current_tx.get("delta", 0.0))
		var context: Dictionary = current_tx.get("context", {})
		
		var before: float = float(store.get_value(key))
		var after: float = float(store.apply_delta(key, delta, context))
		
		workbench.send({
			"type": &"value_tx_applied",
			"key": key,
			"before": before,
			"after": after,
			"delta": after - before,
			"context": context
		})
