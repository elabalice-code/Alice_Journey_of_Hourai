extends RefCounted
class_name ConditionExpr

static func eval_equals_expr(expr: String, context: Dictionary) -> bool:
	if expr.is_empty():
		return true
	var parts := expr.split("==")
	if parts.size() != 2:
		return true
	var key := parts[0].strip_edges()
	var rhs := parts[1].strip_edges()
	if rhs.begins_with("\"") and rhs.ends_with("\"") and rhs.length() >= 2:
		rhs = rhs.substr(1, rhs.length() - 2)
	return str(context.get(key, "")) == rhs
