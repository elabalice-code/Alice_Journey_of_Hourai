extends RefCounted
class_name ActorFramework

const TYPE_ANY: StringName = &"*"
const TYPE_LEVEL_EVENT_REQUEST: StringName = &"level_event_request"
const TYPE_LEVEL_EVENT: StringName = &"level_event"
const TYPE_PLAYER_DATA_CHANGED: StringName = &"player_data_changed"
const TYPE_SAVE_REQUEST: StringName = &"save_request"
const TYPE_SAVE_COMPLETED: StringName = &"save_completed"
const TYPE_LOAD_ROOM_REQUEST: StringName = &"load_room_request"
const TYPE_LOAD_AREA_REQUEST: StringName = &"load_area_request"
const TYPE_RESET_MAP_STARTING_COORDS_REQUEST: StringName = &"reset_map_starting_coords_request"
const TYPE_SHIFT_PLAYER_REQUEST: StringName = &"shift_player_request"
const TYPE_SET_LOOP_TARGET: StringName = &"set_loop_target"
const TYPE_CLEAR_LOOP_TARGET: StringName = &"clear_loop_target"
const TYPE_BATTLE_RESULT_REQUEST: StringName = &"battle_result_request"
const TYPE_ROOM_LOADED: StringName = &"room_loaded"
const TYPE_AREA_LOADED: StringName = &"area_loaded"

const TYPE_INPUT_MODE_CHANGE_REQUEST: StringName = &"input_mode_change_request"
const TYPE_INPUT_MODE_CHANGED: StringName = &"input_mode_changed"

const TYPE_APPLY_DAMAGE_REQUEST: StringName = &"apply_damage_request"
const TYPE_DAMAGE_APPLIED: StringName = &"damage_applied"
const TYPE_COMBAT_SYNC_REQUEST: StringName = &"combat_sync_request"
const TYPE_COMBAT_STATE_CHANGED: StringName = &"combat_state_changed"

const TYPE_ITEM_ACTION_REQUEST: StringName = &"item_action_request"
const TYPE_INVENTORY_UPDATED: StringName = &"inventory_updated"

const TYPE_QUEST_ACTION_REQUEST: StringName = &"quest_action_request"
const TYPE_QUEST_UPDATED: StringName = &"quest_updated"

const TYPE_AUDIO_REQUEST: StringName = &"audio_request"

const TYPE_ADD_GENERATED_ROOM: StringName = &"add_generated_room"
const TYPE_CLEAR_GENERATED_ROOMS: StringName = &"clear_generated_rooms"
const TYPE_RUNTIME_EVENT_SIGNAL: StringName = &"runtime_event_signal"
const TYPE_RUNTIME_EVENT_START: StringName = &"runtime_event_start"
const TYPE_RUNTIME_EVENT_ACTION: StringName = &"runtime_event_action"

class PlayerData extends RefCounted:
	var base_speed_min: float = 300.0
	var base_speed_max: float = 400.0
	var base_jump_velocity: float = -450.0
	
	var speed_min: float = base_speed_min
	var speed_max: float = base_speed_max
	var jump_velocity: float = base_jump_velocity
	
	func reset_to_base() -> void:
		speed_min = base_speed_min
		speed_max = base_speed_max
		jump_velocity = base_jump_velocity
	
	func apply_multiplier(multiplier: float) -> void:
		speed_min = base_speed_min * multiplier
		speed_max = base_speed_max * multiplier
		jump_velocity = base_jump_velocity * multiplier

class CombatData extends RefCounted:
	var hp: float = 0.0
	var max_hp: float = 0.0
	var defense: float = 0.0
	var attack_power: float = 0.0
	var status_effects: Array[StringName] = []

class InventoryData extends RefCounted:
	var bag: Array[ItemDef] = []
	var equipped: Dictionary = {}
	var runes: Array[ItemDef] = []

class QuestData extends RefCounted:
	var active_quests: Dictionary = {}
	var completed_quests: Array[StringName] = []
	var dialogue_history: Array[String] = []

class WorkPlace extends RefCounted:
	var type: StringName = &""
	var payload: Dictionary = {}
	var meta: Dictionary = {}
	var player: PlayerData = PlayerData.new()
	var combat: CombatData = CombatData.new()
	var inventory: InventoryData = InventoryData.new()
	var quest: QuestData = QuestData.new()
	var data: Dictionary = {}
	
	func set_data(key: StringName, value: Variant) -> void:
		data[key] = value
	
	func get_data(key: StringName, default_value: Variant = null) -> Variant:
		return data.get(key, default_value)
	
	func has_data(key: StringName) -> bool:
		return data.has(key)
	
	func remove_data(key: StringName) -> void:
		data.erase(key)

class NotifiableObject extends RefCounted:
	signal notifications(workplace)
	func notify(workplace) -> void:
		notifications.emit(workplace)

class Actor extends RefCounted:
	var is_busy: bool = false
	var notifier
	var actor_thread: Thread
	var work_as_thread: bool = false
	
	func _init(p_notifier) -> void:
		notifier = p_notifier
		if notifier and notifier.has_signal("notifications"):
			notifier.notifications.connect(_process_notification)
	
	func send_message(workplace) -> void:
		if notifier and notifier.has_method("notify"):
			notifier.notify(workplace)
	
	func _process_notification(workplace) -> void:
		if is_busy:
			return
		is_busy = true
		if not work_as_thread:
			process_thread(workplace)
			is_busy = false
			return
		actor_thread = Thread.new()
		actor_thread.start(_thread_entry.bind(workplace))
	
	func _thread_entry(workplace) -> void:
		process_thread(workplace)
		is_busy = false
	
	func process_thread(_workplace) -> void:
		pass
	
	func have_a_rest() -> void:
		if actor_thread and actor_thread.is_started():
			actor_thread.wait_to_finish()

class Speaker extends RefCounted:
	var actors: Array = []
	func add_actor(actor_notifiable) -> void:
		actors.append(actor_notifiable)
	func broadcast(workplace) -> void:
		for a in actors:
			if a and a.has_method("notify"):
				a.notify(workplace)

class ActorSpeaker extends RefCounted:
	var mediator: Speaker
	func _init(p_mediator: Speaker) -> void:
		mediator = p_mediator
	func broadcast_notification(workplace) -> void:
		if mediator:
			mediator.broadcast(workplace)
