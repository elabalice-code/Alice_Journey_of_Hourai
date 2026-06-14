# EventStudio

## 位置

- 工程：`GodotTools/EventStudio/EventStudio/EventStudio.csproj`
- 输出：`GodotTools-Build/EventStudio`

## 编译

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  "D:\Task_Panel\0_AliceJOH\0_AOJ_Reference\workspace\Metroidvania-System-master\GodotTools\EventStudio\EventStudio\EventStudio.csproj" `
  /t:Build /p:Configuration=Release
```

## 数据文件

- 编辑态：`*.events.json`
- 运行时图：`*.runtime.events.json`

## 校验策略

- 保存与导出前自动校验
- 错误会阻断保存/导出
- 警告会二次确认后允许继续

## 示例

- 编辑态示例：`GodotTools/EventStudio/Samples/prologue_flow.events.json`
- 运行时图示例：`GodotTools/EventStudio/Samples/prologue_flow.runtime.events.json`

## Godot 运行时接入

- 运行时事件流：`CoreEngine/Scripts/Actor/RuntimeEventFlowActor.gd`
- 默认读取：`res://GodotTools/EventStudio/Samples/prologue_flow.events.json`
- 可通过 Workbench 数据键覆盖路径：`runtime_event_project_path`
