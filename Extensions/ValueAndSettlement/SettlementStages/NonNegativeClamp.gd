extends RefCounted

func process(tx: Dictionary, store) -> Dictionary:
	var key: StringName = tx.get("key", &"")
	var delta: float = float(tx.get("delta", 0.0))
	var min_value: float = float(tx.get("min_value", 0.0))
	
	if bool(tx.get("allow_below_min", false)):
		return tx
	
	var before: float = float(store.get_value(key))
	var after: float = before + delta
	if after >= min_value:
		return tx
	
	var on_insufficient: String = str(tx.get("on_insufficient", "cancel"))
	match on_insufficient:
		"clamp":
			tx["delta"] = min_value - before
		_:
			tx["cancelled"] = true
			tx["reason"] = "insufficient"
			tx["before"] = before
			tx["min_value"] = min_value
	return tx
