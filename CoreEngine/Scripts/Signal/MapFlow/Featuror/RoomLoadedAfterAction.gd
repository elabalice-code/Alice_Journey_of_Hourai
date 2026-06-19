extends RefCounted
class_name RoomLoadedAfterAction

const RoomFlowIntentScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/RoomFlowIntent.gd")

static func build_intents(after: Dictionary, map_facts: Dictionary, has_player: bool) -> Array[RoomFlowIntent]:
	var intents: Array[RoomFlowIntent] = []
	if after.is_empty():
		return intents
	var action: StringName = after.get("action", &"")
	match action:
		&"move_player_to_node":
			var node_name: StringName = after.get("node", &"")
			var position: Variant = resolve_node_position(map_facts, node_name)
			if position is Vector2:
				intents.append(RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_MOVE_PLAYER_TO_POSITION, {
					"position": position
				}))
			else:
				_append_matching_portal_intent(intents, after, map_facts)
		&"move_player_to_matching_portal":
			_append_matching_portal_intent(intents, after, map_facts)
		&"call_map_node_method":
			var node_name2: StringName = after.get("node", &"")
			var method_name: StringName = after.get("method", &"")
			if node_name2 != &"" and method_name != &"":
				intents.append(RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_CALL_MAP_NODE_METHOD, {
					"node": node_name2,
					"method": method_name
				}))
	if after.has("clear_player_event_after_sec") and has_player:
		intents.append(RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_CLEAR_PLAYER_EVENT, {
			"delay_sec": float(after.get("clear_player_event_after_sec", 0.0))
		}))
	return intents

static func _append_matching_portal_intent(intents: Array[RoomFlowIntent], after: Dictionary, map_facts: Dictionary) -> void:
	var position: Variant = resolve_matching_portal_position(map_facts, after)
	if position is Vector2:
		intents.append(RoomFlowIntentScript.make(RoomFlowIntentScript.KIND_MOVE_PLAYER_TO_POSITION, {
			"position": position
		}))

static func resolve_node_position(map_facts: Dictionary, node_name: StringName) -> Variant:
	if node_name == &"":
		return null
	var node_positions: Dictionary = map_facts.get("node_positions", {}) as Dictionary
	return node_positions.get(String(node_name), null)

static func resolve_matching_portal_position(map_facts: Dictionary, after: Dictionary) -> Variant:
	var from_room := str(after.get("from_room", ""))
	var fallback_node: StringName = after.get("fallback_node", &"") as StringName
	var portal_positions_by_target_map: Dictionary = map_facts.get("portal_positions_by_target_map", {}) as Dictionary
	if not from_room.is_empty():
		var portal_position: Variant = portal_positions_by_target_map.get(from_room, null)
		if portal_position is Vector2:
			return portal_position
	if fallback_node != &"":
		return resolve_node_position(map_facts, fallback_node)
	return null
