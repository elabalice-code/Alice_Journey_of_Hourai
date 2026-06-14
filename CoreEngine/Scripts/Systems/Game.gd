# This is the main script of the game. It manages the current map and some other stuff.
extends "res://addons/MetroidvaniaSystem/Template/Scripts/MetSysGame.gd"
class_name Game

const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")
const SaveManager = preload("res://addons/MetroidvaniaSystem/Template/Scripts/SaveManager.gd")
const AliceNPCScene = preload("res://CoreEngine/Objects/AliceNPC.tscn")
const DialogueManagerActor = preload("res://CoreEngine/Scripts/Actor/DialogueManagerActor.gd")
const ProgressData = preload("res://CoreEngine/Scripts/Data/ProgressData.gd")
const AreaCatalog = preload("res://CoreEngine/Scripts/World/AreaCatalog.gd")
const AreaDef = preload("res://CoreEngine/Scripts/World/AreaDef.gd")
const SAVE_PATH = "user://example_save_data.sav"
const DISPLAY_SETTINGS_PATH = "user://display_settings.cfg"
const DISPLAY_SETTINGS_SECTION = "display"
const DISPLAY_SETTINGS_MODE_KEY = "mode"
const DISPLAY_SETTINGS_SIZE_KEY = "window_size"
const DISPLAY_SETTINGS_LANGUAGE_KEY = "language"
const PROLOGUE_PROJECT_ROOT: String = "res://000_UserInput/00_序章"
const PROLOGUE_EXTERNAL_ROOT: String = "D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/0_UserInput/00_序章"
const PROLOGUE_MD_ROOT_REL: String = "00_0-0_魔宫附近"
const RELAUNCH_ARG = "--aoj_relaunch"
const KEY_CURRENT_AREA_ID: StringName = &"current_area_id"
const KEY_MAP_TYPE: StringName = &"current_map_type"
const KEY_TRANSITION_STYLE: StringName = &"current_transition_style"
const KEY_INPUT_MODE: StringName = &"current_input_mode"
const INITIAL_ROOM_PATH: String = "res://CoreEngine/Maps/DiceRoom.tscn"

# The game starts in this map. Uses special annotation that enabled dedicated inspector plugin.
@export_file("room_link") var starting_map: String

# Number of collected collectibles. Setting it also updates the counter.
var collectibles: int:
	set(count):
		collectibles = count
		%CollectibleCount.text = "%d/7" % count
		var workbench := WorkbenchService.get_singleton()
		if workbench:
			workbench.send({
				"type": &"value_sync",
				"key": &"collectibles",
				"value": count,
				"context": {"origin": &"Game"}
			})

# The coordinates of generated rooms. MetSys does not keep this list, so it needs to be done manually.
# For Custom Runner integration.
var custom_run: bool
# See LoopScript.
var loop: String
var _in_random_level: bool = false
var _default_window_size: Vector2i
var _debug_collisions_visible: bool = false
var _bg_perspective_rect: TextureRect
var _bg_perspective_material: ShaderMaterial
var _bg_perspective_upscale: float = 1.0
var _room_entry_player_global_pos: Vector2 = Vector2.ZERO
var _room_entry_set: bool = false
var _fall_out_cooldown_s: float = 0.0
var _language_setting: String = "auto"
var _syncing_language_option: bool = false
var _prologue_root_abs: String = ""
var _prologue_slides: Array[Dictionary] = []
var _prologue_slide_idx: int = 0
var _prologue_running: bool = false
var _prologue_texture_cache: Dictionary = {}

@onready var _title_menu: Control = %TitleMenu
@onready var _continue_button: Button = %ContinueButton
@onready var _start_button: Button = %StartButton
@onready var _settings_button: Button = %SettingsButton
@onready var _quit_button: Button = %QuitButton
@onready var _settings_panel: Control = %SettingsPanel
@onready var _windowed_button: Button = %WindowedButton
@onready var _fullscreen_button: Button = %FullscreenButton
@onready var _back_button: Button = %BackButton
@onready var _restart_confirm: ConfirmationDialog = %RestartConfirmDialog
@onready var _loading_screen: Control = %LoadingScreen
@onready var _loading_label: Label = %LoadingLabel
@onready var _loading_bar: ProgressBar = %LoadingBar
@onready var _language_label: Label = %LanguageLabel
@onready var _language_option: OptionButton = %LanguageOption
@onready var _prologue_screen: Control = %PrologueScreen
@onready var _prologue_image: TextureRect = %Image
@onready var _prologue_caption: Label = %Caption
@onready var _prologue_dialogue_box: Control = %DialogueBox
@onready var _prologue_dialogue_text: Label = %DialogueText
@onready var _prologue_skip_button: Button = %SkipButton
@onready var _prologue_next_button: Button = %NextButton

func _ready() -> void:
	# A trick for static object reference (before static vars were a thing).
	get_script().set_meta(&"singleton", self)
	
	_default_window_size = DisplayServer.window_get_size()
	if _apply_display_settings_on_startup():
		return
	# Make sure MetSys is in initial state.
	# Does not matter in this project, but normally this ensures that the game works correctly when you exit to menu and start again.
	MetSys.reset_state()
	# Assign player for MetSysGame.
	set_player($Player)
	
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.set_service(&"game", self)
		if is_instance_valid(player):
			workbench.set_service(&"player", player)
		workbench.register_actor(self, [ActorFramework.TYPE_BATTLE_RESULT_REQUEST], &"_on_workplace")
	
	add_module("res://CoreEngine/Scripts/Systems/AreaRoomTransitions.gd")
	add_module("res://CoreEngine/Scripts/Core/ExtensionBootstrap.gd")
	_ensure_dialogue_manager()
	
	if FileAccess.file_exists(SAVE_PATH):
		# If save data exists, load it using MetSys SaveManager.
		var save_manager := SaveManager.new()
		save_manager.load_from_text(SAVE_PATH)
		save_manager.retrieve_game(self)
	else:
		# If no data exists, set empty one.
		MetSys.set_save_data()
	
	_apply_area_defaults()
	
	# Initialize room when it changes.
	room_loaded.connect(init_room, CONNECT_DEFERRED)
	
	# Make sure minimap is at correct position (required for themes to work correctly).
	%Minimap.set_offsets_preset(Control.PRESET_TOP_RIGHT, Control.PRESET_MODE_MINSIZE, 8)
	
	_setup_title_menu()
	_setup_language_setting_ui()
	_setup_prologue_ui()
	_apply_localized_texts()

func _set_loading_visible(visible: bool, text: String = "", progress: float = 0.0) -> void:
	if not is_instance_valid(_loading_screen):
		return
	_loading_screen.visible = visible
	if is_instance_valid(_loading_label) and not text.is_empty():
		_loading_label.text = text
	if is_instance_valid(_loading_bar):
		_loading_bar.value = clampf(progress, 0.0, 1.0)

func _setup_prologue_ui() -> void:
	if not is_instance_valid(_prologue_screen):
		return
	_prologue_screen.visible = false
	if is_instance_valid(_prologue_dialogue_box):
		_prologue_dialogue_box.visible = false
	if is_instance_valid(_prologue_dialogue_text):
		_prologue_dialogue_text.text = ""
	var next_cb := Callable(self, &"_on_prologue_next_pressed")
	if is_instance_valid(_prologue_next_button) and not _prologue_next_button.pressed.is_connected(next_cb):
		_prologue_next_button.pressed.connect(next_cb)
	var skip_cb := Callable(self, &"_on_prologue_skip_pressed")
	if is_instance_valid(_prologue_skip_button) and not _prologue_skip_button.pressed.is_connected(skip_cb):
		_prologue_skip_button.pressed.connect(skip_cb)
	_prologue_root_abs = _get_prologue_root_abs()
	_prologue_slides = _build_prologue_slides(_prologue_root_abs)

func _get_prologue_root_abs() -> String:
	var in_project := ProjectSettings.globalize_path(PROLOGUE_PROJECT_ROOT)
	if DirAccess.dir_exists_absolute(in_project):
		return in_project
	if DirAccess.dir_exists_absolute(PROLOGUE_EXTERNAL_ROOT):
		return PROLOGUE_EXTERNAL_ROOT
	return ""

func _collect_markdown_files(root_abs: String) -> PackedStringArray:
	var ret := PackedStringArray()
	if root_abs.is_empty():
		return ret
	var md_root := root_abs.path_join(PROLOGUE_MD_ROOT_REL)
	if not DirAccess.dir_exists_absolute(md_root):
		return ret
	var dir := DirAccess.open(md_root)
	if dir == null:
		return ret
	dir.list_dir_begin()
	while true:
		var name := dir.get_next()
		if name.is_empty():
			break
		if dir.current_is_dir():
			continue
		if name.to_lower().ends_with(".md"):
			ret.append(md_root.path_join(name))
	dir.list_dir_end()
	ret.sort()
	return ret

func _build_prologue_slides(root_abs: String) -> Array[Dictionary]:
	var slides: Array[Dictionary] = []
	if root_abs.is_empty():
		return slides
	var md_files := _collect_markdown_files(root_abs)
	for md_path in md_files:
		var file := FileAccess.open(md_path, FileAccess.READ)
		if file == null:
			continue
		var text := file.get_as_text()
		slides.append_array(_parse_md_to_prologue_slides(text, root_abs, md_path))
	return slides

func _parse_md_to_prologue_slides(md_text: String, root_abs: String, md_path: String) -> Array[Dictionary]:
	var slides: Array[Dictionary] = []
	if md_text.is_empty():
		return slides
	var img_re := RegEx.new()
	if img_re.compile("!\\[(.*?)\\]\\((.*?)\\)") != OK:
		return slides
	var dia_re := RegEx.new()
	dia_re.compile("^([^：]{1,20})：(.+)$")
	var lines := md_text.split("\n")
	for raw_line in lines:
		var line := str(raw_line).strip_edges()
		if line.is_empty():
			continue
		if line.begins_with("<!--"):
			continue
		var img_matches := img_re.search_all(line)
		for m in img_matches:
			var caption := str(m.get_string(1))
			var rel := str(m.get_string(2)).strip_edges()
			if not rel.to_lower().ends_with(".png"):
				continue
			if not rel.begins_with("_images/"):
				continue
			if not rel.to_lower().ends_with("_v1.png"):
				continue
			var abs := md_path.get_base_dir().path_join(rel)
			slides.append({
				"type": "image",
				"abs": abs,
				"caption": caption,
				"source": md_path,
			})
		if line.begins_with("#") or line.begins_with(">") or line.begins_with("-") or line.begins_with("!"):
			continue
		if not line.contains("："):
			continue
		var dm := dia_re.search(line)
		if dm == null:
			continue
		var speaker := str(dm.get_string(1)).strip_edges()
		var content := str(dm.get_string(2)).strip_edges()
		if speaker.is_empty() or content.is_empty():
			continue
		slides.append({
			"type": "dialogue",
			"speaker": speaker,
			"text": content,
			"source": md_path,
		})
	return slides

func _load_texture_from_abs(abs_path: String) -> Texture2D:
	if abs_path.is_empty():
		return null
	if _prologue_texture_cache.has(abs_path):
		return _prologue_texture_cache[abs_path] as Texture2D
	var img := Image.new()
	var err := img.load(abs_path)
	if err != OK:
		_prologue_texture_cache[abs_path] = null
		return null
	var tex := ImageTexture.create_from_image(img)
	_prologue_texture_cache[abs_path] = tex
	return tex

func _set_prologue_visible(visible: bool) -> void:
	if not is_instance_valid(_prologue_screen):
		return
	_prologue_screen.visible = visible

func _show_prologue_slide(idx: int) -> void:
	if idx < 0 or idx >= _prologue_slides.size():
		return
	var slide := _prologue_slides[idx]
	var t := str(slide.get("type", "image"))
	if t == "dialogue":
		var speaker := str(slide.get("speaker", ""))
		var text := str(slide.get("text", ""))
		if is_instance_valid(_prologue_dialogue_box):
			_prologue_dialogue_box.visible = true
		if is_instance_valid(_prologue_dialogue_text):
			_prologue_dialogue_text.text = "%s：%s" % [speaker, text]
		if is_instance_valid(_prologue_caption):
			_prologue_caption.text = ""
		return
	
	var abs := str(slide.get("abs", ""))
	var caption := str(slide.get("caption", ""))
	var tex := _load_texture_from_abs(abs)
	if is_instance_valid(_prologue_image):
		_prologue_image.texture = tex
	if is_instance_valid(_prologue_dialogue_box):
		_prologue_dialogue_box.visible = false
	if is_instance_valid(_prologue_dialogue_text):
		_prologue_dialogue_text.text = ""
	if is_instance_valid(_prologue_caption):
		_prologue_caption.text = caption

func _on_prologue_next_pressed() -> void:
	if not _prologue_running:
		return
	_prologue_slide_idx += 1
	if _prologue_slide_idx >= _prologue_slides.size():
		_finish_prologue.call_deferred()
		return
	_show_prologue_slide(_prologue_slide_idx)

func _on_prologue_skip_pressed() -> void:
	if not _prologue_running:
		return
	_finish_prologue.call_deferred()

func _start_prologue() -> bool:
	if _prologue_slides.is_empty():
		return false
	_prologue_running = true
	_prologue_slide_idx = 0
	_set_prologue_visible(true)
	_show_prologue_slide(_prologue_slide_idx)
	return true

func _finish_prologue() -> void:
	_prologue_running = false
	_set_prologue_visible(false)

func _play_prologue_if_any() -> void:
	if not _start_prologue():
		return
	while _prologue_running:
		await get_tree().process_frame

func _enter_starting_room() -> bool:
	var ok := await _load_room_with_progress(starting_map, tr("LOADING_CHAPTER"))
	if ok:
		_teleport_player_to_save_point_if_any()
		await get_tree().physics_frame
		reset_map_starting_coords.call_deferred()
		player.process_mode = Node.PROCESS_MODE_INHERIT
	_set_loading_visible(false)
	if not ok:
		_setup_title_menu()
	return ok

func _load_packed_scene_threaded(resolved_path: String, label_text: String) -> PackedScene:
	if resolved_path.is_empty():
		return null
	_set_loading_visible(true, label_text, 0.0)
	await get_tree().process_frame
	var err := ResourceLoader.load_threaded_request(resolved_path, "PackedScene")
	if err != OK:
		return null
	var progress: Array = []
	while true:
		var status := ResourceLoader.load_threaded_get_status(resolved_path, progress)
		if status == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
			if progress.size() > 0:
				_set_loading_visible(true, "", float(progress[0]))
			await get_tree().process_frame
			continue
		if status == ResourceLoader.THREAD_LOAD_LOADED:
			_set_loading_visible(true, "", 1.0)
			var res := ResourceLoader.load_threaded_get(resolved_path)
			return res as PackedScene
		return null
	return null

func _load_room_with_progress(path: String, label_text: String) -> bool:
	if map_changing:
		return false
	map_changing = true
	var effective := path
	if not effective.begins_with("GEN") and not loop.is_empty():
		effective = loop
		loop = ""
	var resolved := ResourceUID.uid_to_path(effective) if effective.begins_with("uid://") else effective
	
	_set_loading_visible(true, label_text, 0.0)
	await get_tree().process_frame
	
	if map:
		map.queue_free()
		await map.tree_exited
		map = null
	
	var new_map: Node2D = null
	if effective.begins_with("GEN"):
		new_map = _load_room(effective) as Node2D
	else:
		var ps := await _load_packed_scene_threaded(resolved, label_text)
		if ps != null:
			_set_loading_visible(true, label_text, 1.0)
			await get_tree().process_frame
			new_map = ps.instantiate() as Node2D
	
	if new_map == null:
		map_changing = false
		return false
	
	map = new_map
	add_child(map)
	
	MetSys.current_layer = MetSys.get_current_room_instance().get_layer()
	map_changing = false
	room_loaded.emit()
	return true

func _teleport_player_to_save_point_if_any() -> void:
	if custom_run:
		return
	if map == null or not is_instance_valid(player):
		return
	var start := map.get_node_or_null(^"SavePoint") as Node2D
	if start != null:
		player.position = start.position

func _continue_game() -> void:
	_settings_panel.visible = false
	_title_menu.visible = false
	player.process_mode = Node.PROCESS_MODE_DISABLED
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.send({
			"type": ActorFramework.TYPE_RUNTIME_EVENT_SIGNAL,
			"signal": "Game.Continue",
			"source_domain": "System"
		})
	await _enter_starting_room()

func _apply_display_settings_on_startup() -> bool:
	var settings := _load_display_settings()
	var mode := str(settings.get(DISPLAY_SETTINGS_MODE_KEY, ""))
	var size := settings.get(DISPLAY_SETTINGS_SIZE_KEY, Vector2i.ZERO) as Vector2i
	_language_setting = str(settings.get(DISPLAY_SETTINGS_LANGUAGE_KEY, "auto"))
	_apply_language(_language_setting)
	var is_embedded := _is_embedded_window()
	
	if (OS.has_feature("editor") or is_embedded) and mode == "fullscreen":
		_save_display_settings("windowed", _default_window_size)
		mode = "windowed"
		size = _default_window_size
	
	if mode == "fullscreen":
		if not OS.has_feature("editor") and not is_embedded:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN)
		else:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
			if _default_window_size != Vector2i.ZERO:
				DisplayServer.window_set_size(_default_window_size)
	elif mode == "windowed":
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		if size != Vector2i.ZERO:
			DisplayServer.window_set_size(size)
	
	return false

func _load_display_settings() -> Dictionary:
	var cfg := ConfigFile.new()
	var err := cfg.load(DISPLAY_SETTINGS_PATH)
	if err != OK:
		return {}
	return {
		DISPLAY_SETTINGS_MODE_KEY: cfg.get_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_MODE_KEY, ""),
		DISPLAY_SETTINGS_SIZE_KEY: cfg.get_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_SIZE_KEY, Vector2i.ZERO),
		DISPLAY_SETTINGS_LANGUAGE_KEY: cfg.get_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_LANGUAGE_KEY, "auto"),
	}

func _save_display_settings(mode: String, window_size: Vector2i = Vector2i.ZERO, language: String = "") -> void:
	var cfg := ConfigFile.new()
	cfg.load(DISPLAY_SETTINGS_PATH)
	cfg.set_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_MODE_KEY, mode)
	if window_size != Vector2i.ZERO:
		cfg.set_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_SIZE_KEY, window_size)
	if not language.is_empty():
		cfg.set_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_LANGUAGE_KEY, language)
	cfg.save(DISPLAY_SETTINGS_PATH)

func _save_language_setting(language: String) -> void:
	var cfg := ConfigFile.new()
	cfg.load(DISPLAY_SETTINGS_PATH)
	cfg.set_value(DISPLAY_SETTINGS_SECTION, DISPLAY_SETTINGS_LANGUAGE_KEY, language)
	cfg.save(DISPLAY_SETTINGS_PATH)

func _normalize_locale(locale: String) -> String:
	var lc := locale.strip_edges()
	if lc.is_empty():
		return lc
	if lc.begins_with("zh"):
		return "zh_CN"
	if lc.begins_with("en"):
		return "en"
	return lc

func _apply_language(language: String) -> void:
	var lc := language.strip_edges()
	if lc.is_empty():
		lc = "auto"
	var target := lc
	if target == "auto":
		target = OS.get_locale()
	target = _normalize_locale(target)
	if not target.is_empty():
		TranslationServer.set_locale(target)

func _apply_localized_texts() -> void:
	if is_instance_valid(_continue_button):
		_continue_button.text = tr("MENU_CONTINUE")
	if is_instance_valid(_start_button):
		_start_button.text = tr("MENU_START")
	if is_instance_valid(_settings_button):
		_settings_button.text = tr("MENU_SETTINGS")
	if is_instance_valid(_quit_button):
		_quit_button.text = tr("MENU_QUIT")
	if is_instance_valid(_windowed_button):
		_windowed_button.text = tr("MENU_WINDOW")
	if is_instance_valid(_fullscreen_button):
		_fullscreen_button.text = tr("MENU_FULLSCREEN")
	if is_instance_valid(_back_button):
		_back_button.text = tr("MENU_BACK")
	if is_instance_valid(_language_label):
		_language_label.text = tr("MENU_LANGUAGE")
	if is_instance_valid(_loading_label):
		_loading_label.text = tr("LOADING")
	if is_instance_valid(_prologue_skip_button):
		_prologue_skip_button.text = tr("PROLOGUE_SKIP")
	if is_instance_valid(_prologue_next_button):
		_prologue_next_button.text = tr("PROLOGUE_NEXT")

func _setup_language_setting_ui() -> void:
	if not is_instance_valid(_language_option):
		return
	var cb := Callable(self, &"_on_language_option_selected")
	if not _language_option.item_selected.is_connected(cb):
		_language_option.item_selected.connect(cb)
	_refresh_language_option_items()

func _refresh_language_option_items() -> void:
	if not is_instance_valid(_language_option):
		return
	_syncing_language_option = true
	_language_option.clear()
	_language_option.add_item(tr("LANG_AUTO"), 0)
	_language_option.add_item(tr("LANG_EN"), 1)
	_language_option.add_item(tr("LANG_ZH_CN"), 2)
	var idx := 0
	match _language_setting:
		"en":
			idx = 1
		"zh_CN":
			idx = 2
		_:
			idx = 0
	_language_option.select(idx)
	_syncing_language_option = false

func _on_language_option_selected(index: int) -> void:
	if _syncing_language_option:
		return
	var code := "auto"
	match index:
		1:
			code = "en"
		2:
			code = "zh_CN"
		_:
			code = "auto"
	_language_setting = code
	_apply_language(_language_setting)
	_save_language_setting(_language_setting)
	_refresh_language_option_items()
	_apply_localized_texts()

func _relaunch_self(extra_args: PackedStringArray) -> bool:
	var args := PackedStringArray()
	if OS.has_feature("editor"):
		args.append("--path")
		args.append(ProjectSettings.globalize_path("res://"))
	args.append_array(extra_args)
	
	var pid := OS.create_instance(args)
	if pid > 0:
		return true
	
	var exe := OS.get_executable_path()
	if exe.is_empty():
		return false
	
	pid = OS.create_process(exe, args)
	return pid > 0

func _is_embedded_window() -> bool:
	var w := get_window()
	if not is_instance_valid(w):
		return true
	return w.is_embedded()

func _setup_title_menu() -> void:
	if not is_instance_valid(_title_menu):
		return
	
	var is_embedded := _is_embedded_window()
	
	var has_save := FileAccess.file_exists(SAVE_PATH)
	_continue_button.disabled = not has_save
	
	if is_embedded:
		_fullscreen_button.tooltip_text = "将重启游戏并进入全屏"
		_fullscreen_button.disabled = true
	else:
		_fullscreen_button.disabled = false
	
	var continue_cb := Callable(self, &"_on_continue_pressed")
	if not _continue_button.pressed.is_connected(continue_cb):
		_continue_button.pressed.connect(continue_cb)
	var start_cb := Callable(self, &"_on_start_pressed")
	if not _start_button.pressed.is_connected(start_cb):
		_start_button.pressed.connect(start_cb)
	var settings_cb := Callable(self, &"_on_settings_pressed")
	if not _settings_button.pressed.is_connected(settings_cb):
		_settings_button.pressed.connect(settings_cb)
	var quit_cb := Callable(self, &"_on_quit_pressed")
	if not _quit_button.pressed.is_connected(quit_cb):
		_quit_button.pressed.connect(quit_cb)
	var windowed_cb := Callable(self, &"_on_windowed_pressed")
	if not _windowed_button.pressed.is_connected(windowed_cb):
		_windowed_button.pressed.connect(windowed_cb)
	var fullscreen_cb := Callable(self, &"_on_fullscreen_pressed")
	if not _fullscreen_button.pressed.is_connected(fullscreen_cb):
		_fullscreen_button.pressed.connect(fullscreen_cb)
	var back_cb := Callable(self, &"_on_back_pressed")
	if not _back_button.pressed.is_connected(back_cb):
		_back_button.pressed.connect(back_cb)
	var restart_confirmed_cb := Callable(self, &"_on_restart_confirmed")
	if not _restart_confirm.confirmed.is_connected(restart_confirmed_cb):
		_restart_confirm.confirmed.connect(restart_confirmed_cb)
	var restart_canceled_cb := Callable(self, &"_on_restart_canceled")
	if not _restart_confirm.canceled.is_connected(restart_canceled_cb):
		_restart_confirm.canceled.connect(restart_canceled_cb)
	
	_settings_panel.visible = false
	_title_menu.visible = true
	player.process_mode = Node.PROCESS_MODE_DISABLED
	
	if has_save:
		_continue_button.grab_focus()
	else:
		_start_button.grab_focus()

func _on_continue_pressed() -> void:
	_continue_game.call_deferred()

func _on_start_pressed() -> void:
	_start_new_game.call_deferred()

func _start_new_game() -> void:
	if FileAccess.file_exists(SAVE_PATH):
		var abs_path := ProjectSettings.globalize_path(SAVE_PATH)
		DirAccess.remove_absolute(abs_path)
	
	var workbench := WorkbenchService.get_singleton()
	if workbench != null:
		workbench.register_workplace_data(&"progress", ProgressData.new())
	collectibles = 0
	loop = ""
	_in_random_level = false
	
	MetSys.reset_state()
	MetSys.set_save_data()
	_apply_area(AreaCatalog.get_initial_area_id(), true)
	_settings_panel.visible = false
	_title_menu.visible = false
	player.process_mode = Node.PROCESS_MODE_DISABLED
	_set_loading_visible(false)
	if workbench != null:
		workbench.send({
			"type": ActorFramework.TYPE_RUNTIME_EVENT_START
		})
		workbench.send({
			"type": ActorFramework.TYPE_RUNTIME_EVENT_SIGNAL,
			"signal": "Game.StartNew",
			"source_domain": "System"
		})
	await _play_prologue_if_any()
	await _enter_starting_room()

func _on_settings_pressed() -> void:
	_settings_panel.visible = not _settings_panel.visible
	if _settings_panel.visible:
		_windowed_button.grab_focus()

func _on_back_pressed() -> void:
	_settings_panel.visible = false
	_settings_button.grab_focus()

func _on_windowed_pressed() -> void:
	if _is_embedded_window():
		return
	_save_display_settings("windowed", DisplayServer.window_get_size())
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	if _default_window_size != Vector2i.ZERO:
		DisplayServer.window_set_size(_default_window_size)

func _on_fullscreen_pressed() -> void:
	_restart_confirm.popup_centered()

func _on_restart_confirmed() -> void:
	_save_display_settings("fullscreen")
	if _relaunch_self(PackedStringArray([RELAUNCH_ARG, "--fullscreen"])):
		get_tree().quit()
		return
	
	if not _is_embedded_window():
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN)
		return
	
	OS.alert("无法重启进程以进入全屏。请用独立窗口运行（不要嵌入运行），或导出后再尝试。", "重启失败")

func _on_restart_canceled() -> void:
	_fullscreen_button.grab_focus()

func _on_quit_pressed() -> void:
	get_tree().quit()

# Debugging helper. Press F2 to quickly reload game.
func _unhandled_input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if k and k.pressed and not k.echo:
		var key := k.keycode
		if key == 0:
			key = k.physical_keycode
		if key == KEY_Q:
			_debug_collisions_visible = not _debug_collisions_visible
			get_tree().debug_collisions_hint = _debug_collisions_visible
			get_viewport().set_input_as_handled()
			return
		
		var area_id: StringName = &""
		if key == KEY_F6:
			area_id = &"story_vertical"
		elif key == KEY_F7:
			area_id = &"story_dungeon_rooms"
		elif key == KEY_F8:
			area_id = &"story_overworld"
		elif key == KEY_F9:
			area_id = &"story_starsky"
		if area_id != &"":
			var workbench := WorkbenchService.get_singleton()
			if workbench != null:
				workbench.send({
					"type": ActorFramework.TYPE_LOAD_AREA_REQUEST,
					"area_id": area_id
				})
			get_viewport().set_input_as_handled()
			return
	if event.is_action_pressed(&"toggle_bag"):
		var inv := get_node_or_null(^"UI/InventoryUI")
		if inv != null and inv.has_method("toggle_bag"):
			inv.call("toggle_bag")
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed(&"toggle_equipment"):
		var inv2 := get_node_or_null(^"UI/InventoryUI")
		if inv2 != null and inv2.has_method("toggle_equipment"):
			inv2.call("toggle_equipment")
		get_viewport().set_input_as_handled()
		return
	if k and k.pressed and k.keycode == KEY_F2:
		var cr: Script
		# CustomRunner can't be used directly, since the addon is optional.
		if ResourceLoader.exists("res://addons/CustomRunner/CustomRunner.gd"):
			cr = load("res://addons/CustomRunner/CustomRunner.gd")
		
		if cr and cr.is_custom_running():
			get_tree().change_scene_to_file.call_deferred("res://CoreEngine/CustomRunnerIntegration/CustomStart.tscn")
		else:
			get_tree().reload_current_scene()

# Returns this node from anywhere.
static func get_singleton() -> Game:
	return (Game as Script).get_meta(&"singleton") as Game

func _get_save_data() -> Dictionary:
	var progress := _get_progress()
	var workbench := WorkbenchService.get_singleton()
	var area_id: StringName = &""
	if workbench != null:
		area_id = workbench.get_workplace_data(KEY_CURRENT_AREA_ID, &"") as StringName
	return {
		"collectible_count": collectibles,
		"generated_rooms": progress.generated_rooms if progress != null else [],
		"events": progress.events if progress != null else [],
		"current_area_id": area_id,
		"current_room": MetSys.get_current_room_id(),
		"abilities": player.abilities,
	}

func _set_save_data(data: Dictionary):
	collectibles = int(data.get("collectible_count", collectibles))
	var progress := _get_progress()
	if progress != null:
		progress.generated_rooms.assign(data.get("generated_rooms", []))
		var raw_events: Array = data.get("events", [])
		progress.events.clear()
		for e in raw_events:
			var s := StringName(str(e))
			if s != &"":
				progress.add_event(s)
	player.abilities.assign(data.get("abilities", []))
	var saved_area_id := StringName(str(data.get("current_area_id", "")))
	if saved_area_id != &"":
		_apply_area(saved_area_id, false)
	
	if not custom_run:
		var loaded_starting_map: String = data.get("current_room", "")
		if not loaded_starting_map.is_empty():
			starting_map = loaded_starting_map

func _apply_area_defaults() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var saved_area_id := workbench.get_workplace_data(KEY_CURRENT_AREA_ID, &"") as StringName
	if saved_area_id != &"":
		_apply_area(saved_area_id, false)
		return
	_apply_area(AreaCatalog.get_initial_area_id(), true)

func _apply_area(area_id: StringName, also_set_starting_room: bool) -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var def := AreaCatalog.get_area_def(area_id) as AreaDef
	if def == null:
		return
	workbench.register_workplace_data(KEY_CURRENT_AREA_ID, area_id)
	workbench.register_workplace_data(KEY_TRANSITION_STYLE, def.transition_style)
	workbench.register_workplace_data(KEY_MAP_TYPE, def.map_type)
	workbench.register_workplace_data(KEY_INPUT_MODE, def.input_mode)
	var mode_name: StringName = &"side_scrolling"
	match def.input_mode:
		AreaDef.InputMode.TOP_DOWN:
			mode_name = &"top_down"
		AreaDef.InputMode.TOP_DOWN_SHOOTER:
			mode_name = &"top_down_shooter"
	workbench.send({
		"type": ActorFramework.TYPE_INPUT_MODE_CHANGE_REQUEST,
		"mode": mode_name
	})
	if also_set_starting_room and not def.starting_room.is_empty():
		starting_map = def.starting_room

# Save game using MetSys SaveManager.
func save_game():
	var save_manager := SaveManager.new()
	save_manager.store_game(self)
	save_manager.save_as_text(SAVE_PATH)

func reset_map_starting_coords():
	$UI/MapWindow.reset_starting_coords()

func init_room():
	var cam := $Player/Camera2D as Camera2D
	var ri := MetSys.get_current_room_instance()
	if ri != null and cam != null:
		ri.adjust_camera_limits(cam)
	player.on_enter()
	_ensure_alice()
	_room_entry_player_global_pos = player.global_position if is_instance_valid(player) else Vector2.ZERO
	_room_entry_set = is_instance_valid(player)
	_fall_out_cooldown_s = 0.75
	_apply_room_collision_from_metadata()
	_apply_foreground_texture_transform_from_metadata()
	_apply_background_texture_transform_from_metadata()
	if cam != null:
		_apply_camera_limits_from_map_metadata(cam)
	print("Game.init_room room_id=%s room_path=%s" % [str(MetSys.get_current_room_id()), str(map.scene_file_path)])
	var bg_layer := map.get_node_or_null(NodePath("BackgroundLayer")) as CanvasLayer
	var bg := map.get_node_or_null(NodePath("BackgroundLayer/BackgroundTexture")) as TextureRect
	if bg_layer == null:
		print("Game.init_room BackgroundLayer not found")
	elif bg_layer.visible == false:
		print("Game.init_room BackgroundLayer visible=false layer=%d" % int(bg_layer.layer))
	else:
		print("Game.init_room BackgroundLayer visible=true layer=%d" % int(bg_layer.layer))
	if bg == null:
		print("Game.init_room BackgroundTexture not found")
	else:
		var tex_path := ""
		if bg.texture != null:
			tex_path = str(bg.texture.resource_path)
		var tex_size := Vector2.ZERO
		if bg.texture != null:
			tex_size = bg.texture.get_size()
		var viewport_size := Vector2.ZERO
		var vp := get_viewport()
		if vp != null:
			viewport_size = vp.get_visible_rect().size
		print("Game.init_room BackgroundTexture visible=%s modulate=%s z_index=%d expand_mode=%d stretch_mode=%d tex=%s tex_size=%s viewport=%s rect_size=%s" % [
			str(bg.visible),
			str(bg.modulate),
			int(bg.z_index),
			int(bg.expand_mode),
			int(bg.stretch_mode),
			tex_path,
			str(tex_size),
			str(viewport_size),
			str(bg.size)
		])
	
	var wb := WorkbenchService.get_singleton()
	if wb != null:
		wb.set_service(&"player", player)
		wb.send({
			"type": ActorFramework.TYPE_ROOM_LOADED,
			"room_id": StringName(str(MetSys.get_current_room_id())),
			"room_path": str(map.scene_file_path)
		})
	
	var is_random := str(map.scene_file_path).begins_with("GEN")
	if is_random != _in_random_level:
		var workbench := WorkbenchService.get_singleton()
		if workbench != null:
			workbench.send({
				"type": ActorFramework.TYPE_LEVEL_EVENT_REQUEST,
				"event": &"enter_random_level" if is_random else &"exit_random_level",
				"room": str(map.scene_file_path)
			})
		_in_random_level = is_random
	
	# Initializes MetSys.get_current_coords(), so you can use it from the beginning.
	if MetSys.last_player_position.x == Vector2i.MAX.x:
		MetSys.set_player_position(player.position)

func _process(_delta: float) -> void:
	if map == null:
		return
	if not is_instance_valid(_bg_perspective_rect):
		return
	if _bg_perspective_material == null:
		return
	_bg_perspective_material.set_shader_parameter("focus", _get_player_focus_in_foreground())

func _physics_process(delta: float) -> void:
	if map == null:
		return
	if map_changing:
		return
	if not _room_entry_set:
		return
	if not is_instance_valid(player):
		return
	if player.process_mode == Node.PROCESS_MODE_DISABLED:
		return
	
	_fall_out_cooldown_s = maxf(0.0, _fall_out_cooldown_s - delta)
	if _fall_out_cooldown_s > 0.0:
		return
	
	var bounds := _get_map_world_bounds_rect()
	if bounds.size == Vector2.ZERO:
		return
	
	var margin := 256.0
	var padded := bounds.grow(margin)
	var p := map.to_local(player.global_position)
	if padded.has_point(p):
		return
	
	player.global_position = _room_entry_player_global_pos
	player.set_meta(&"IsTransferred", true)
	player.IsTransferred = true
	MetSys.set_player_position(player.position)
	_fall_out_cooldown_s = 0.75

func _apply_camera_limits_from_map_metadata(camera: Camera2D) -> void:
	if map == null or camera == null:
		return
	
	var ri := MetSys.get_current_room_instance()
	var has_cells := false
	if ri != null and typeof(ri.get("cells")) == TYPE_ARRAY:
		has_cells = not (ri.get("cells") as Array).is_empty()
	if has_cells:
		return
	
	var mode := str(map.get_meta(&"collision_mode", "")).to_lower()
	var path := ""
	if mode == "fgtex" or mode == "foreground_texture":
		path = str(map.get_meta(&"collision_fgtex_path", ""))
	elif mode == "tile" or mode == "tiles" or mode == "tilemap":
		path = str(map.get_meta(&"collision_tile_path", ""))
	
	var room_sz := _get_room_world_size_from_collision_json()
	var has_room_rect := room_sz != Vector2.ZERO
	var room_rect := Rect2(Vector2.ZERO, room_sz)
	var fg_rect := _get_foreground_world_rect()
	var has_fg_rect := fg_rect.size != Vector2.ZERO
	
	if not has_room_rect and not has_fg_rect:
		return
	
	var min_x := room_rect.position.x if has_room_rect else fg_rect.position.x
	var min_y := room_rect.position.y if has_room_rect else fg_rect.position.y
	var max_x := room_rect.end.x if has_room_rect else fg_rect.end.x
	var max_y := room_rect.end.y if has_room_rect else fg_rect.end.y
	
	if has_fg_rect:
		min_x = minf(min_x, fg_rect.position.x)
		min_y = minf(min_y, fg_rect.position.y)
		max_x = maxf(max_x, fg_rect.end.x)
		max_y = maxf(max_y, fg_rect.end.y)
	
	camera.limit_left = int(floor(min_x))
	camera.limit_top = int(floor(min_y))
	camera.limit_right = int(ceil(max_x))
	camera.limit_bottom = int(ceil(max_y))

func _get_room_world_size_from_collision_json() -> Vector2:
	if map == null:
		return Vector2.ZERO
	
	var mode := str(map.get_meta(&"collision_mode", "")).to_lower()
	var path := ""
	if mode == "fgtex" or mode == "foreground_texture":
		path = str(map.get_meta(&"collision_fgtex_path", ""))
	elif mode == "tile" or mode == "tiles" or mode == "tilemap":
		path = str(map.get_meta(&"collision_tile_path", ""))
	
	if path.is_empty():
		return Vector2.ZERO
	
	var data := _load_collision_json(path)
	if data.is_empty():
		return Vector2.ZERO
	
	var room_w := int(data.get("RoomWidth", data.get("roomWidth", 0)))
	var room_h := int(data.get("RoomHeight", data.get("roomHeight", 0)))
	var tile_size := int(data.get("TileSize", data.get("tileSize", 32)))
	if room_w <= 0 or room_h <= 0 or tile_size <= 0:
		return Vector2.ZERO
	return Vector2(float(room_w * tile_size), float(room_h * tile_size))

func _get_map_world_bounds_rect() -> Rect2:
	var room_sz := _get_room_world_size_from_collision_json()
	var has_room_rect := room_sz != Vector2.ZERO
	var room_rect := Rect2(Vector2.ZERO, room_sz)
	var fg_rect := _get_foreground_world_rect()
	var has_fg_rect := fg_rect.size != Vector2.ZERO
	
	if not has_room_rect and not has_fg_rect:
		return Rect2()
	
	var min_x := room_rect.position.x if has_room_rect else fg_rect.position.x
	var min_y := room_rect.position.y if has_room_rect else fg_rect.position.y
	var max_x := room_rect.end.x if has_room_rect else fg_rect.end.x
	var max_y := room_rect.end.y if has_room_rect else fg_rect.end.y
	
	if has_fg_rect:
		min_x = minf(min_x, fg_rect.position.x)
		min_y = minf(min_y, fg_rect.position.y)
		max_x = maxf(max_x, fg_rect.end.x)
		max_y = maxf(max_y, fg_rect.end.y)
	
	return Rect2(Vector2(min_x, min_y), Vector2(max_x - min_x, max_y - min_y))

func _apply_foreground_texture_transform_from_metadata() -> void:
	if map == null:
		return
	var fg := _ensure_world_foreground_texture_sprite()
	if fg == null or fg.texture == null:
		return
	
	var mode := str(map.get_meta(&"collision_mode", "")).to_lower()
	if mode != "fgtex" and mode != "foreground_texture":
		return
	
	var upscale_v: Variant = map.get_meta(&"foreground_texture_upscale", 1.0)
	var upscale := 1.0
	if typeof(upscale_v) == TYPE_INT or typeof(upscale_v) == TYPE_FLOAT:
		upscale = float(upscale_v)
	else:
		upscale = float(str(upscale_v))
	if upscale <= 0.0:
		upscale = 1.0
	
	fg.centered = false
	fg.region_enabled = false
	fg.scale = Vector2(upscale, upscale)
	
	var room_sz := _get_room_world_size_from_collision_json()
	if room_sz == Vector2.ZERO:
		fg.position = Vector2.ZERO
		return
	
	var tex_sz := fg.texture.get_size() * upscale
	var anchor := str(map.get_meta(&"foreground_texture_anchor", "TopLeft")).to_lower()
	if anchor == "topleft" or anchor == "top_left" or anchor == "top-left" or anchor == "lt":
		fg.position = Vector2.ZERO
	elif anchor == "topright" or anchor == "top_right" or anchor == "top-right" or anchor == "rt":
		fg.position = Vector2(room_sz.x - tex_sz.x, 0.0)
	elif anchor == "bottomleft" or anchor == "bottom_left" or anchor == "bottom-left" or anchor == "lb":
		fg.position = Vector2(0.0, room_sz.y - tex_sz.y)
	elif anchor == "bottomright" or anchor == "bottom_right" or anchor == "bottom-right" or anchor == "rb":
		fg.position = Vector2(room_sz.x - tex_sz.x, room_sz.y - tex_sz.y)
	elif anchor == "center" or anchor == "centre" or anchor == "c":
		fg.position = (room_sz - tex_sz) / 2.0
	else:
		fg.position = Vector2.ZERO

func _apply_background_texture_transform_from_metadata() -> void:
	_bg_perspective_rect = null
	_bg_perspective_material = null
	_bg_perspective_upscale = 1.0
	
	if map == null:
		return
	
	var bg := map.get_node_or_null(^"BackgroundLayer/BackgroundTexture") as TextureRect
	if bg == null or bg.texture == null:
		return
	
	var upscale_v: Variant = map.get_meta(&"background_texture_upscale", 1.0)
	var upscale := 1.0
	if typeof(upscale_v) == TYPE_INT or typeof(upscale_v) == TYPE_FLOAT:
		upscale = float(upscale_v)
	else:
		upscale = float(str(upscale_v))
	if upscale < 1.0:
		upscale = 1.0
	
	var shader := Shader.new()
	shader.code = "shader_type canvas_item;\n\nuniform float upscale = 1.0;\nuniform vec2 focus = vec2(0.5, 0.5);\n\nvoid fragment() {\n\tvec2 tex_size = vec2(1.0) / TEXTURE_PIXEL_SIZE;\n\tvec2 screen_size = vec2(1.0) / SCREEN_PIXEL_SIZE;\n\tfloat cover_scale = max(screen_size.x / tex_size.x, screen_size.y / tex_size.y);\n\tvec2 cover_span = (screen_size / cover_scale) / tex_size;\n\tvec2 cover_offset = (vec2(1.0) - cover_span) * 0.5;\n\tfloat s = max(upscale, 1.0);\n\tvec2 zoom_span = cover_span / s;\n\tvec2 max_move = cover_span - zoom_span;\n\tvec2 f = clamp(focus, vec2(0.0), vec2(1.0));\n\tvec2 zoom_offset = cover_offset + max_move * f;\n\tvec2 uv = zoom_offset + UV * zoom_span;\n\tCOLOR = texture(TEXTURE, uv);\n}\n"
	
	var mat := ShaderMaterial.new()
	mat.shader = shader
	mat.set_shader_parameter("upscale", upscale)
	mat.set_shader_parameter("focus", _get_player_focus_in_foreground())
	bg.material = mat
	
	_bg_perspective_rect = bg
	_bg_perspective_material = mat
	_bg_perspective_upscale = upscale

func _get_foreground_world_rect() -> Rect2:
	var fg := _ensure_world_foreground_texture_sprite()
	if fg == null or fg.texture == null:
		return Rect2()
	var sz := fg.texture.get_size() * fg.scale
	if fg.region_enabled:
		sz = fg.region_rect.size * fg.scale
	if sz.x <= 0.0 or sz.y <= 0.0:
		return Rect2()
	return Rect2(fg.position, sz)

func _get_player_focus_in_foreground() -> Vector2:
	if map == null or not is_instance_valid(player):
		return Vector2(0.5, 0.5)
	
	var world_pos := player.global_position
	var pos := map.to_local(world_pos)
	
	var fg_rect := _get_foreground_world_rect()
	if fg_rect.size != Vector2.ZERO:
		var rel := (pos - fg_rect.position) / fg_rect.size
		return Vector2(clampf(rel.x, 0.0, 1.0), clampf(rel.y, 0.0, 1.0))
	
	var room_sz := _get_room_world_size_from_collision_json()
	if room_sz == Vector2.ZERO:
		return Vector2(0.5, 0.5)
	
	var rel2 := pos / room_sz
	return Vector2(clampf(rel2.x, 0.0, 1.0), clampf(rel2.y, 0.0, 1.0))

func _ensure_world_foreground_texture_sprite() -> Sprite2D:
	if map == null:
		return null
	
	var layer := map.get_node_or_null(^"ForegroundTextureLayer")
	if layer is Node2D:
		var s := map.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as Sprite2D
		if s != null:
			return s
		
		var tr := map.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as TextureRect
		if tr == null or tr.texture == null:
			return null
		
		tr.name = "ForegroundTexture_UI"
		tr.visible = false
		
		var ns := Sprite2D.new()
		ns.name = "ForegroundTexture"
		ns.texture = tr.texture
		ns.centered = false
		ns.position = Vector2.ZERO
		(layer as Node2D).add_child(ns)
		return ns
	
	if layer is CanvasLayer:
		var texture: Texture2D = null
		var tr2 := map.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as TextureRect
		if tr2 != null:
			texture = tr2.texture
		var s2 := map.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as Sprite2D
		if s2 != null:
			texture = s2.texture
		
		if texture == null:
			return null
		
		(layer as CanvasLayer).name = "ForegroundTextureLayer_UI"
		(layer as CanvasLayer).visible = false
		
		var nl := Node2D.new()
		nl.name = "ForegroundTextureLayer"
		nl.z_index = -1
		map.add_child(nl)
		
		var ns2 := Sprite2D.new()
		ns2.name = "ForegroundTexture"
		ns2.texture = texture
		ns2.centered = false
		ns2.position = Vector2.ZERO
		nl.add_child(ns2)
		return ns2
	
	return map.get_node_or_null(^"ForegroundTextureLayer/ForegroundTexture") as Sprite2D

func _apply_room_collision_from_metadata() -> void:
	if map == null:
		return
	var existing := map.get_node_or_null(^"CollisionFromJson")
	if existing != null:
		existing.queue_free()
	var mode := str(map.get_meta(&"collision_mode", ""))
	if mode.to_lower() == "fgtex" or mode.to_lower() == "foreground_texture":
		var path := str(map.get_meta(&"collision_fgtex_path", ""))
		if path.is_empty():
			return
		var data := _load_collision_json(path)
		if data.is_empty():
			return
		_build_collision_from_json_data(data)

func _load_collision_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var txt := f.get_as_text()
	var parsed_any: Variant = JSON.parse_string(txt)
	if typeof(parsed_any) != TYPE_DICTIONARY:
		return {}
	return parsed_any as Dictionary

func _build_collision_from_json_data(data: Dictionary) -> void:
	if map == null:
		return
	var root := StaticBody2D.new()
	root.name = "CollisionFromJson"
	root.collision_layer = 1
	root.collision_mask = 1
	map.add_child(root)
	
	var polys_any: Variant = data.get("Polygons", data.get("polygons", []))
	if typeof(polys_any) == TYPE_ARRAY and (polys_any as Array).size() > 0:
		for poly in polys_any as Array:
			var points := _parse_polygon_points(poly)
			if points.size() < 3:
				continue
			var cp := CollisionPolygon2D.new()
			cp.polygon = points
			root.add_child(cp)
		return
	
	var solid_any: Variant = data.get("Solid", data.get("solid", []))
	var room_w := int(data.get("RoomWidth", data.get("roomWidth", 0)))
	var room_h := int(data.get("RoomHeight", data.get("roomHeight", 0)))
	if room_w <= 0 or room_h <= 0:
		return
	if typeof(solid_any) != TYPE_ARRAY:
		return
	_build_grid_collision_shapes(root, solid_any as Array, room_w, room_h, 32)

func _parse_polygon_points(poly) -> PackedVector2Array:
	var out := PackedVector2Array()
	if typeof(poly) != TYPE_ARRAY:
		return out
	for pt in poly as Array:
		if typeof(pt) == TYPE_DICTIONARY:
			var d := pt as Dictionary
			var x := float(d.get("X", d.get("x", 0.0)))
			var y := float(d.get("Y", d.get("y", 0.0)))
			out.append(Vector2(x, y))
		elif typeof(pt) == TYPE_ARRAY:
			var a := pt as Array
			if a.size() >= 2:
				out.append(Vector2(float(a[0]), float(a[1])))
	return out

func _build_grid_collision_shapes(root: StaticBody2D, solid: Array, room_w: int, room_h: int, tile_size: int) -> void:
	var expected := room_w * room_h
	if solid.size() < expected:
		return
	for y in range(room_h):
		var x := 0
		while x < room_w:
			var idx := y * room_w + x
			if idx < 0 or idx >= solid.size() or not bool(solid[idx]):
				x += 1
				continue
			var start_x := x
			while x < room_w and bool(solid[y * room_w + x]):
				x += 1
			var seg_len := x - start_x
			var shape := RectangleShape2D.new()
			shape.size = Vector2(seg_len * tile_size, tile_size)
			var cs := CollisionShape2D.new()
			cs.shape = shape
			cs.position = Vector2(start_x * tile_size + (seg_len * tile_size) / 2.0, y * tile_size + tile_size / 2.0)
			root.add_child(cs)

func _get_progress() -> ProgressData:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return null
	return workbench.get_workplace_data(&"progress") as ProgressData

func _on_workplace(workplace) -> void:
	if workplace == null:
		return
	var t: StringName = workplace.type
	var msg: Dictionary = workplace.payload
	match t:
		ActorFramework.TYPE_BATTLE_RESULT_REQUEST:
			_show_battle_result(str(msg.get("text", "")))

func _ensure_alice() -> void:
	if map == null:
		return
	var is_starting_map := str(map.scene_file_path) == INITIAL_ROOM_PATH
	var alice := map.get_node_or_null(^"Alice")
	if not is_starting_map:
		if alice != null:
			alice.queue_free()
		return
	
	if alice == null:
		alice = AliceNPCScene.instantiate()
		alice.name = "Alice"
		var spawn := map.get_node_or_null(^"AliceSpawn") as Node2D
		if spawn != null:
			alice.position = spawn.position
		elif is_instance_valid(player):
			alice.position = player.position + Vector2(96, 0)
		map.add_child(alice)
	
	alice.set("random_spawn_enabled", false)
	alice.set("is_enemy", false)
	if alice.has_method("set_enemy_mode"):
		alice.call("set_enemy_mode", false)

func _ensure_dialogue_manager() -> void:
	if get_node_or_null(^"DialogueManagerActor") != null:
		return
	var mgr := DialogueManagerActor.new()
	mgr.name = "DialogueManagerActor"
	add_child(mgr)

func _on_enemy_defeated(_npc: Node) -> void:
	_show_battle_result("胜利")

func _show_battle_result(text: String) -> void:
	var hud := get_node_or_null(^"UI/BattleHUD")
	if hud != null and hud.has_method("show_result"):
		hud.call("show_result", text, 2.0)

# Customized load function that handles maps generated in Dice.tscn and loops in LoopRoom.tscn.
func _load_room(path: String) -> Node:
	if not path.begins_with("GEN"):
		# See LoopScript.
		if not loop.is_empty():
			path = loop
			loop = ""
		return super(path)
	
	# Base scene that will be customized (Junction.tscn).
	var prototype := preload("res://CoreEngine/Maps/Junction.tscn").instantiate()
	prototype.scene_file_path = path
	
	var config := path.split("/")
	# Assign values to the scene (see the script in Junction.tscn).
	prototype.exits = config[2].to_int()
	prototype.has_collectible = config[3] == "true"
	# Apply the values. It has to happen before the scene enters tree.
	prototype.apply_config()
	
	return prototype
