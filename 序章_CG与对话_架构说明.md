# 序章 CG 与对话：运行时架构说明

本文档说明当前仓库里“序章 CG/地图规划图播放 + 简易对话展示”的数据来源、运行时结构，以及最后如何确定切入到哪个地图。

---

## 1. 数据从哪里来

### 1.1 序章素材与脚本

- 外部输入目录（你提供的原始素材/脚本）：  
  `D:\Task_Panel\0_AliceJOH\0_AOJ_Reference\workspace\0_UserInput\00_序章`
- 项目内镜像目录（用于将来导出/打包更稳妥；当前也会优先读取）：  
  `res://000_UserInput/00_序章`

运行时会按以下优先级选择根路径（见 [Game.gd:L162-L168](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L162-L168)）：

1. 若项目内 `res://000_UserInput/00_序章` 存在 → 使用它
2. 否则回退到你机器上的绝对路径 `D:/Task_Panel/.../0_UserInput/00_序章`

### 1.2 Markdown 作为“序章脚本源”

当前实现只扫描一个固定子目录（见 [Game.gd:L170-L191](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L170-L191)）：

- `00_序章/00_0-0_魔宫附近/*.md`

并将这些 md 文件排序后按顺序解析为“序章播放队列”（见 [Game.gd:L193-L204](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L193-L204)）。

---

## 2. 运行时怎么把 CG / 对话变成“可播放的队列”

### 2.1 Slide（播放条目）的抽象

解析产物是一个数组：`_prologue_slides: Array[Dictionary]`（初始化见 [Game.gd:L145-L160](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L145-L160)）。

每个元素是一个 Dictionary（目前支持两类）：

- **image slide**
  - `type = "image"`
  - `abs`: 图片绝对路径（Windows 路径）
  - `caption`: 图片 markdown 的 `[]` 内文字
  - `source`: 来源 md 文件路径
- **dialogue slide**
  - `type = "dialogue"`
  - `speaker`: 发言者（冒号前）
  - `text`: 文本内容（冒号后）
  - `source`: 来源 md 文件路径

### 2.2 图片抽取规则（CG / 地图规划图）

解析规则见 [Game.gd:L206-L256](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L206-L256)：

- 识别 `![caption](rel_path)` 这种 markdown 图片语法
- 只接受：
  - `rel_path` 以 `_images/` 开头
  - 以 `.png` 结尾
  - 且以 `_v1.png` 结尾（即正文里主展示图，而不是 v2~v5 的候选）
- 图片路径拼接方式：以 **md 文件所在目录** 为基准：  
  `abs = md_path.get_base_dir().path_join(rel)`

因此 md 里写的 `_images/0001/cg/bg_0001..._v1.png` 会被正确解析到：  
`.../00_0-0_魔宫附近/_images/0001/cg/bg_0001..._v1.png`

### 2.3 对话抽取规则（最小可用版本）

同样在 [Game.gd:L206-L256](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L206-L256)：

- 跳过以 `#`/`>`/`-`/`!` 开头的行（标题、引用、列表、图片行）
- 对普通文本行，若包含中文冒号 `：`，并匹配正则：  
  `^([^：]{1,20})：(.+)$`
  - 冒号前为 speaker（长度 1~20，允许包含括号等，例如：`爱丽丝（OS / 轻声）`）
  - 冒号后为文本内容
- 结果会被加入 `dialogue slide`

---

## 3. UI 层：怎么显示图片与对话

序章 UI 位于主场景 [Game.tscn](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Game.tscn) 中的 `UI/PrologueScreen`（关键节点见 [Game.tscn:L532-L566](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Game.tscn#L532-L566)）：

- `Image`（TextureRect）：显示 CG/地图图
- `DialogueBox`（PanelContainer） + `DialogueText`（Label）：显示对话
- `Caption`（Label）：显示图片的 caption
- `SkipButton` / `NextButton`：推进/跳过

渲染逻辑在 [Game.gd:L277-L303](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L277-L303)：

- 当 slide 是 `dialogue`：
  - 不切换图片（保持上一张图作为“背景”）
  - 显示对话框并写入 `speaker：text`
  - 清空 caption
- 当 slide 是 `image`：
  - 读取本机绝对路径 PNG，生成 `ImageTexture` 并赋值给 `Image.texture`
  - 隐藏对话框
  - caption 显示为图片的 `![...]` 文本

图片加载使用 `Image.load(abs_path)`（见 [Game.gd:L258-L270](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L258-L270)），并有简单缓存 `_prologue_texture_cache`，避免同一张图重复解码。

---

## 4. 流程层：什么时候播放序章、什么时候切入地图

### 4.1 “开始游戏”流程（从主菜单到地图）

入口是 [Game.gd:_start_new_game](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L653-L674)：

1. 清存档（如果存在 `SAVE_PATH`）
2. 重置 MetSys 状态并写入空存档态：`MetSys.reset_state()` + `MetSys.set_save_data()`
3. 选择“初始章节/区域”，并把该区域的起始房间写入 `starting_map`：  
   `_apply_area(AreaCatalog.get_initial_area_id(), true)`
4. 隐藏主菜单、禁用 player 处理
5. 播放序章（若有可播放条目）：`await _play_prologue_if_any()`（见 [Game.gd:L332-L336](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L332-L336)）
6. 切入地图并加载：`await _enter_starting_room()`（见 [Game.gd:L338-L348](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L338-L348)）

### 4.2 “继续游戏”流程

继续游戏不会播放序章，直接进入 `_enter_starting_room()`（内部会使用当前 `starting_map`，而它可能来自存档恢复；见 [Game.gd:L647-L674](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L647-L674) 和 [Game.gd:L338-L348](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L338-L348)）。

---

## 5. 最后“切入到哪个地图”是怎么确定的

### 5.1 初始章节（Area）从哪里来

当前初始章节由 [AreaCatalog.gd](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/World/AreaCatalog.gd) 决定：

- `AreaCatalog.get_initial_area_id()` 返回 `INITIAL_AREA_ID`（见 [AreaCatalog.gd:L5-L17](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/World/AreaCatalog.gd#L5-L17)）
- `INITIAL_AREA_ID` 当前是 `story_dungeon_rooms`

### 5.2 初始“房间/地图场景”从哪里来

当 `_apply_area(area_id, also_set_starting_room=true)` 被调用时，会读取该 area 的 `starting_room`，并写回 `Game.starting_map`（见 [Game.gd:_apply_area](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L817-L839)）。

以 `story_dungeon_rooms` 为例，它的 `starting_room` 在 AreaCatalog 中定义为：

- `res://CoreEngine/Maps/DiceRoom.tscn`（见 [AreaCatalog.gd:L37-L43](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/World/AreaCatalog.gd#L37-L43)）

也就是说：

- **开始新游戏**：先决定初始 area → 再把该 area 的 `starting_room` 作为 `starting_map` → 最后 `load_room(starting_map)` 进入该房间

### 5.3 存档会覆盖切入点

当存在存档时，`SaveManager.retrieve_game(self)` 会调用 `Game._set_save_data(data)`，其中会读取 `current_room` 并覆盖 `starting_map`（仅在非 custom_run 时；见 [Game.gd:_set_save_data](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L786-L805)）。

因此：

- **继续游戏**的实际切入点通常来自存档里的 `current_room`
- **开始新游戏**则不会读取存档（存档被删除），切入点来自 `AreaCatalog` 的默认初始章节配置

---

## 6. 多语种（按钮与提示文案）

### 6.1 文案来源

主菜单按钮、语言设置、加载提示、序章 Next/Skip 等文案都通过 `tr("KEY")` 获取（见 [Game.gd:L503-L525](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L503-L525)）。

对应翻译文件在：

- [en.po](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Resources/Translations/en.po)
- [zh_CN.po](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Resources/Translations/zh_CN.po)

并由项目设置注册（见 [project.godot](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/project.godot) 的 `internationalization/locale/translations`）。

### 6.2 语言设置如何生效与持久化

- 语言设置项存储在 `user://display_settings.cfg` 的 `display.language`（读取见 [Game.gd:L455-L464](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L455-L464)，保存见 [Game.gd:L476-L480](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L476-L480)）
- 启动时会应用语言：`TranslationServer.set_locale(...)`（见 [Game.gd:L430-L434](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L430-L434) 和 [Game.gd:L492-L501](file:///D:/Task_Panel/0_AliceJOH/0_AOJ_Reference/workspace/Metroidvania-System-master/CoreEngine/Scripts/Systems/Game.gd#L492-L501)）

---

## 7. 下一步扩展建议（可选）

- **把“序章脚本根目录/章节切入点”数据化**：例如在 `AreaCatalog` 为某个 area 增加 `prologue_id` 或 `prologue_md_root`，而不是写死 `00_0-0_魔宫附近`
- **更精细的脚本解析**：支持 `【舞台】`、`【转场】`、旁白、分镜指令（例如 `@ASSET:` 标签）
- **资源打包**：若要导出可运行包，建议把 `00_序章` 内容完整复制到 `res://000_UserInput/00_序章`，避免依赖本机绝对路径
