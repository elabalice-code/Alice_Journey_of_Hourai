# Workbench 架构使用示例

本文件演示如何在当前 Demo 的架构里，使用 Workbench 在对象 A 和对象 B 之间传递数据，并简单说明数值与结算相关的用法。

---

## 1. 核心概念速览

- WorkbenchService  
  - 全局单例节点名：`Workbench`（在 Autoload 里）  
  - 主要接口：  
    - `send(message: Dictionary)`：发送一条消息  
    - 信号 `message_published(message: Dictionary)`：每帧批量派发队列中的消息  
    - 信号 `tick(delta: float)`：每个物理帧触发一次  
    - `register_service(name: StringName, service)`：注册服务  
    - `get_service(name: StringName)` / `get_services(name: StringName)`：获取服务

- ExtensionRegistryService  
  - 全局单例节点名：`Extensions`  
  - 启动时扫描 `res://Extensions/*/extension.gd`，自动加载扩展模块

- Value 系统相关  
  - `ValueStore`：通用数值仓库，提供 `get_value/set_value/apply_delta` 和 `value_changed` 信号  
  - `SettlementEngine`：处理 `value_tx` 交易的结算管线  
  - 默认扩展示例：`Extensions/ValueAndSettlement`

---

## 2. 在对象 A 中发送一条消息给对象 B

对象 A 不需要直接持有 B 的引用，只要约定好消息格式即可。下面示例展示在任意 Node 中发送一条 `spawn_enemy` 消息：

```gdscript
extends Node2D

func _ready() -> void:
	pass

func spawn_enemy_request() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var msg := {}
	msg["type"] = &"spawn_enemy"
	msg["position"] = global_position
	msg["source"] = self
	workbench.send(msg)
```

使用方式：

- 在输入或动画事件里调用 `spawn_enemy_request()`  
- Workbench 会在后续某个物理帧里，把该消息通过 `message_published` 信号广播给所有订阅者

---

## 3. 在对象 B 中订阅并处理来自 Workbench 的消息

对象 B 只需要在 `_ready` 中订阅 `message_published` 信号，并在回调里解析需要的消息类型。

```gdscript
extends Node2D

func _ready() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	workbench.message_published.connect(_on_message)

func _on_message(message: Dictionary) -> void:
	var t: StringName = message.get("type", &"")
	if t != &"spawn_enemy":
		return
	var pos: Vector2 = message.get("position", global_position)
	_spawn_enemy_at(pos)

func _spawn_enemy_at(pos: Vector2) -> void:
	var scene := load("res://SomeEnemy.tscn") as PackedScene
	if scene == null:
		return
	var enemy := scene.instantiate()
	get_tree().current_scene.add_child(enemy)
	enemy.global_position = pos
```

要点：

- A 只负责发送 `{ "type": &"spawn_enemy", ... }`  
- B 只关心 `type == &"spawn_enemy"` 的消息  
- 通过这种消息路由，可以轻松实现一对多、多对多的交互

---

## 4. 使用服务容器共享“工作台数据”

Workbench 同时承担“服务容器”的角色。扩展模块可以注册服务，别的对象可以从 Workbench 里拿到这些服务。

### 4.1 在模块中注册服务

`Extensions/ValueAndSettlement/Scripts/ValueSettlementModule.gd` 的核心逻辑：

```gdscript
func _initialize():
	_workbench = WorkbenchService.get_singleton()
	_store = load("res://Extensions/ValueAndSettlement/Scripts/ValueStore.gd").new()
	_engine = load("res://Extensions/ValueAndSettlement/Scripts/SettlementEngine.gd").new(_store, _workbench)
	_workbench.register_service(&"values", _store)
	_workbench.register_service(&"settlement", _engine)
```

这表示：

- 名为 `"values"` 的服务对应 `ValueStore` 实例  
- 名为 `"settlement"` 的服务对应结算引擎实例

### 4.2 在任意对象中使用 ValueStore

例如某个敌人对象希望直接读取或修改全局数值，可这么写：

```gdscript
extends Node2D

func _ready() -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var store = workbench.get_service(&"values")
	if store == null:
		return
	var hp: float = float(store.get_value(&"player_hp", 100.0))
	_take_initial_action(hp)

func _deal_damage_to_player(amount: float) -> void:
	var workbench := WorkbenchService.get_singleton()
	if workbench == null:
		return
	var msg := {}
	msg["type"] = &"value_tx"
	msg["key"] = &"player_hp"
	msg["delta"] = -amount
	var ctx := {}
	ctx["origin"] = &"Enemy"
	ctx["enemy_path"] = str(get_path())
	msg["context"] = ctx
	workbench.send(msg)
```

这里用的是“消息 + 结算管线”的方式修改数值，所有扣血逻辑可以在 SettlementStages 里统一管理。

---

## 5. 利用 tick 信号做 Actor 定时逻辑

Workbench 每个物理帧会发出 `tick(delta)` 信号，可以把对象的行为写成“订阅 tick 的 Actor”：

```gdscript
extends Node2D

var _workbench
var _timer := 0.0

func _ready() -> void:
	_workbench = WorkbenchService.get_singleton()
	if _workbench == null:
		return
	_workbench.tick.connect(_on_tick)

func _on_tick(delta: float) -> void:
	_timer += delta
	if _timer < 1.0:
		return
	_timer = 0.0
	var msg := {}
	msg["type"] = &"heartbeat"
	msg["source"] = self
	_workbench.send(msg)
```

任何订阅 `message_published` 的对象都可以收到这些 `heartbeat` 消息，实现类似“Actor 定时心跳”的机制。

---

## 6. 和当前 Demo 的关联示例

当前 Demo 中已经有一个实际使用 Workbench 的例子：收集品和数值系统。

### 6.1 Collectible 发出 value_tx 消息

文件：`CoreEngine/Objects/Collectible.tscn` 中的脚本：

```gdscript
func collect(body: Node2D) -> void:
	if not body.is_in_group(&"player"):
		return
	var workbench := WorkbenchService.get_singleton()
	if workbench:
		var msg := {}
		msg["type"] = &"value_tx"
		msg["key"] = &"collectibles"
		msg["delta"] = 1
		var ctx := {}
		ctx["origin"] = &"Collectible"
		ctx["node"] = str(get_path())
		msg["context"] = ctx
		workbench.send(msg)
	else:
		Game.get_singleton().collectibles += 1
	MetSys.store_object(self)
	queue_free()
```

### 6.2 ValueSettlementModule 作为“Actor”处理消息

`ValueSettlementModule` 订阅 Workbench 的消息，并把 `value_tx` 交给结算引擎处理：

```gdscript
func _initialize():
	_workbench = WorkbenchService.get_singleton()
	_store = load("res://Extensions/ValueAndSettlement/Scripts/ValueStore.gd").new()
	_engine = load("res://Extensions/ValueAndSettlement/Scripts/SettlementEngine.gd").new(_store, _workbench)
	_workbench.register_service(&"values", _store)
	_workbench.register_service(&"settlement", _engine)
	_register_stages()
	_workbench.message_published.connect(_on_message)
	_workbench.tick.connect(_on_tick)
	_store.value_changed.connect(_on_value_changed)
	_store.set_value(&"collectibles", float(game.collectibles), {"origin": &"init"})
```

`_on_message` 里，对 `value_tx` 进行归一化后入队，`_on_tick` 每帧调用 `_engine.flush()`，完成一个“Actor + Reactive” 风格的结算流水线。

---

## 7. 建议的使用模式小结

- 对象 A 和 B 之间的直接交互优先走 Workbench 消息  
- 需要共享的“工作台数据”封装成服务，并通过 `register_service/get_service` 暴露  
- 所有需要统一管理的数值修改尽量走 `value_tx`，由 SettlementEngine + Stages 组合完成  
- 需要定时行为时订阅 Workbench 的 `tick` 信号，把对象当作独立 Actor 来写

按照这些约定，你可以在此基础上继续扩展更多“系统文件夹”（新的 Extension），每个系统只需要关心：

- 自己在 Workbench 上发送和接收什么消息  
- 自己注册了哪些服务给其它系统使用  
- 自己在结算流水线中插入了哪些 Stage

