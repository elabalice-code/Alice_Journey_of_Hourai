# map_starsky：地图皮肤替换 Note

## 这张地图在工程里的组成

- 地图场景：`res://CoreEngine/Maps/map_starsky.tscn`
- 地面生成脚本：`res://CoreEngine/Scripts/World/MapStarSky.gd`
  - 通过 `TileMap/Foreground` 在运行时批量 `set_cell()` 生成一条地面（没有空中石块）
- 背景：`map_starsky.tscn` 里有一个 `CanvasLayer + ColorRect`，默认纯黑
- 左右出口：`ExitLeft` / `ExitRight` 两个 `Portal`（`res://CoreEngine/Objects/Portal.tscn`）

## 皮肤替换的两种推荐做法

### 做法 A：替换 Tileset（推荐，适合做整套主题）

1. 在 Godot 里复制一份 TileSet 资源：
   - 从 `res://CoreEngine/Resources/Tileset.tres` 复制为例如 `Tileset_StarSky.tres`
2. 打开新 TileSet（`Tileset_StarSky.tres`），在每个 `TileSetAtlasSource` 里替换贴图：
   - 例如把默认的 `Tiles.png / DarkTiles.png / MirageTiles.png` 换成你的“星空主题”tile贴图
3. 打开 `map_starsky.tscn`，把 `TileMap/Foreground` 的 `tile_set` 指向 `Tileset_StarSky.tres`
4. 如果你还想让其它地图也换主题，同样把它们的 `TileMapLayer.tile_set` 改成新 tileset

适用场景：
- 你有一整套新的 tile 图（地面、墙、装饰），希望全图一致替换

### 做法 B：不换 Tileset，只改“用哪一套 atlas/source + 用哪一个 tile”（最快）

`MapStarSky.gd` 暴露了几组导出参数，你可以直接在 `map_starsky.tscn` 里选中根节点 `Map`，在 Inspector 改：

- `ground_source_id`
  - `1`：Tiles.png（默认）
  - `2`：DarkTiles.png
  - `3`：MirageTiles.png
- `ground_atlas_coords`
  - 这是 tile 在 atlas 里的坐标（例如 `Vector2i(3, 6)`）

你可以通过这两个参数，把地面从“普通砖块”一键换成“暗色砖块/幻彩砖块”，而不需要改 TileSet 资源本身。

适用场景：
- 你只想快速换一种现成风格（普通/暗色/幻彩），或只想换地面那一块 tile

## 背景从纯黑换成“星空背景”的方式

当前背景是 `CanvasLayer/Black(ColorRect)`，替换为星空背景有两种常见做法：

### 方式 1：ColorRect 改为 TextureRect（最简单）

1. 在 `map_starsky.tscn` 的 `StarSkyBackground` 下新增 `TextureRect`
2. 设为全屏（anchors preset: Full Rect）
3. 指定你的星空贴图（Texture）
4. 删除或隐藏 `Black(ColorRect)`

### 方式 2：ParallaxBackground（有视差，更像“远处星空”）

1. 在地图根节点下新增 `ParallaxBackground`
2. 添加 1~2 个 `ParallaxLayer`，每层放一个 `Sprite2D/TextureRect`
3. 调整每层的 `motion_scale`，让星空移动得更慢（远景）

## 注意点（避免踩坑）

- 这套工程的房间必须在 `res://CoreEngine/Maps/MapData.txt` 里有 cell 绑定，否则 `RoomInstance` 在运行时会拿不到 room 信息导致异常。
- `map_starsky` 的画面尺寸默认按 `MetSys.settings.in_game_cell_size = (864, 480)` 的 1 个 cell 设计；如果你改了 cell size，地面行数/位置（`ground_y`）也需要一起调整。

