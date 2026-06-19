extends RefCounted
class_name RuntimeEventDiagnostics

static func project_summary(project: Dictionary, indexed_events: Dictionary) -> Dictionary:
	var events = project.get("events", [])
	var event_count := events.size() if events is Array else 0
	return {
		"name": str(project.get("name", "")),
		"version": str(project.get("version", "")),
		"start_event_id": str(project.get("startEventId", "")),
		"event_count": event_count,
		"indexed_event_count": indexed_events.size(),
	}

static func frame_summary(frame: RuntimeEventSignalFrame) -> Dictionary:
	if frame == null:
		return {"valid": false}
	return {
		"valid": frame.is_valid(),
		"signal": frame.signal_name,
		"source_domain": frame.source_domain,
		"source_type": frame.source_type,
	}
