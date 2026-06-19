extends RefCounted
class_name AreaFlowRouter

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")
const AreaLoadPlanScript = preload("res://CoreEngine/Scripts/Signal/MapFlow/Featuror/AreaLoadPlan.gd")

static func route(frame: AreaFlowSignalFrame) -> Array[AreaFlowIntent]:
	var intents: Array[AreaFlowIntent] = []
	if frame == null or not frame.is_valid():
		return intents
	match frame.source_type:
		MessageTypes.TYPE_LOAD_AREA_REQUEST:
			return AreaLoadPlanScript.build_load_area_intents(frame.payload)
		_:
			return intents

static func route_area_loaded(pending_area_loaded: Dictionary) -> Array[AreaFlowIntent]:
	return AreaLoadPlanScript.build_area_loaded_intents(pending_area_loaded)
