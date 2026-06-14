# A save point object. Colliding with it saves the game.
extends Area2D
const ActorFramework = preload("res://CoreEngine/Scripts/Actor/ActorFramework.gd")

@onready var start_time := Time.get_ticks_msec()

func _ready() -> void:
	body_entered.connect(on_body_entered)

# Player enter save point. Note that in a legit code this should check whether body is really a player.
func on_body_entered(body: Node2D) -> void:
	if Time.get_ticks_msec() - start_time < 1000:
		return
	
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	
	workbench.send({
		"type": ActorFramework.TYPE_SAVE_REQUEST,
		"reason": &"save_point",
		"body": body
	})

func _draw() -> void:
	$CollisionShape2D.shape.draw(get_canvas_item(), Color.BLUE)
