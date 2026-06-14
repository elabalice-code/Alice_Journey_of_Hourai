using System.IO.Pipes;
using System.Text;
using EventStudioReplay.Shared;

namespace EventStudioReplayServer;

public sealed class MainForm : Form
{
    private readonly TextBox _txtProjectName = new() { Name = "txtProjectName", Dock = DockStyle.Fill, Text = "PrologueFlow" };
    private readonly TextBox _txtStartEvent = new() { Name = "txtStartEvent", Dock = DockStyle.Fill, Text = "evt_prologue_start" };
    private readonly TextBox _txtLastExport = new() { Name = "txtLastExport", Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _txtGuide = new() { Name = "txtGuide", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _txtLog = new() { Name = "txtLog", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _lblPipe = new() { AutoSize = true, Text = "Pipe: starting..." };
    private readonly Label _lblDialog = new() { AutoSize = true, Text = "Dialog: none" };

    private ReplayInputDialog? _dialog;
    private CancellationTokenSource? _pipeCts;
    private Task? _pipeTask;
    private string _activeDialog = "";
    private readonly List<string> _events = [];

    public MainForm()
    {
        Name = "main";
        Text = "EventStudio Replay Server";
        Width = 1200;
        Height = 780;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        RefreshGuide();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        StartPipeServer();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _pipeCts?.Cancel();
        _dialog?.Close();
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        var head = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        head.Controls.Add(_lblPipe);
        head.Controls.Add(new Label { AutoSize = true, Text = "    " });
        head.Controls.Add(_lblDialog);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        center.Controls.Add(BuildActionPanel(), 0, 0);
        center.Controls.Add(_txtGuide, 1, 0);

        var logPanel = new GroupBox { Dock = DockStyle.Fill, Text = "日志" };
        logPanel.Controls.Add(_txtLog);

        root.Controls.Add(head, 0, 0);
        root.Controls.Add(center, 0, 1);
        root.Controls.Add(logPanel, 0, 2);
        Controls.Add(root);
    }

    private Control BuildActionPanel()
    {
        var group = new GroupBox { Dock = DockStyle.Fill, Text = "事件编辑演示面板" };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { AutoSize = true, Text = "工程名", Anchor = AnchorStyles.Left }, 0, 0);
        panel.Controls.Add(_txtProjectName, 1, 0);
        panel.Controls.Add(CreateButton("btnNewProject", "新建工程", (_, _) => OpenInputDialog("dlgNewProject", "新建工程名", _txtProjectName.Text, v => _txtProjectName.Text = v)), 2, 0);

        panel.Controls.Add(new Label { AutoSize = true, Text = "StartEvent", Anchor = AnchorStyles.Left }, 0, 1);
        panel.Controls.Add(_txtStartEvent, 1, 1);
        panel.Controls.Add(CreateButton("btnStartEvent", "设置开始事件", (_, _) => OpenInputDialog("dlgStartEvent", "开始事件ID", _txtStartEvent.Text, v => _txtStartEvent.Text = v)), 2, 1);

        panel.Controls.Add(new Label { AutoSize = true, Text = "事件操作", Anchor = AnchorStyles.Left }, 0, 2);
        panel.Controls.Add(new TextBox { Name = "txtEventHint", ReadOnly = true, Dock = DockStyle.Fill, Text = "从“开始游戏”创建主线起点并串接对白结束切图" }, 1, 2);
        panel.Controls.Add(CreateButton("btnAddFlow", "添加演示事件流", (_, _) => AddDemoFlow()), 2, 2);

        panel.Controls.Add(new Label { AutoSize = true, Text = "导出", Anchor = AnchorStyles.Left }, 0, 3);
        panel.Controls.Add(_txtLastExport, 1, 3);
        panel.Controls.Add(CreateButton("btnValidateExport", "校验并导出", (_, _) => ValidateAndExport()), 2, 3);

        panel.Controls.Add(new Label { AutoSize = true, Text = "对话框", Anchor = AnchorStyles.Left }, 0, 4);
        panel.Controls.Add(new TextBox { Name = "txtDialogHint", ReadOnly = true, Dock = DockStyle.Fill, Text = "全部使用可回放子窗体，不用 MessageBox" }, 1, 4);
        panel.Controls.Add(CreateButton("btnCloseDialog", "关闭活动对话框", (_, _) => CloseDialog()), 2, 4);

        group.Controls.Add(panel);
        return group;
    }

    private Button CreateButton(string name, string text, EventHandler click)
    {
        var btn = new Button { Name = name, Text = text, Dock = DockStyle.Fill };
        btn.Click += click;
        return btn;
    }

    private void AddDemoFlow()
    {
        _events.Clear();
        _events.Add("evt_prologue_start -> StartDialogue(dlg_prologue_0001) -> evt_after_dialogue");
        _events.Add("evt_after_dialogue -> ChangeMap(res://CoreEngine/Maps/DiceRoom.tscn)");
        AppendLog("已添加演示事件流。");
    }

    private void ValidateAndExport()
    {
        if (string.IsNullOrWhiteSpace(_txtProjectName.Text))
        {
            AppendLog("校验失败：工程名为空。");
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtStartEvent.Text))
        {
            AppendLog("校验失败：StartEvent 为空。");
            return;
        }
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "demo.runtime.events.json");
        var lines = new StringBuilder();
        lines.AppendLine("{");
        lines.AppendLine("  \"project\": \"" + _txtProjectName.Text.Replace("\"", "\\\"") + "\",");
        lines.AppendLine("  \"startEventId\": \"" + _txtStartEvent.Text.Replace("\"", "\\\"") + "\",");
        lines.AppendLine("  \"events\": [");
        for (var i = 0; i < _events.Count; i++)
        {
            var tail = i == _events.Count - 1 ? "" : ",";
            lines.AppendLine("    \"" + _events[i].Replace("\"", "\\\"") + "\"" + tail);
        }
        lines.AppendLine("  ]");
        lines.AppendLine("}");
        File.WriteAllText(output, lines.ToString(), Encoding.UTF8);
        _txtLastExport.Text = output;
        AppendLog("导出成功：" + output);
    }

    private void OpenInputDialog(string dialogName, string title, string currentValue, Action<string> onConfirm)
    {
        CloseDialog();
        _dialog = new ReplayInputDialog(dialogName, title, currentValue);
        _activeDialog = dialogName;
        _lblDialog.Text = "Dialog: " + _activeDialog;
        _dialog.Confirmed += (_, value) =>
        {
            onConfirm(value);
            AppendLog("已更新: " + title + " = " + value);
            CloseDialog();
        };
        _dialog.Canceled += (_, _) => CloseDialog();
        _dialog.Show(this);
    }

    private void CloseDialog()
    {
        if (_dialog != null)
        {
            if (!_dialog.IsDisposed)
            {
                _dialog.Close();
            }
            _dialog = null;
        }
        _activeDialog = "";
        _lblDialog.Text = "Dialog: none";
    }

    private void RefreshGuide()
    {
        _txtGuide.Text = string.Join(Environment.NewLine, GetAvailableTargets());
    }

    private IEnumerable<string> GetAvailableTargets()
    {
        yield return "main.btnNewProject";
        yield return "main.btnStartEvent";
        yield return "main.btnAddFlow";
        yield return "main.btnValidateExport";
        yield return "main.btnCloseDialog";
        yield return "main.txtProjectName";
        yield return "main.txtStartEvent";
        yield return "main.txtLastExport";
        yield return "main.txtLog";
        yield return "dialog.txtValue";
        yield return "dialog.btnConfirm";
        yield return "dialog.btnCancel";
    }

    private void AppendLog(string message)
    {
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void StartPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        _pipeTask = Task.Run(() => PipeLoopAsync(_pipeCts.Token));
        _lblPipe.Text = "Pipe: listening " + ReplayProtocol.PipeName;
    }

    private async Task PipeLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(ReplayProtocol.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(ct);
            var reqLine = await ReplayProtocol.ReadLineAsync(server, ct);
            var req = ReplayProtocol.Deserialize<ReplayRequest>(reqLine);
            var response = await ExecuteRequestAsync(req);
            var json = ReplayProtocol.Serialize(response);
            await ReplayProtocol.WriteLineAsync(server, json, ct);
        }
    }

    private Task<ReplayResponse> ExecuteRequestAsync(ReplayRequest req)
    {
        if (InvokeRequired)
        {
            var tcs = new TaskCompletionSource<ReplayResponse>();
            BeginInvoke(new Action(async () =>
            {
                try
                {
                    tcs.SetResult(await ExecuteRequestAsync(req));
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new ReplayResponse { Success = false, Message = ex.Message, ActiveDialog = _activeDialog });
                }
            }));
            return tcs.Task;
        }

        var cmd = req.Command.Trim().ToLowerInvariant();
        switch (cmd)
        {
            case "ping":
                return Task.FromResult(new ReplayResponse { Success = true, Message = "alive", ActiveDialog = _activeDialog });
            case "list":
            case "list-controls":
                RefreshGuide();
                return Task.FromResult(new ReplayResponse { Success = true, Message = string.Join(Environment.NewLine, GetAvailableTargets()), ActiveDialog = _activeDialog });
            case "click":
                return Task.FromResult(ClickTarget(req.Target));
            case "set-text":
                return Task.FromResult(SetText(req.Target, req.Value));
            case "focus":
            case "activate":
                return Task.FromResult(FocusTarget(req.Target));
            case "close-dialog":
                CloseDialog();
                return Task.FromResult(new ReplayResponse { Success = true, Message = "dialog closed", ActiveDialog = _activeDialog });
            default:
                return Task.FromResult(new ReplayResponse { Success = false, Message = "unsupported command: " + req.Command, ActiveDialog = _activeDialog });
        }
    }

    private ReplayResponse FocusTarget(string target)
    {
        var c = FindControl(target);
        if (c == null)
        {
            return new ReplayResponse { Success = false, Message = "target not found: " + target, ActiveDialog = _activeDialog };
        }
        c.Focus();
        return new ReplayResponse { Success = true, Message = "focused " + target, ActiveDialog = _activeDialog };
    }

    private ReplayResponse ClickTarget(string target)
    {
        var c = FindControl(target);
        if (c is Button b)
        {
            b.PerformClick();
            return new ReplayResponse { Success = true, Message = "clicked " + target, ActiveDialog = _activeDialog };
        }
        return new ReplayResponse { Success = false, Message = "button target not found: " + target, ActiveDialog = _activeDialog };
    }

    private ReplayResponse SetText(string target, string value)
    {
        var c = FindControl(target);
        if (c is TextBox tb)
        {
            tb.Text = value;
            return new ReplayResponse { Success = true, Message = "set-text " + target, ActiveDialog = _activeDialog };
        }
        return new ReplayResponse { Success = false, Message = "textbox target not found: " + target, ActiveDialog = _activeDialog };
    }

    private Control? FindControl(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }
        var t = target.Trim();
        if (t.StartsWith("main.", StringComparison.OrdinalIgnoreCase))
        {
            return Controls.Find(t["main.".Length..], true).FirstOrDefault();
        }
        if (t.StartsWith("dialog.", StringComparison.OrdinalIgnoreCase) && _dialog != null && !_dialog.IsDisposed)
        {
            return _dialog.Controls.Find(t["dialog.".Length..], true).FirstOrDefault();
        }
        return Controls.Find(t, true).FirstOrDefault();
    }
}

public sealed class ReplayInputDialog : Form
{
    public event EventHandler<string>? Confirmed;
    public event EventHandler? Canceled;

    private readonly TextBox _txtValue = new() { Name = "txtValue", Dock = DockStyle.Fill };
    private readonly Button _btnConfirm = new() { Name = "btnConfirm", Text = "确认", Dock = DockStyle.Fill };
    private readonly Button _btnCancel = new() { Name = "btnCancel", Text = "取消", Dock = DockStyle.Fill };

    public ReplayInputDialog(string name, string title, string defaultValue)
    {
        Name = name;
        Text = title;
        Width = 460;
        Height = 180;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        _txtValue.Text = defaultValue;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.Controls.Add(_txtValue, 0, 0);
        root.SetColumnSpan(_txtValue, 2);
        root.Controls.Add(_btnConfirm, 0, 1);
        root.Controls.Add(_btnCancel, 1, 1);
        Controls.Add(root);

        _btnConfirm.Click += (_, _) => Confirmed?.Invoke(this, _txtValue.Text);
        _btnCancel.Click += (_, _) => Canceled?.Invoke(this, EventArgs.Empty);
    }
}
