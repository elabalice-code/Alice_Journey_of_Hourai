extends CanvasLayer
class_name GameStartupUI

signal continue_requested
signal new_game_requested
signal quit_requested

const DISPLAY_SETTINGS_PATH = "user://display_settings.cfg"
const DISPLAY_SETTINGS_SECTION = "display"
const DISPLAY_SETTINGS_MODE_KEY = "mode"
const DISPLAY_SETTINGS_SIZE_KEY = "window_size"
const DISPLAY_SETTINGS_LANGUAGE_KEY = "language"
const PROLOGUE_PROJECT_ROOT: String = "res://000_UserInput/00_序章"
const PROLOGUE_EXTERNAL_ROOT: String = "D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/0_UserInput/00_序章"
const PROLOGUE_MD_ROOT_REL: String = "00_0-0_魔宫附近"
const RELAUNCH_ARG = "--aoj_relaunch"

var _default_window_size: Vector2i
var _save_path: String = ""
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

func initialize(save_path: String, default_window_size: Vector2i) -> void:
	_save_path = save_path
	_default_window_size = default_window_size
	_setup_title_menu()
	_setup_language_setting_ui()
	_setup_prologue_ui()
	_apply_localized_texts()

func apply_display_settings_on_startup(default_window_size: Vector2i) -> bool:
	_default_window_size = default_window_size
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

func show_title_menu() -> void:
	_setup_title_menu()

func hide_title_menu() -> void:
	if is_instance_valid(_settings_panel):
		_settings_panel.visible = false
	if is_instance_valid(_title_menu):
		_title_menu.visible = false

func set_loading_visible(visible: bool, text: String = "", progress: float = 0.0) -> void:
	if not is_instance_valid(_loading_screen):
		return
	_loading_screen.visible = visible
	if is_instance_valid(_loading_label) and not text.is_empty():
		_loading_label.text = text
	if is_instance_valid(_loading_bar):
		_loading_bar.value = clampf(progress, 0.0, 1.0)

func play_prologue_if_any() -> void:
	if not _start_prologue():
		return
	while _prologue_running:
		await get_tree().process_frame

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
	if dia_re.compile("^([^：]{1,20})：(.+)$") != OK:
		return slides
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
			var abs_path := md_path.get_base_dir().path_join(rel)
			slides.append({
				"type": "image",
				"abs": abs_path,
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
	
	var abs_path := str(slide.get("abs", ""))
	var caption := str(slide.get("caption", ""))
	var tex := _load_texture_from_abs(abs_path)
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
	var has_save := FileAccess.file_exists(_save_path)
	if is_instance_valid(_continue_button):
		_continue_button.disabled = not has_save
	
	if is_instance_valid(_fullscreen_button):
		if is_embedded:
			_fullscreen_button.tooltip_text = "Fullscreen is disabled in embedded windows."
			_fullscreen_button.disabled = true
		else:
			_fullscreen_button.disabled = false
	
	var continue_cb := Callable(self, &"_on_continue_pressed")
	if is_instance_valid(_continue_button) and not _continue_button.pressed.is_connected(continue_cb):
		_continue_button.pressed.connect(continue_cb)
	var start_cb := Callable(self, &"_on_start_pressed")
	if is_instance_valid(_start_button) and not _start_button.pressed.is_connected(start_cb):
		_start_button.pressed.connect(start_cb)
	var settings_cb := Callable(self, &"_on_settings_pressed")
	if is_instance_valid(_settings_button) and not _settings_button.pressed.is_connected(settings_cb):
		_settings_button.pressed.connect(settings_cb)
	var quit_cb := Callable(self, &"_on_quit_pressed")
	if is_instance_valid(_quit_button) and not _quit_button.pressed.is_connected(quit_cb):
		_quit_button.pressed.connect(quit_cb)
	var windowed_cb := Callable(self, &"_on_windowed_pressed")
	if is_instance_valid(_windowed_button) and not _windowed_button.pressed.is_connected(windowed_cb):
		_windowed_button.pressed.connect(windowed_cb)
	var fullscreen_cb := Callable(self, &"_on_fullscreen_pressed")
	if is_instance_valid(_fullscreen_button) and not _fullscreen_button.pressed.is_connected(fullscreen_cb):
		_fullscreen_button.pressed.connect(fullscreen_cb)
	var back_cb := Callable(self, &"_on_back_pressed")
	if is_instance_valid(_back_button) and not _back_button.pressed.is_connected(back_cb):
		_back_button.pressed.connect(back_cb)
	var restart_confirmed_cb := Callable(self, &"_on_restart_confirmed")
	if is_instance_valid(_restart_confirm) and not _restart_confirm.confirmed.is_connected(restart_confirmed_cb):
		_restart_confirm.confirmed.connect(restart_confirmed_cb)
	var restart_canceled_cb := Callable(self, &"_on_restart_canceled")
	if is_instance_valid(_restart_confirm) and not _restart_confirm.canceled.is_connected(restart_canceled_cb):
		_restart_confirm.canceled.connect(restart_canceled_cb)
	
	_settings_panel.visible = false
	_title_menu.visible = true
	
	if has_save and is_instance_valid(_continue_button):
		_continue_button.grab_focus()
	elif is_instance_valid(_start_button):
		_start_button.grab_focus()

func _on_continue_pressed() -> void:
	continue_requested.emit()

func _on_start_pressed() -> void:
	new_game_requested.emit()

func _on_settings_pressed() -> void:
	_settings_panel.visible = not _settings_panel.visible
	if _settings_panel.visible and is_instance_valid(_windowed_button):
		_windowed_button.grab_focus()

func _on_back_pressed() -> void:
	_settings_panel.visible = false
	if is_instance_valid(_settings_button):
		_settings_button.grab_focus()

func _on_windowed_pressed() -> void:
	if _is_embedded_window():
		return
	_save_display_settings("windowed", DisplayServer.window_get_size())
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	if _default_window_size != Vector2i.ZERO:
		DisplayServer.window_set_size(_default_window_size)

func _on_fullscreen_pressed() -> void:
	if is_instance_valid(_restart_confirm):
		_restart_confirm.popup_centered()

func _on_restart_confirmed() -> void:
	_save_display_settings("fullscreen")
	if _relaunch_self(PackedStringArray([RELAUNCH_ARG, "--fullscreen"])):
		get_tree().quit()
		return
	
	if not _is_embedded_window():
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN)
		return
	
	OS.alert("Unable to restart into fullscreen. Please run in a standalone window or exported build.", "Restart failed")

func _on_restart_canceled() -> void:
	if is_instance_valid(_fullscreen_button):
		_fullscreen_button.grab_focus()

func _on_quit_pressed() -> void:
	quit_requested.emit()
