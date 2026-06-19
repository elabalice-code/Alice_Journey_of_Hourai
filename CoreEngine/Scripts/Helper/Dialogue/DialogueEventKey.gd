extends RefCounted
class_name DialogueEventKey

static func story_done(npc_id: StringName) -> StringName:
	return StringName("story_%s_done" % String(npc_id))
