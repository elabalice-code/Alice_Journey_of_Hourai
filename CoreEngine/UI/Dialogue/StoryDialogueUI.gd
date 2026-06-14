extends CanvasLayer

@onready var panel: Panel = $Panel
@onready var text_label: Label = $Panel/TextLabel
@onready var name_label: Label = $Panel/NameLabel

func _ready() -> void:
	hide_dialogue()

func show_dialogue(text: String, speaker_name: String) -> void:
	panel.visible = true
	text_label.text = text
	name_label.text = speaker_name

func hide_dialogue() -> void:
	panel.visible = false
	text_label.text = ""
	name_label.text = ""
