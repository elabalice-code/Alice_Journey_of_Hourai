extends RefCounted
class_name MessageTypes

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
