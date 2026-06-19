extends RefCounted
class_name CollisionLayout

static func selected_path_from_metadata(metadata: Dictionary) -> String:
	var mode := str(metadata.get("collision_mode", "")).to_lower()
	if uses_foreground_texture(mode):
		return str(metadata.get("collision_fgtex_path", ""))
	if mode == "tile" or mode == "tiles" or mode == "tilemap":
		return str(metadata.get("collision_tile_path", ""))
	return ""

static func uses_foreground_texture(mode: String) -> bool:
	var lowered := mode.to_lower()
	return lowered == "fgtex" or lowered == "foreground_texture"

static func room_world_size(data: Dictionary) -> Vector2:
	var room_w := int(data.get("RoomWidth", data.get("roomWidth", 0)))
	var room_h := int(data.get("RoomHeight", data.get("roomHeight", 0)))
	var tile_size := int(data.get("TileSize", data.get("tileSize", 32)))
	if room_w <= 0 or room_h <= 0 or tile_size <= 0:
		return Vector2.ZERO
	return Vector2(float(room_w * tile_size), float(room_h * tile_size))

static func polygon_points(poly: Variant) -> PackedVector2Array:
	var out := PackedVector2Array()
	if typeof(poly) != TYPE_ARRAY:
		return out
	for pt in poly as Array:
		if typeof(pt) == TYPE_DICTIONARY:
			var d := pt as Dictionary
			out.append(Vector2(
				float(d.get("X", d.get("x", 0.0))),
				float(d.get("Y", d.get("y", 0.0)))
			))
		elif typeof(pt) == TYPE_ARRAY:
			var a := pt as Array
			if a.size() >= 2:
				out.append(Vector2(float(a[0]), float(a[1])))
	return out

static func polygon_list(data: Dictionary) -> Array:
	var result: Array = []
	var polys_any: Variant = data.get("Polygons", data.get("polygons", []))
	if typeof(polys_any) != TYPE_ARRAY:
		return result
	for poly in polys_any as Array:
		var points := polygon_points(poly)
		if points.size() >= 3:
			result.append(points)
	return result

static func solid_grid_rects(data: Dictionary) -> Array[Rect2]:
	var result: Array[Rect2] = []
	var solid_any: Variant = data.get("Solid", data.get("solid", []))
	if typeof(solid_any) != TYPE_ARRAY:
		return result
	var solid := solid_any as Array
	var room_w := int(data.get("RoomWidth", data.get("roomWidth", 0)))
	var room_h := int(data.get("RoomHeight", data.get("roomHeight", 0)))
	var tile_size := int(data.get("TileSize", data.get("tileSize", 32)))
	if room_w <= 0 or room_h <= 0 or tile_size <= 0:
		return result
	var expected := room_w * room_h
	if solid.size() < expected:
		return result
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
			result.append(Rect2(
				Vector2(start_x * tile_size, y * tile_size),
				Vector2(seg_len * tile_size, tile_size)
			))
	return result

static func union_bounds(room_size: Vector2, foreground_rect: Rect2) -> Rect2:
	var has_room_rect := room_size != Vector2.ZERO
	var room_rect := Rect2(Vector2.ZERO, room_size)
	var has_fg_rect := foreground_rect.size != Vector2.ZERO
	if not has_room_rect and not has_fg_rect:
		return Rect2()
	var min_x := room_rect.position.x if has_room_rect else foreground_rect.position.x
	var min_y := room_rect.position.y if has_room_rect else foreground_rect.position.y
	var max_x := room_rect.end.x if has_room_rect else foreground_rect.end.x
	var max_y := room_rect.end.y if has_room_rect else foreground_rect.end.y
	if has_fg_rect:
		min_x = minf(min_x, foreground_rect.position.x)
		min_y = minf(min_y, foreground_rect.position.y)
		max_x = maxf(max_x, foreground_rect.end.x)
		max_y = maxf(max_y, foreground_rect.end.y)
	return Rect2(Vector2(min_x, min_y), Vector2(max_x - min_x, max_y - min_y))

static func anchor_position(anchor: String, room_size: Vector2, texture_size: Vector2) -> Vector2:
	var lowered := anchor.to_lower()
	if lowered == "topleft" or lowered == "top_left" or lowered == "top-left" or lowered == "lt":
		return Vector2.ZERO
	if lowered == "topright" or lowered == "top_right" or lowered == "top-right" or lowered == "rt":
		return Vector2(room_size.x - texture_size.x, 0.0)
	if lowered == "bottomleft" or lowered == "bottom_left" or lowered == "bottom-left" or lowered == "lb":
		return Vector2(0.0, room_size.y - texture_size.y)
	if lowered == "bottomright" or lowered == "bottom_right" or lowered == "bottom-right" or lowered == "rb":
		return Vector2(room_size.x - texture_size.x, room_size.y - texture_size.y)
	if lowered == "center" or lowered == "centre" or lowered == "c":
		return (room_size - texture_size) / 2.0
	return Vector2.ZERO
