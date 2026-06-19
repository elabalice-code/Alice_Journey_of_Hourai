extends RefCounted
class_name RuntimeEventTriggerMatch

const ConditionExprScript = preload("res://CoreEngine/Scripts/Helper/Expression/ConditionExpr.gd")

static func matches_event(event_dict: Dictionary, frame: RuntimeEventSignalFrame) -> bool:
	if frame == null or not frame.is_valid():
		return false
	var triggers = event_dict.get("triggers", [])
	if not (triggers is Array):
		return false
	for t in triggers:
		if not (t is Dictionary):
			continue
		if matches_trigger(t as Dictionary, frame):
			return true
	return false

static func matches_trigger(trigger: Dictionary, frame: RuntimeEventSignalFrame) -> bool:
	var expected_signal := str(trigger.get("signal", "")).strip_edges()
	if not expected_signal.is_empty() and expected_signal != frame.signal_name:
		return false
	var expected_domain := str(trigger.get("sourceDomain", "")).strip_edges().to_lower()
	if not expected_domain.is_empty() and expected_domain != frame.source_domain.to_lower():
		return false
	return ConditionExprScript.eval_equals_expr(str(trigger.get("conditionExpr", "")).strip_edges(), frame.context)
