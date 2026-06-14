using System.ComponentModel;
using System.Text.Json;
using EventStudio.IO;
using EventStudio.Models;
using EventStudio.Replay;

namespace EventStudio;

public sealed class MainForm : Form
{
    private EventProject _project = new();
    private string? _currentPath;
    private bool _dirty;

    private readonly SplitContainer _root = new() { Dock = DockStyle.Fill, SplitterDistance = 320 };
    private readonly ListBox _eventList = new() { Dock = DockStyle.Fill };
    private readonly PropertyGrid _eventGrid = new() { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true };
    private readonly DataGridView _triggerGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DataGridView _actionGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly PropertyGrid _projectGrid = new() { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true };
    private readonly MenuStrip _menu = new();
    private readonly ToolStripMenuItem _fileMenu = new("文件");
    private readonly ToolStripMenuItem _editMenu = new("编辑");
    private readonly TabControl _rightTabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _eventTab = new("事件");
    private readonly TabPage _triggerTab = new("触发器");
    private readonly TabPage _actionTab = new("动作");
    private readonly TabPage _projectTab = new("项目");
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusText = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Dictionary<string, string> _replayInputs = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _replayCts;
    private Task? _replayTask;
    private VirtualCursorForm? _virtualCursor;

    public MainForm()
    {
        Text = "事件编辑器";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1400;
        Height = 900;

        BuildMenu();
        BuildLayout();
        BindProject();
        InitializeReplayInputs();
        UpdateTitle();
    }

    private void BuildMenu()
    {
        _fileMenu.DropDownItems.Add("新建", null, (_, _) => NewProject());
        _fileMenu.DropDownItems.Add("打开...", null, (_, _) => OpenProject());
        _fileMenu.DropDownItems.Add("保存", null, (_, _) => SaveProject());
        _fileMenu.DropDownItems.Add("另存为...", null, (_, _) => SaveProjectAs());
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _fileMenu.DropDownItems.Add("导出运行时图...", null, (_, _) => ExportRuntimeGraph());
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _fileMenu.DropDownItems.Add("退出", null, (_, _) => Close());

        _editMenu.DropDownItems.Add("新增事件", null, (_, _) => AddEvent());
        _editMenu.DropDownItems.Add("删除事件", null, (_, _) => RemoveSelectedEvent());
        _editMenu.DropDownItems.Add(new ToolStripSeparator());
        _editMenu.DropDownItems.Add("新增触发器", null, (_, _) => AddTrigger());
        _editMenu.DropDownItems.Add("删除触发器", null, (_, _) => RemoveTrigger());
        _editMenu.DropDownItems.Add("新增动作", null, (_, _) => AddAction());
        _editMenu.DropDownItems.Add("删除动作", null, (_, _) => RemoveAction());
        _editMenu.DropDownItems.Add(new ToolStripSeparator());
        _editMenu.DropDownItems.Add("校验项目", null, (_, _) => ValidateProjectAndReport());

        _menu.Items.AddRange([_fileMenu, _editMenu]);
        MainMenuStrip = _menu;
        Controls.Add(_menu);
    }

    private void BuildLayout()
    {
        _eventList.DisplayMember = nameof(EventNode.Title);
        _eventList.SelectedIndexChanged += (_, _) => BindCurrentEvent();
        _eventList.ContextMenuStrip = BuildEventContextMenu();
        _root.Panel1.Controls.Add(_eventList);

        _eventTab.Controls.Add(_eventGrid);
        _triggerTab.Controls.Add(_triggerGrid);
        _actionTab.Controls.Add(_actionGrid);
        _projectTab.Controls.Add(_projectGrid);

        _rightTabs.TabPages.AddRange([_eventTab, _triggerTab, _actionTab, _projectTab]);
        _root.Panel2.Controls.Add(_rightTabs);

        SetupTriggerGrid();
        SetupActionGrid();

        _status.Items.Add(_statusText);
        Controls.Add(_root);
        Controls.Add(_status);
    }

    private ContextMenuStrip BuildEventContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("新增事件", null, (_, _) => AddEvent());
        menu.Items.Add("删除事件", null, (_, _) => RemoveSelectedEvent());
        return menu;
    }

    private void SetupTriggerGrid()
    {
        _triggerGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "类型",
            DataPropertyName = nameof(TriggerRule.Type),
            DataSource = Enum.GetValues(typeof(TriggerType))
        });
        _triggerGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "来源域",
            DataPropertyName = nameof(TriggerRule.SourceDomain),
            DataSource = Enum.GetValues(typeof(EventDomain))
        });
        _triggerGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "信号", DataPropertyName = nameof(TriggerRule.Signal), Width = 180 });
        _triggerGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "条件表达式", DataPropertyName = nameof(TriggerRule.ConditionExpr), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _triggerGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "去抖ms", DataPropertyName = nameof(TriggerRule.DebounceMs), Width = 90 });
        _triggerGrid.CellValueChanged += (_, _) => MarkDirty();
        _triggerGrid.UserDeletedRow += (_, _) => MarkDirty();
    }

    private void SetupActionGrid()
    {
        _actionGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "动作",
            DataPropertyName = nameof(DispatchAction.Type),
            DataSource = Enum.GetValues(typeof(DispatchActionType))
        });
        _actionGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "目标事件ID", DataPropertyName = nameof(DispatchAction.TargetEventId), Width = 170 });
        _actionGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "延迟ms", DataPropertyName = nameof(DispatchAction.DelayMs), Width = 90 });
        _actionGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Payload(JSON)", DataPropertyName = nameof(DispatchAction.PayloadJson), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _actionGrid.CellValueChanged += (_, _) => MarkDirty();
        _actionGrid.UserDeletedRow += (_, _) => MarkDirty();
    }

    private void BindProject()
    {
        _projectGrid.SelectedObject = _project;
        _projectGrid.PropertyValueChanged += (_, e) =>
        {
            if (e.ChangedItem?.Label == nameof(EventProject.StartEventId))
            {
                BindEventList();
            }
            MarkDirty();
        };
        BindEventList();
        BindCurrentEvent();
        UpdateStatus();
    }

    private void BindEventList()
    {
        var selected = (_eventList.SelectedItem as EventNode)?.Id;
        _eventList.DataSource = null;
        _eventList.DataSource = _project.Events;
        _eventList.DisplayMember = nameof(EventNode.Title);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            var idx = _project.Events.FindIndex(x => x.Id == selected);
            if (idx >= 0)
            {
                _eventList.SelectedIndex = idx;
            }
        }
        if (_eventList.SelectedIndex < 0 && _project.Events.Count > 0)
        {
            _eventList.SelectedIndex = 0;
        }
    }

    private void BindCurrentEvent()
    {
        var evt = _eventList.SelectedItem as EventNode;
        _replayInputs["selectedEventId"] = evt?.Id ?? "";
        _eventGrid.SelectedObject = evt;
        if (evt == null)
        {
            _triggerGrid.DataSource = null;
            _actionGrid.DataSource = null;
            return;
        }

        _eventGrid.PropertyValueChanged -= EventGrid_PropertyValueChanged;
        _eventGrid.PropertyValueChanged += EventGrid_PropertyValueChanged;

        _triggerGrid.DataSource = new BindingSource(new BindingList<TriggerRule>(evt.Triggers), null);
        _actionGrid.DataSource = new BindingSource(new BindingList<DispatchAction>(evt.Actions), null);
        UpdateStatus();
    }

    private void EventGrid_PropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
    {
        if (e.ChangedItem?.Label == nameof(EventNode.Title))
        {
            BindEventList();
        }
        MarkDirty();
    }

    private void AddEvent()
    {
        var node = _project.CreateEvent();
        BindEventList();
        var idx = _project.Events.FindIndex(x => x.Id == node.Id);
        if (idx >= 0)
        {
            _eventList.SelectedIndex = idx;
        }
        MarkDirty();
    }

    private void RemoveSelectedEvent()
    {
        RemoveSelectedEventCore(requireConfirm: true);
    }

    private bool RemoveSelectedEventCore(bool requireConfirm)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return false;
        }

        if (requireConfirm)
        {
            var yes = MessageBox.Show($"删除事件 {evt.Title}({evt.Id}) ?", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (yes != DialogResult.Yes)
            {
                return false;
            }
        }

        _project.Events.Remove(evt);
        foreach (var node in _project.Events)
        {
            node.Actions.RemoveAll(x => string.Equals(x.TargetEventId, evt.Id, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(_project.StartEventId, evt.Id, StringComparison.OrdinalIgnoreCase))
        {
            _project.StartEventId = _project.Events.FirstOrDefault()?.Id ?? "";
        }

        BindEventList();
        BindCurrentEvent();
        MarkDirty();
        return true;
    }

    private void AddTrigger()
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return;
        }
        evt.Triggers.Add(new TriggerRule());
        BindCurrentEvent();
        MarkDirty();
    }

    private void RemoveTrigger()
    {
        if (_eventList.SelectedItem is not EventNode evt || _triggerGrid.CurrentRow == null)
        {
            return;
        }
        var idx = _triggerGrid.CurrentRow.Index;
        if (idx < 0 || idx >= evt.Triggers.Count)
        {
            return;
        }
        evt.Triggers.RemoveAt(idx);
        BindCurrentEvent();
        MarkDirty();
    }

    private void AddAction()
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return;
        }
        evt.Actions.Add(new DispatchAction());
        BindCurrentEvent();
        MarkDirty();
    }

    private void RemoveAction()
    {
        if (_eventList.SelectedItem is not EventNode evt || _actionGrid.CurrentRow == null)
        {
            return;
        }
        var idx = _actionGrid.CurrentRow.Index;
        if (idx < 0 || idx >= evt.Actions.Count)
        {
            return;
        }
        evt.Actions.RemoveAt(idx);
        BindCurrentEvent();
        MarkDirty();
    }

    private void NewProject()
    {
        if (!ConfirmSaveBeforeLoss())
        {
            return;
        }
        ResetProject();
    }

    private void OpenProject()
    {
        if (!ConfirmSaveBeforeLoss())
        {
            return;
        }

        using var dlg = new OpenFileDialog
        {
            Filter = "Event Project (*.events.json)|*.events.json|JSON (*.json)|*.json|All Files (*.*)|*.*",
            Title = "打开事件工程"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        OpenProjectFromPath(dlg.FileName);
    }

    private void SaveProject()
    {
        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            SaveProjectAs();
            return;
        }
        Persist(_currentPath!);
    }

    private void SaveProjectAs()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Event Project (*.events.json)|*.events.json|JSON (*.json)|*.json|All Files (*.*)|*.*",
            Title = "保存事件工程",
            FileName = $"{_project.Name.Replace(' ', '_')}.events.json"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        Persist(dlg.FileName);
    }

    private void Persist(string path)
    {
        if (!ValidateBeforeSaveOrExport())
        {
            return;
        }
        EventProjectStore.Save(path, _project);
        _currentPath = path;
        _dirty = false;
        UpdateTitle();
        UpdateStatus("已保存");
    }

    private void ExportRuntimeGraph()
    {
        if (!ValidateBeforeSaveOrExport())
        {
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "Runtime Event Graph (*.runtime.events.json)|*.runtime.events.json|JSON (*.json)|*.json",
            Title = "导出运行时图",
            FileName = $"{_project.Name.Replace(' ', '_')}.runtime.events.json"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        var graph = EventProjectStore.BuildRuntimeGraph(_project);
        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(dlg.FileName, json);
        UpdateStatus("已导出运行时图");
    }

    private bool ValidateBeforeSaveOrExport()
    {
        var issues = EventProjectStore.ValidateProject(_project);
        var errors = issues.Where(x => x.Severity == ValidationSeverity.Error).ToList();
        var warnings = issues.Where(x => x.Severity == ValidationSeverity.Warning).ToList();
        if (errors.Count == 0 && warnings.Count == 0)
        {
            return true;
        }

        if (errors.Count > 0)
        {
            ShowValidationResult(issues, "校验失败：存在错误，已阻止保存/导出。");
            return false;
        }

        var msg = BuildValidationText(issues, "校验通过：仅包含警告。是否继续保存/导出？");
        var result = MessageBox.Show(msg, "项目校验", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return result == DialogResult.Yes;
    }

    private void ValidateProjectAndReport()
    {
        var issues = EventProjectStore.ValidateProject(_project);
        if (issues.Count == 0)
        {
            MessageBox.Show("校验通过：未发现问题。", "项目校验", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ShowValidationResult(issues, "发现以下问题：");
    }

    private void ShowValidationResult(List<ValidationIssue> issues, string title)
    {
        var msg = BuildValidationText(issues, title);
        var icon = issues.Any(x => x.Severity == ValidationSeverity.Error) ? MessageBoxIcon.Error : MessageBoxIcon.Warning;
        MessageBox.Show(msg, "项目校验", MessageBoxButtons.OK, icon);
    }

    private static string BuildValidationText(List<ValidationIssue> issues, string title)
    {
        var lines = new List<string> { title, "" };
        foreach (var issue in issues.Take(20))
        {
            var tag = issue.Severity == ValidationSeverity.Error ? "ERROR" : "WARN";
            lines.Add($"[{tag}] {issue.Code} - {issue.Message}");
        }

        if (issues.Count > 20)
        {
            lines.Add($"... 其余 {issues.Count - 20} 条问题已省略");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private bool ConfirmSaveBeforeLoss()
    {
        if (!_dirty)
        {
            return true;
        }
        var result = MessageBox.Show("当前修改尚未保存，是否先保存？", "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (result == DialogResult.Cancel)
        {
            return false;
        }
        if (result == DialogResult.Yes)
        {
            SaveProject();
            return !_dirty;
        }
        return true;
    }

    private void MarkDirty()
    {
        _dirty = true;
        UpdateTitle();
        UpdateStatus();
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrWhiteSpace(_currentPath) ? "未命名.events.json" : Path.GetFileName(_currentPath);
        Text = $"{(_dirty ? "*" : "")}事件编辑器 - {fileName}";
    }

    private void UpdateStatus(string? text = null)
    {
        var total = _project.Events.Count;
        var triggers = _project.Events.Sum(x => x.Triggers.Count);
        var actions = _project.Events.Sum(x => x.Actions.Count);
        _statusText.Text = string.IsNullOrWhiteSpace(text)
            ? $"事件 {total} | 触发器 {triggers} | 动作 {actions} | StartEvent: {_project.StartEventId}"
            : $"{text} | 事件 {total} | 触发器 {triggers} | 动作 {actions}";
    }

    private void InitializeReplayInputs()
    {
        _replayInputs["openPath"] = "";
        _replayInputs["savePath"] = "";
        _replayInputs["exportPath"] = "";
        _replayInputs["selectedEventId"] = "";
    }

    private void ResetProject()
    {
        _project = new EventProject();
        _project.CreateEvent();
        _currentPath = null;
        _dirty = false;
        BindProject();
        UpdateTitle();
    }

    private void OpenProjectFromPath(string path)
    {
        _project = EventProjectStore.Load(path);
        if (_project.Events.Count == 0)
        {
            _project.CreateEvent();
        }
        _currentPath = path;
        _dirty = false;
        BindProject();
        UpdateTitle();
    }

    private bool ExportRuntimeGraphToPath(string path, out string message)
    {
        if (!ValidateBeforeSaveOrExport())
        {
            message = "校验失败，导出已中止。";
            return false;
        }
        var graph = EventProjectStore.BuildRuntimeGraph(_project);
        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(path, json);
        UpdateStatus("已导出运行时图");
        message = $"已导出: {path}";
        return true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        StartReplayBridge();
    }

    private void StartReplayBridge()
    {
        if (_replayTask != null)
        {
            return;
        }
        _replayCts = new CancellationTokenSource();
        _replayTask = Task.Run(() => ReplayLoopAsync(_replayCts.Token));
        UpdateStatus($"Replayer已监听: {ReplayBridge.PipeName}");
    }

    private async Task ReplayLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = ReplayBridge.CreateServer();
                await server.WaitForConnectionAsync(ct);
                var line = await ReplayBridge.ReadLineAsync(server, ct);
                var req = ReplayBridge.Deserialize<ReplayRequest>(line);
                var resp = await ExecuteReplayRequestAsync(req);
                var json = ReplayBridge.Serialize(resp);
                await ReplayBridge.WriteLineAsync(server, json, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task<ReplayResponse> ExecuteReplayRequestAsync(ReplayRequest request)
    {
        if (InvokeRequired)
        {
            var tcs = new TaskCompletionSource<ReplayResponse>();
            BeginInvoke(new Action(async () =>
            {
                try
                {
                    var resp = await ExecuteReplayRequestAsync(request);
                    tcs.TrySetResult(resp);
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(ReplayFail(ex.Message));
                }
            }));
            return await tcs.Task;
        }

        var command = request.Command.Trim().ToLowerInvariant();
        return command switch
        {
            "ping" => ReplayOk("alive"),
            "list" or "list-controls" => ReplayOk(string.Join(Environment.NewLine, GetReplayTargets())),
            "set-text" => await HandleReplaySetTextAsync(request.Target, request.Value, request.DurationMs),
            "click" => await HandleReplayClickAsync(request.Target, request.DurationMs),
            "focus" or "activate" => await HandleReplayFocusAsync(request.Target, request.DurationMs),
            "close-dialog" => ReplayOk("no dialog"),
            _ => ReplayFail("unsupported command: " + request.Command)
        };
    }

    private static string[] GetReplayTargets()
    {
        return
        [
            "main.btnNewProject",
            "main.btnOpenProject",
            "main.btnSaveProject",
            "main.btnSaveAs",
            "main.btnExportRuntime",
            "main.btnAddEvent",
            "main.btnDeleteEvent",
            "main.btnAddTrigger",
            "main.btnDeleteTrigger",
            "main.btnAddAction",
            "main.btnDeleteAction",
            "main.btnValidateProject",
            "main.btnSelectEvent",
            "main.txtOpenPath",
            "main.txtSavePath",
            "main.txtExportPath",
            "main.txtSelectedEventId",
            "main.txtCurrentEventId",
            "main.txtCurrentEventTitle",
            "main.txtCurrentTriggerType",
            "main.txtCurrentTriggerSourceDomain",
            "main.txtCurrentTriggerSignal",
            "main.txtCurrentTriggerCondition",
            "main.txtCurrentActionType",
            "main.txtCurrentActionTargetEventId",
            "main.txtCurrentActionPayload",
            "main.txtProjectName",
            "main.txtStartEventId"
        ];
    }

    private async Task<ReplayResponse> HandleReplaySetTextAsync(string target, string value, int durationMs)
    {
        SelectReplayContext(target);
        if (NeedsReplayAnimation(target))
        {
            await AnimateReplayPointerAsync(target, durationMs, press: false);
        }

        ReplayResponse response;
        switch (target.Trim())
        {
            case "main.txtOpenPath":
                _replayInputs["openPath"] = value;
                response = ReplayOk("set open path");
                break;
            case "main.txtSavePath":
                _replayInputs["savePath"] = value;
                response = ReplayOk("set save path");
                break;
            case "main.txtExportPath":
                _replayInputs["exportPath"] = value;
                response = ReplayOk("set export path");
                break;
            case "main.txtSelectedEventId":
                _replayInputs["selectedEventId"] = value;
                response = ReplayOk("set selected event id");
                break;
            case "main.txtCurrentEventId":
                response = SetCurrentEventId(value);
                break;
            case "main.txtCurrentEventTitle":
                response = SetCurrentEventTitle(value);
                break;
            case "main.txtCurrentTriggerType":
                response = SetCurrentTriggerType(value);
                break;
            case "main.txtCurrentTriggerSourceDomain":
                response = SetCurrentTriggerSourceDomain(value);
                break;
            case "main.txtCurrentTriggerSignal":
                response = SetCurrentTriggerSignal(value);
                break;
            case "main.txtCurrentTriggerCondition":
                response = SetCurrentTriggerCondition(value);
                break;
            case "main.txtCurrentActionType":
                response = SetCurrentActionType(value);
                break;
            case "main.txtCurrentActionTargetEventId":
                response = SetCurrentActionTargetEventId(value);
                break;
            case "main.txtCurrentActionPayload":
                response = SetCurrentActionPayload(value);
                break;
            case "main.txtProjectName":
                _project.Name = value;
                MarkDirty();
                response = ReplayOk("set project name");
                break;
            case "main.txtStartEventId":
                _project.StartEventId = value;
                BindEventList();
                MarkDirty();
                response = ReplayOk("set start event id");
                break;
            default:
                response = ReplayFail("target not found: " + target);
                break;
        }

        RefreshReplaySurface();
        await Task.Delay(160);
        return response;
    }

    private async Task<ReplayResponse> HandleReplayClickAsync(string target, int durationMs)
    {
        SelectReplayContext(target);
        await AnimateReplayPointerAsync(target, durationMs, press: true);

        ReplayResponse response;
        switch (target.Trim())
        {
            case "main.btnNewProject":
                ResetProject();
                response = ReplayOk("new project");
                break;
            case "main.btnOpenProject":
                if (!_replayInputs.TryGetValue("openPath", out var openPath) || string.IsNullOrWhiteSpace(openPath))
                {
                    return ReplayFail("open path is empty");
                }
                if (!File.Exists(openPath))
                {
                    return ReplayFail("open path not found: " + openPath);
                }
                OpenProjectFromPath(openPath);
                response = ReplayOk("opened project");
                break;
            case "main.btnSaveProject":
                if (string.IsNullOrWhiteSpace(_currentPath))
                {
                    if (!_replayInputs.TryGetValue("savePath", out var save1) || string.IsNullOrWhiteSpace(save1))
                    {
                        return ReplayFail("current path empty, save path is required");
                    }
                    Persist(save1);
                    response = ReplayOk("saved project");
                    break;
                }
                Persist(_currentPath);
                response = ReplayOk("saved project");
                break;
            case "main.btnSaveAs":
                if (!_replayInputs.TryGetValue("savePath", out var savePath) || string.IsNullOrWhiteSpace(savePath))
                {
                    return ReplayFail("save path is empty");
                }
                Persist(savePath);
                response = ReplayOk("saved as");
                break;
            case "main.btnExportRuntime":
                if (!_replayInputs.TryGetValue("exportPath", out var exportPath) || string.IsNullOrWhiteSpace(exportPath))
                {
                    return ReplayFail("export path is empty");
                }
                response = ExportRuntimeGraphToPath(exportPath, out var exportMsg) ? ReplayOk(exportMsg) : ReplayFail(exportMsg);
                break;
            case "main.btnAddEvent":
                AddEvent();
                response = ReplayOk("added event");
                break;
            case "main.btnDeleteEvent":
                response = RemoveSelectedEventCore(requireConfirm: false) ? ReplayOk("deleted event") : ReplayFail("delete event failed");
                break;
            case "main.btnAddTrigger":
                AddTrigger();
                response = ReplayOk("added trigger");
                break;
            case "main.btnDeleteTrigger":
                RemoveTrigger();
                response = ReplayOk("deleted trigger");
                break;
            case "main.btnAddAction":
                AddAction();
                response = ReplayOk("added action");
                break;
            case "main.btnDeleteAction":
                RemoveAction();
                response = ReplayOk("deleted action");
                break;
            case "main.btnValidateProject":
                var issues = EventProjectStore.ValidateProject(_project);
                var text = issues.Count == 0 ? "校验通过" : BuildValidationText(issues, "校验结果");
                response = ReplayOk(text);
                break;
            case "main.btnSelectEvent":
                if (!_replayInputs.TryGetValue("selectedEventId", out var eventId) || string.IsNullOrWhiteSpace(eventId))
                {
                    return ReplayFail("selected event id is empty");
                }
                var idx = _project.Events.FindIndex(x => string.Equals(x.Id, eventId, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                {
                    return ReplayFail("event not found: " + eventId);
                }
                _eventList.SelectedIndex = idx;
                response = ReplayOk("selected event");
                break;
            default:
                response = ReplayFail("target not found: " + target);
                break;
        }

        RefreshReplaySurface();
        await Task.Delay(180);
        return response;
    }

    private async Task<ReplayResponse> HandleReplayFocusAsync(string target, int durationMs)
    {
        SelectReplayContext(target);
        await AnimateReplayPointerAsync(target, durationMs, press: false);
        RefreshReplaySurface();
        return ReplayOk("focused " + target);
    }

    private ReplayResponse SetCurrentEventId(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        var oldId = evt.Id;
        evt.Id = value;
        if (string.Equals(_project.StartEventId, oldId, StringComparison.OrdinalIgnoreCase))
        {
            _project.StartEventId = value;
        }
        foreach (var node in _project.Events)
        {
            foreach (var action in node.Actions)
            {
                if (string.Equals(action.TargetEventId, oldId, StringComparison.OrdinalIgnoreCase))
                {
                    action.TargetEventId = value;
                }
            }
        }
        BindEventList();
        MarkDirty();
        return ReplayOk("set current event id");
    }

    private ReplayResponse SetCurrentEventTitle(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        evt.Title = value;
        BindEventList();
        MarkDirty();
        return ReplayOk("set current event title");
    }

    private ReplayResponse SetCurrentActionTargetEventId(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Actions.Count == 0)
        {
            return ReplayFail("selected event has no action");
        }
        evt.Actions[^1].TargetEventId = value;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current action target");
    }

    private ReplayResponse SetCurrentActionType(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Actions.Count == 0)
        {
            return ReplayFail("selected event has no action");
        }
        if (!Enum.TryParse<DispatchActionType>(value, true, out var parsed))
        {
            return ReplayFail("invalid action type: " + value);
        }
        evt.Actions[^1].Type = parsed;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current action type");
    }

    private ReplayResponse SetCurrentActionPayload(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Actions.Count == 0)
        {
            return ReplayFail("selected event has no action");
        }
        evt.Actions[^1].PayloadJson = value;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current action payload");
    }

    private ReplayResponse SetCurrentTriggerType(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Triggers.Count == 0)
        {
            return ReplayFail("selected event has no trigger");
        }
        if (!Enum.TryParse<TriggerType>(value, true, out var parsed))
        {
            return ReplayFail("invalid trigger type: " + value);
        }
        evt.Triggers[^1].Type = parsed;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current trigger type");
    }

    private ReplayResponse SetCurrentTriggerSourceDomain(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Triggers.Count == 0)
        {
            return ReplayFail("selected event has no trigger");
        }
        if (!Enum.TryParse<EventDomain>(value, true, out var parsed))
        {
            return ReplayFail("invalid trigger source domain: " + value);
        }
        evt.Triggers[^1].SourceDomain = parsed;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current trigger source domain");
    }

    private ReplayResponse SetCurrentTriggerSignal(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Triggers.Count == 0)
        {
            return ReplayFail("selected event has no trigger");
        }
        evt.Triggers[^1].Signal = value;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current trigger signal");
    }

    private ReplayResponse SetCurrentTriggerCondition(string value)
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return ReplayFail("no selected event");
        }
        if (evt.Triggers.Count == 0)
        {
            return ReplayFail("selected event has no trigger");
        }
        evt.Triggers[^1].ConditionExpr = value;
        BindCurrentEvent();
        MarkDirty();
        return ReplayOk("set current trigger condition");
    }

    private void SelectReplayContext(string target)
    {
        switch (target)
        {
            case "main.txtProjectName":
            case "main.txtStartEventId":
                _rightTabs.SelectedTab = _projectTab;
                break;
            case "main.txtCurrentEventId":
            case "main.txtCurrentEventTitle":
                _rightTabs.SelectedTab = _eventTab;
                break;
            case "main.btnAddTrigger":
            case "main.btnDeleteTrigger":
            case "main.txtCurrentTriggerType":
            case "main.txtCurrentTriggerSourceDomain":
            case "main.txtCurrentTriggerSignal":
            case "main.txtCurrentTriggerCondition":
                _rightTabs.SelectedTab = _triggerTab;
                break;
            case "main.btnAddAction":
            case "main.btnDeleteAction":
            case "main.txtCurrentActionType":
            case "main.txtCurrentActionTargetEventId":
            case "main.txtCurrentActionPayload":
                _rightTabs.SelectedTab = _actionTab;
                break;
            default:
                _rightTabs.SelectedTab = _eventTab;
                break;
        }
        RefreshReplaySurface();
    }

    private void RefreshReplaySurface()
    {
        _menu.Refresh();
        _eventList.Refresh();
        _eventGrid.Refresh();
        _triggerGrid.Refresh();
        _actionGrid.Refresh();
        _projectGrid.Refresh();
        _rightTabs.Refresh();
        Update();
    }

    private bool NeedsReplayAnimation(string target)
    {
        return target is "main.txtProjectName"
            or "main.txtStartEventId"
            or "main.txtCurrentEventId"
            or "main.txtCurrentEventTitle"
            or "main.txtCurrentTriggerType"
            or "main.txtCurrentTriggerSourceDomain"
            or "main.txtCurrentTriggerSignal"
            or "main.txtCurrentTriggerCondition"
            or "main.txtCurrentActionType"
            or "main.txtCurrentActionTargetEventId"
            or "main.txtCurrentActionPayload";
    }

    private async Task AnimateReplayPointerAsync(string target, int durationMs, bool press)
    {
        var cursor = EnsureVirtualCursor();
        var start = GetReplayOriginScreenPoint();
        var end = GetReplayTargetScreenPoint(target);
        cursor.MoveToCursorPoint(start);
        cursor.Show();
        cursor.BringToFront();

        var steps = Math.Max(18, durationMs / 16);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var eased = 1f - MathF.Pow(1f - t, 3f);
            var x = start.X + (end.X - start.X) * eased;
            var y = start.Y + (end.Y - start.Y) * eased;
            cursor.MoveToCursorPoint(new Point((int)x, (int)y));
            await Task.Delay(Math.Max(8, durationMs / steps));
        }

        await Task.Delay(press ? 110 : 150);
        if (press)
        {
            await cursor.PulseAsync();
        }
        await Task.Delay(press ? 180 : 120);
        cursor.Hide();
    }

    private VirtualCursorForm EnsureVirtualCursor()
    {
        if (_virtualCursor == null || _virtualCursor.IsDisposed)
        {
            _virtualCursor = new VirtualCursorForm();
        }
        return _virtualCursor;
    }

    private Point GetReplayOriginScreenPoint()
    {
        var clientCenter = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
        return PointToScreen(clientCenter);
    }

    private Point GetReplayTargetScreenPoint(string target)
    {
        return target switch
        {
            "main.btnNewProject" => GetMenuPoint(_fileMenu),
            "main.btnOpenProject" => GetMenuPoint(_fileMenu),
            "main.btnSaveProject" => GetMenuPoint(_fileMenu),
            "main.btnSaveAs" => GetMenuPoint(_fileMenu),
            "main.btnExportRuntime" => GetMenuPoint(_fileMenu),
            "main.btnAddEvent" => GetListPoint(_eventList, _project.Events.Count),
            "main.btnDeleteEvent" => GetListPoint(_eventList, Math.Max(0, _eventList.SelectedIndex)),
            "main.btnSelectEvent" => GetListPoint(_eventList, Math.Max(0, _eventList.SelectedIndex)),
            "main.btnAddTrigger" => GetGridPoint(_triggerGrid, 0, 0),
            "main.btnDeleteTrigger" => GetGridPoint(_triggerGrid, 1, 0),
            "main.btnAddAction" => GetGridPoint(_actionGrid, 0, 0),
            "main.btnDeleteAction" => GetGridPoint(_actionGrid, 1, 0),
            "main.btnValidateProject" => GetMenuPoint(_editMenu),
            "main.txtProjectName" => GetPropertyGridPoint(_projectGrid, 1),
            "main.txtStartEventId" => GetPropertyGridPoint(_projectGrid, 3),
            "main.txtCurrentEventId" => GetPropertyGridPoint(_eventGrid, 1),
            "main.txtCurrentEventTitle" => GetPropertyGridPoint(_eventGrid, 2),
            "main.txtCurrentTriggerType" => GetGridPoint(_triggerGrid, Math.Max(1, _triggerGrid.Rows.Count - 1), 0),
            "main.txtCurrentTriggerSourceDomain" => GetGridPoint(_triggerGrid, Math.Max(1, _triggerGrid.Rows.Count - 1), 1),
            "main.txtCurrentTriggerSignal" => GetGridPoint(_triggerGrid, Math.Max(1, _triggerGrid.Rows.Count - 1), 2),
            "main.txtCurrentTriggerCondition" => GetGridPoint(_triggerGrid, Math.Max(1, _triggerGrid.Rows.Count - 1), 3),
            "main.txtCurrentActionType" => GetGridPoint(_actionGrid, Math.Max(1, _actionGrid.Rows.Count - 1), 0),
            "main.txtCurrentActionTargetEventId" => GetGridPoint(_actionGrid, Math.Max(1, _actionGrid.Rows.Count - 1), 1),
            "main.txtCurrentActionPayload" => GetGridPoint(_actionGrid, Math.Max(1, _actionGrid.Rows.Count - 1), 3),
            _ => GetReplayOriginScreenPoint()
        };
    }

    private Point GetMenuPoint(ToolStripMenuItem item)
    {
        var bounds = item.Bounds;
        return _menu.PointToScreen(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
    }

    private Point GetPropertyGridPoint(PropertyGrid grid, int rowIndex)
    {
        var y = 28 + rowIndex * 22;
        return grid.PointToScreen(new Point(Math.Max(120, grid.Width / 2), Math.Min(grid.Height - 24, y)));
    }

    private Point GetGridPoint(DataGridView grid, int rowIndex, int colIndex)
    {
        var x = 24;
        for (var i = 0; i < Math.Min(colIndex, grid.Columns.Count); i++)
        {
            x += grid.Columns[i].Width;
        }

        var y = grid.ColumnHeadersHeight + 16 + Math.Max(0, rowIndex - 1) * Math.Max(grid.RowTemplate.Height, 22);
        return grid.PointToScreen(new Point(Math.Min(grid.Width - 24, x + 40), Math.Min(grid.Height - 24, y)));
    }

    private Point GetListPoint(ListBox list, int index)
    {
        var itemHeight = Math.Max(list.ItemHeight, 24);
        var y = 14 + Math.Max(0, index) * itemHeight;
        return list.PointToScreen(new Point(Math.Min(list.Width - 20, 110), Math.Min(list.Height - 20, y)));
    }

    private ReplayResponse ReplayOk(string message) => new() { Success = true, Message = message, ActiveDialog = "" };
    private ReplayResponse ReplayFail(string message) => new() { Success = false, Message = message, ActiveDialog = "" };

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmSaveBeforeLoss())
        {
            e.Cancel = true;
            return;
        }
        _replayCts?.Cancel();
        _virtualCursor?.Close();
        base.OnFormClosing(e);
    }
}
