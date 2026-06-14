# EventStudioReplay

- 默认模式：驱动 `GodotTools-Build/EventStudio/net8.0-windows/EventStudio.exe` 原界面
- Server 工程：`GodotTools/EventStudioReplay/Server/EventStudioReplayServer.csproj`
- Client 工程：`GodotTools/EventStudioReplay/Client/EventStudioReplayClient.csproj`
- Headless Server 工程：`GodotTools/EventStudioReplay/Host/EventStudioReplayHost.csproj`
- 脚本示例：`GodotTools/EventStudioReplay/DemoScripts/start_game_demo.replay.txt`
- 通信：本机命名管道 `EventStudioReplayPipe`

构建后输出：

- `GodotTools-Build/EventStudioReplayServer/net8.0-windows/EventStudioReplayServer.exe`
- `GodotTools-Build/EventStudioReplayClient/net8.0/EventStudioReplayClient.exe`
- `GodotTools-Build/EventStudioReplayHost/net8.0/EventStudioReplayHost.exe`

最小用法：

```powershell
$server = "D:\Task_Panel\0_AliceJOH\0_AOJ_Reference\workspace\Metroidvania-System-master\GodotTools-Build\EventStudio\net8.0-windows\EventStudio.exe"
$cli = "D:\Task_Panel\0_AliceJOH\0_AOJ_Reference\workspace\Metroidvania-System-master\GodotTools-Build\EventStudioReplayClient\net8.0\EventStudioReplayClient.exe"

& $server
# 新终端执行
& $cli ping
& $cli list-controls
```
