extends CanvasLayer

@onready var panel: Panel = $Panel
@onready var text_label: Label = $Panel/TextLabel
@onready var name_label: Label = $Panel/NameLabel
@onready var prompt_label: Label = $PromptLabel

func _ready() -> void:
	hide_dialogue()
	hide_prompt()

func show_dialogue(text: String, speaker_name: String) -> void:
	panel.visible = true
	text_label.text = text
	name_label.text = speaker_name
	prompt_label.visible = false

func hide_dialogue() -> void:
	panel.visible = false
	text_label.text = ""
	name_label.text = ""

func show_prompt(text: String) -> void:
	prompt_label.text = text
	prompt_label.visible = true

func hide_prompt() -> void:
	prompt_label.visible = false
