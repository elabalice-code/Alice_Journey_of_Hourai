extends RefCounted
class_name RuntimeEventStateExecutor

const MessageTypes = preload("res://CoreEngine/Scripts/Contract/MessageTypes.gd")

const KEY_RUNTIME_EVENT_STATE: StringName = &"runtime_event_state"

var _workbench: WorkbenchService

func _init(p_workbench: WorkbenchService) -> void:
	_workbench = p_workbench

func execute_intent(intent: RuntimeEventStateIntent) -> void:
	if _workbench == null or intent == null or not intent.is_valid():
		return
	match intent.kind:
		RuntimeEventStateIntent.KIND_MARK_FIRED:
			var state := _workbench.get_workplace_data(KEY_RUNTIME_EVENT_STATE, {}) as Dictionary
			state[intent.event_id] = intent.state
			_workbench.register_workplace_data(KEY_RUNTIME_EVENT_STATE, state)
			_workbench.send({
				"type": MessageTypes.TYPE_RUNTIME_EVENT_ACTION,
				"event_id": intent.event_id
			})
