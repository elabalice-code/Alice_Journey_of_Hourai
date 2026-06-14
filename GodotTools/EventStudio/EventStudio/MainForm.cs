using System.ComponentModel;
using System.Text.Json;
using EventStudio.IO;
using EventStudio.Models;
using EventStudio.Replay;

namespace EventStudio;

public sealed class MainForm : Form
{
    private const string AllGroupsId = "__all__";
    private const string UngroupedId = "__ungrouped__";

    private EventProject _project = new();
    private string? _currentPath;
    private bool _dirty;
    private string _currentGroupId = AllGroupsId;

    private readonly SplitContainer _root = new() { Dock = DockStyle.Fill, SplitterDistance = 320 };
    private readonly SplitContainer _content = new() { Dock = DockStyle.Fill, SplitterDistance = 620 };
    private readonly TreeView _groupTree = new() { Dock = DockStyle.Fill, HideSelection = false, AllowDrop = true, LabelEdit = true };
    private readonly ListBox _eventList = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _taskGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly BindingSource _taskGridSource = new();
    private bool _syncingGroupSelection;
    private bool _syncingTaskSelection;
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
        TryOpenDefaultSample();
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

        RebuildReadableMenus();
        _menu.Items.AddRange([_fileMenu, _editMenu]);
        MainMenuStrip = _menu;
        Controls.Add(_menu);
    }

    private void RebuildReadableMenus()
    {
        _fileMenu.Text = "File";
        _editMenu.Text = "Edit";

        _fileMenu.DropDownItems.Clear();
        _fileMenu.DropDownItems.Add("New", null, (_, _) => NewProject());
        _fileMenu.DropDownItems.Add("Open...", null, (_, _) => OpenProject());
        _fileMenu.DropDownItems.Add("Save", null, (_, _) => SaveProject());
        _fileMenu.DropDownItems.Add("Save As...", null, (_, _) => SaveProjectAs());
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _fileMenu.DropDownItems.Add("Export Runtime Graph...", null, (_, _) => ExportRuntimeGraph());
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _fileMenu.DropDownItems.Add("Exit", null, (_, _) => Close());

        _editMenu.DropDownItems.Clear();
        _editMenu.DropDownItems.Add("Add Event Group", null, (_, _) => AddTaskGroup());
        _editMenu.DropDownItems.Add("Add Event", null, (_, _) => AddTask());
        _editMenu.DropDownItems.Add("Delete Selected", null, (_, _) => RemoveSelectedEvent());
        _editMenu.DropDownItems.Add(new ToolStripSeparator());
        _editMenu.DropDownItems.Add("Add Trigger", null, (_, _) => AddTrigger());
        _editMenu.DropDownItems.Add("Delete Trigger", null, (_, _) => RemoveTrigger());
        _editMenu.DropDownItems.Add("Add Action", null, (_, _) => AddAction());
        _editMenu.DropDownItems.Add("Delete Action", null, (_, _) => RemoveAction());
        _editMenu.DropDownItems.Add(new ToolStripSeparator());
        _editMenu.DropDownItems.Add("Validate Project", null, (_, _) => ValidateProjectAndReport());
    }

    private void BuildLayout()
    {
        _eventTab.Text = "Event Detail";
        _triggerTab.Text = "Triggers";
        _actionTab.Text = "Actions";
        _projectTab.Text = "Project";

        SetupGroupTree();
        SetupTaskGrid();
        _eventList.DisplayMember = nameof(EventNode.Title);
        _eventList.SelectedIndexChanged += (_, _) => BindCurrentEvent();
        _eventList.ContextMenuStrip = BuildEventContextMenu();
        _root.Panel1.Controls.Add(_groupTree);

        _eventTab.Controls.Add(_eventGrid);
        _triggerTab.Controls.Add(_triggerGrid);
        _actionTab.Controls.Add(_actionGrid);
        _projectTab.Controls.Add(_projectGrid);

        _rightTabs.TabPages.AddRange([_eventTab, _triggerTab, _actionTab, _projectTab]);
        _content.Panel1.Controls.Add(_taskGrid);
        _content.Panel2.Controls.Add(_rightTabs);
        _root.Panel2.Controls.Add(_content);

        SetupTriggerGrid();
        SetupActionGrid();

        _status.Items.Add(_statusText);
        Controls.Add(_root);
        Controls.Add(_status);
    }

    private void SetupGroupTree()
    {
        _groupTree.ContextMenuStrip = BuildGroupContextMenu();
        _groupTree.AfterSelect += (_, _) => SelectGroupFromTree();
        _groupTree.ItemDrag += (_, e) =>
        {
            if (e.Item is TreeNode { Tag: string id } && IsRealGroupId(id))
            {
                _groupTree.DoDragDrop(new GroupDragData(id), DragDropEffects.Move);
            }
        };
        _groupTree.DragEnter += GroupTree_DragOver;
        _groupTree.DragOver += GroupTree_DragOver;
        _groupTree.DragDrop += GroupTree_DragDrop;
        _groupTree.AfterLabelEdit += GroupTree_AfterLabelEdit;
    }

    private ContextMenuStrip BuildGroupContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Add Event Group", null, (_, _) => AddTaskGroup());
        menu.Items.Add("Add Event", null, (_, _) => AddTask());
        menu.Items.Add("Delete Selected", null, (_, _) => RemoveSelectedEvent());
        return menu;
    }

    private void SetupTaskGrid()
    {
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", DataPropertyName = nameof(TaskRow.Id), Width = 130 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Title", DataPropertyName = nameof(TaskRow.Title), Width = 180 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trigger Source", DataPropertyName = nameof(TaskRow.TriggerSource), Width = 150 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trigger Reason", DataPropertyName = nameof(TaskRow.TriggerReason), Width = 150 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Interact Object", DataPropertyName = nameof(TaskRow.InteractionObjectId), Width = 150 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Output", DataPropertyName = nameof(TaskRow.Output), Width = 150 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "State", DataPropertyName = nameof(TaskRow.State), Width = 150 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Completion Item", DataPropertyName = nameof(TaskRow.CompletionItem), Width = 150 });
        _taskGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Enabled", DataPropertyName = nameof(TaskRow.Enabled), Width = 70 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Triggers", DataPropertyName = nameof(TaskRow.TriggerCount), Width = 70 });
        _taskGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Actions", DataPropertyName = nameof(TaskRow.ActionCount), Width = 70 });
        _taskGrid.RowHeadersVisible = false;
        _taskGrid.DataSource = _taskGridSource;
        _taskGrid.SelectionChanged += (_, _) => SelectEventFromTaskGrid();
        _taskGrid.CellDoubleClick += (_, _) => _rightTabs.SelectedTab = _eventTab;
        _taskGrid.MouseDown += TaskGrid_MouseDown;
        _taskGrid.ContextMenuStrip = BuildTaskContextMenu();
    }

    private ContextMenuStrip BuildTaskContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Add Event Group", null, (_, _) => AddTaskGroup());
        menu.Items.Add("Add Event", null, (_, _) => AddTask());
        menu.Items.Add("Delete Selected", null, (_, _) => RemoveSelectedEvent());
        return menu;
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
        PopulateGroupTree();
        _taskGridSource.DataSource = BuildTaskRows();
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
        SyncTaskGridSelection();
    }

    private void PopulateGroupTree()
    {
        var selected = _currentGroupId;
        _syncingGroupSelection = true;
        try
        {
            _groupTree.BeginUpdate();
            _groupTree.Nodes.Clear();

            var root = new TreeNode("All Events") { Tag = AllGroupsId };
            var ungrouped = new TreeNode("Ungrouped") { Tag = UngroupedId };
            root.Nodes.Add(ungrouped);

            var groups = _project.Events
                .Where(x => x.NodeKind == EventNodeKind.TaskGroup)
                .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var childLookup = groups.Values
                .GroupBy(x => groups.ContainsKey(x.ParentGroupId) ? x.ParentGroupId : "", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

            AddGroupNodes(root, "", childLookup, []);
            _groupTree.Nodes.Add(root);
            root.Expand();

            var node = FindGroupNode(selected) ?? root;
            _currentGroupId = node.Tag as string ?? AllGroupsId;
            _groupTree.SelectedNode = node;
            node.EnsureVisible();
        }
        finally
        {
            _groupTree.EndUpdate();
            _syncingGroupSelection = false;
        }
    }

    private void AddGroupNodes(TreeNode parent, string parentGroupId, Dictionary<string, List<EventNode>> childLookup, HashSet<string> visited)
    {
        if (!childLookup.TryGetValue(parentGroupId, out var children))
        {
            return;
        }

        foreach (var group in children.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (!visited.Add(group.Id))
            {
                continue;
            }

            var node = new TreeNode(group.Title) { Tag = group.Id };
            parent.Nodes.Add(node);
            AddGroupNodes(node, group.Id, childLookup, visited);
            visited.Remove(group.Id);
        }
    }

    private TreeNode? FindGroupNode(string id)
    {
        foreach (TreeNode node in _groupTree.Nodes)
        {
            var found = FindGroupNode(node, id);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TreeNode? FindGroupNode(TreeNode node, string id)
    {
        if (node.Tag is string nodeId && string.Equals(nodeId, id, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (TreeNode child in node.Nodes)
        {
            var found = FindGroupNode(child, id);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private List<TaskRow> BuildTaskRows()
    {
        var groups = _project.Events
            .Where(x => x.NodeKind == EventNodeKind.TaskGroup)
            .ToDictionary(x => x.Id, x => x.Title, StringComparer.OrdinalIgnoreCase);
        return _project.Events
            .Where(IsTaskInCurrentGroup)
            .Select(evt => new TaskRow
        {
            Id = evt.Id,
            Kind = "Event",
            Group = string.IsNullOrWhiteSpace(evt.ParentGroupId) ? "" : groups.GetValueOrDefault(evt.ParentGroupId, evt.ParentGroupId),
            Title = evt.Title,
            TriggerSource = SummarizeTriggerSource(evt),
            TriggerReason = SummarizeTriggerReason(evt),
            InteractionObjectId = evt.InteractionObjectId,
            Output = SummarizeOutput(evt),
            State = string.IsNullOrWhiteSpace(evt.StateKey) ? "" : $"{evt.StateKey}={evt.StateValueOnActivate}",
            CompletionItem = string.IsNullOrWhiteSpace(evt.CompletionItemId) ? "" : $"{evt.CompletionItemId} x{evt.CompletionItemCount}",
            Enabled = evt.Enabled,
            TriggerCount = evt.Triggers.Count,
            ActionCount = evt.Actions.Count
        }).ToList();
    }

    private static string SummarizeTriggerSource(EventNode evt)
    {
        var trigger = evt.Triggers.FirstOrDefault();
        return trigger == null ? "" : trigger.SourceDomain.ToString();
    }

    private static string SummarizeTriggerReason(EventNode evt)
    {
        var trigger = evt.Triggers.FirstOrDefault();
        if (trigger == null)
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(trigger.Signal) ? trigger.Type.ToString() : trigger.Signal;
    }

    private static string SummarizeOutput(EventNode evt)
    {
        var action = evt.Actions.FirstOrDefault();
        return action == null ? "" : action.Type.ToString();
    }

    private bool IsTaskInCurrentGroup(EventNode evt)
    {
        if (evt.NodeKind == EventNodeKind.TaskGroup)
        {
            return false;
        }

        return _currentGroupId switch
        {
            AllGroupsId => true,
            UngroupedId => string.IsNullOrWhiteSpace(evt.ParentGroupId),
            _ => string.Equals(evt.ParentGroupId, _currentGroupId, StringComparison.OrdinalIgnoreCase)
        };
    }

    private void SyncTaskGridSelection()
    {
        if (_eventList.SelectedItem is not EventNode evt)
        {
            return;
        }

        _syncingTaskSelection = true;
        try
        {
            foreach (DataGridViewRow row in _taskGrid.Rows)
            {
                if (row.DataBoundItem is TaskRow taskRow &&
                    string.Equals(taskRow.Id, evt.Id, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                    _taskGrid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }
        finally
        {
            _syncingTaskSelection = false;
        }
    }

    private void SelectEventFromTaskGrid()
    {
        if (_syncingTaskSelection || _taskGrid.CurrentRow?.DataBoundItem is not TaskRow row)
        {
            return;
        }

        var idx = _project.Events.FindIndex(x => string.Equals(x.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && _eventList.SelectedIndex != idx)
        {
            _eventList.SelectedIndex = idx;
        }
    }

    private void SelectGroupFromTree()
    {
        if (_syncingGroupSelection || _groupTree.SelectedNode?.Tag is not string groupId)
        {
            return;
        }

        _currentGroupId = groupId;
        _taskGridSource.DataSource = BuildTaskRows();
        if (IsRealGroupId(groupId))
        {
            SelectEvent(groupId, revealGroup: false);
        }
        else if (_taskGrid.Rows.Count > 0)
        {
            _taskGrid.Rows[0].Selected = true;
            _taskGrid.CurrentCell = _taskGrid.Rows[0].Cells[0];
        }
        else
        {
            _eventGrid.SelectedObject = null;
            _triggerGrid.DataSource = null;
            _actionGrid.DataSource = null;
        }

        UpdateStatus();
    }

    private void GroupTree_AfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (e.Label == null || e.Node?.Tag is not string groupId || !IsRealGroupId(groupId))
        {
            return;
        }

        var group = _project.Find(groupId);
        if (group == null)
        {
            e.CancelEdit = true;
            return;
        }

        var label = e.Label.Trim();
        if (string.IsNullOrWhiteSpace(label))
        {
            e.CancelEdit = true;
            return;
        }

        group.Title = label;
        BindEventList();
        SelectGroup(group.Id);
        MarkDirty();
    }

    private void TaskGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var hit = _taskGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _taskGrid.Rows.Count)
        {
            return;
        }

        _taskGrid.CurrentCell = _taskGrid.Rows[hit.RowIndex].Cells[Math.Max(0, hit.ColumnIndex)];
        if (_taskGrid.Rows[hit.RowIndex].DataBoundItem is TaskRow row)
        {
            _taskGrid.DoDragDrop(new EventDragData(row.Id), DragDropEffects.Move);
        }
    }

    private void GroupTree_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data == null ||
            (!e.Data.GetDataPresent(typeof(EventDragData)) && !e.Data.GetDataPresent(typeof(GroupDragData))))
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        var point = _groupTree.PointToClient(new Point(e.X, e.Y));
        var targetNode = _groupTree.GetNodeAt(point);
        if (targetNode != null)
        {
            _groupTree.SelectedNode = targetNode;
        }

        e.Effect = DragDropEffects.Move;
    }

    private void GroupTree_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data == null)
        {
            return;
        }

        var point = _groupTree.PointToClient(new Point(e.X, e.Y));
        var targetNode = _groupTree.GetNodeAt(point);
        var targetGroupId = GetDropGroupId(targetNode);

        if (e.Data.GetData(typeof(EventDragData)) is EventDragData eventDrag)
        {
            MoveEventToGroup(eventDrag.EventId, targetGroupId);
            return;
        }

        if (e.Data.GetData(typeof(GroupDragData)) is GroupDragData groupDrag)
        {
            MoveGroupToGroup(groupDrag.GroupId, targetGroupId);
        }
    }

    private string GetDropGroupId(TreeNode? targetNode)
    {
        if (targetNode?.Tag is not string id || id == AllGroupsId || id == UngroupedId)
        {
            return "";
        }

        return id;
    }

    private void MoveEventToGroup(string eventId, string targetGroupId)
    {
        var evt = _project.Find(eventId);
        if (evt == null || evt.NodeKind == EventNodeKind.TaskGroup)
        {
            return;
        }

        evt.ParentGroupId = targetGroupId;
        _currentGroupId = string.IsNullOrWhiteSpace(targetGroupId) ? UngroupedId : targetGroupId;
        BindEventList();
        SelectEvent(evt.Id);
        MarkDirty();
    }

    private void MoveGroupToGroup(string groupId, string targetGroupId)
    {
        var group = _project.Find(groupId);
        if (group == null || group.NodeKind != EventNodeKind.TaskGroup)
        {
            return;
        }

        if (string.Equals(groupId, targetGroupId, StringComparison.OrdinalIgnoreCase) ||
            IsGroupDescendant(targetGroupId, groupId))
        {
            return;
        }

        group.ParentGroupId = targetGroupId;
        _currentGroupId = group.Id;
        BindEventList();
        SelectGroup(group.Id);
        MarkDirty();
    }

    private bool IsGroupDescendant(string maybeDescendantId, string ancestorId)
    {
        var current = _project.Find(maybeDescendantId);
        while (current != null && current.NodeKind == EventNodeKind.TaskGroup)
        {
            if (string.Equals(current.ParentGroupId, ancestorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = _project.Find(current.ParentGroupId);
        }

        return false;
    }

    private static bool IsRealGroupId(string id) =>
        !string.IsNullOrWhiteSpace(id) &&
        !string.Equals(id, AllGroupsId, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(id, UngroupedId, StringComparison.OrdinalIgnoreCase);

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

        var isGroup = evt.NodeKind == EventNodeKind.TaskGroup;
        _triggerGrid.Enabled = !isGroup;
        _actionGrid.Enabled = !isGroup;
        _triggerGrid.DataSource = isGroup ? null : new BindingSource(new BindingList<TriggerRule>(evt.Triggers), null);
        _actionGrid.DataSource = isGroup ? null : new BindingSource(new BindingList<DispatchAction>(evt.Actions), null);
        SyncTaskGridSelection();
        UpdateStatus();
    }

    private void EventGrid_PropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
    {
        if (e.ChangedItem?.Label == nameof(EventNode.Title))
        {
            BindEventList();
        }
        _taskGridSource.DataSource = BuildTaskRows();
        SyncTaskGridSelection();
        MarkDirty();
    }

    private void AddEvent()
    {
        AddTask();
    }

    private void AddTaskGroup()
    {
        var node = _project.CreateEvent(EventNodeKind.TaskGroup);
        node.ParentGroupId = IsRealGroupId(_currentGroupId) ? _currentGroupId : "";
        BindEventList();
        SelectGroup(node.Id);
        MarkDirty();
    }

    private void AddTask()
    {
        var node = _project.CreateEvent(EventNodeKind.Task);
        node.ParentGroupId = IsRealGroupId(_currentGroupId) ? _currentGroupId : "";
        InitializeNewEvent(node);
        BindEventList();
        SelectEvent(node.Id);
        MarkDirty();
    }

    private static void InitializeNewEvent(EventNode node)
    {
        if (string.IsNullOrWhiteSpace(node.StateKey))
        {
            node.StateKey = $"event.{node.Id}.active";
        }

        if (node.Triggers.Count == 0)
        {
            node.Triggers.Add(new TriggerRule
            {
                Type = TriggerType.Signal,
                SourceDomain = EventDomain.Map,
                Signal = "ScenePosition"
            });
        }

        if (node.Actions.Count == 0)
        {
            node.Actions.Add(new DispatchAction
            {
                Type = DispatchActionType.EmitSignal,
                PayloadJson = "{\"signal\":\"EventActivated\"}"
            });
        }
    }

    private bool SelectEvent(string id, bool revealGroup = true)
    {
        var idx = _project.Events.FindIndex(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            return false;
        }
        var evt = _project.Events[idx];
        if (revealGroup)
        {
            _currentGroupId = evt.NodeKind == EventNodeKind.TaskGroup
                ? evt.Id
                : string.IsNullOrWhiteSpace(evt.ParentGroupId) ? UngroupedId : evt.ParentGroupId;
            PopulateGroupTree();
            _taskGridSource.DataSource = BuildTaskRows();
        }
        _eventList.SelectedIndex = idx;
        SyncTaskGridSelection();
        return true;
    }

    private bool SelectGroup(string id)
    {
        _currentGroupId = id;
        PopulateGroupTree();
        _taskGridSource.DataSource = BuildTaskRows();
        return SelectEvent(id, revealGroup: false);
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
            var label = evt.NodeKind == EventNodeKind.TaskGroup ? "event group" : "event";
            var yes = MessageBox.Show($"Delete {label} {evt.Title} ({evt.Id})?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (yes != DialogResult.Yes)
            {
                return false;
            }
        }

        var fallbackGroupId = evt.NodeKind == EventNodeKind.TaskGroup ? evt.ParentGroupId : "";
        if (evt.NodeKind == EventNodeKind.TaskGroup)
        {
            foreach (var child in _project.Events.Where(x => string.Equals(x.ParentGroupId, evt.Id, StringComparison.OrdinalIgnoreCase)))
            {
                child.ParentGroupId = fallbackGroupId;
            }
        }

        _project.Events.Remove(evt);
        foreach (var node in _project.Events)
        {
            node.Actions.RemoveAll(x => string.Equals(x.TargetEventId, evt.Id, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(_project.StartEventId, evt.Id, StringComparison.OrdinalIgnoreCase))
        {
            _project.StartEventId = _project.Events.FirstOrDefault(x => x.NodeKind != EventNodeKind.TaskGroup)?.Id ?? "";
        }

        if (string.Equals(_currentGroupId, evt.Id, StringComparison.OrdinalIgnoreCase))
        {
            _currentGroupId = string.IsNullOrWhiteSpace(fallbackGroupId) ? AllGroupsId : fallbackGroupId;
        }

        BindEventList();
        BindCurrentEvent();
        MarkDirty();
        return true;
    }

    private void AddTrigger()
    {
        if (_eventList.SelectedItem is not EventNode evt || evt.NodeKind == EventNodeKind.TaskGroup)
        {
            return;
        }
        evt.Triggers.Add(new TriggerRule());
        BindCurrentEvent();
        MarkDirty();
    }

    private void RemoveTrigger()
    {
        if (_eventList.SelectedItem is not EventNode evt || evt.NodeKind == EventNodeKind.TaskGroup || _triggerGrid.CurrentRow == null)
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
        if (_eventList.SelectedItem is not EventNode evt || evt.NodeKind == EventNodeKind.TaskGroup)
        {
            return;
        }
        evt.Actions.Add(new DispatchAction());
        BindCurrentEvent();
        MarkDirty();
    }

    private void RemoveAction()
    {
        if (_eventList.SelectedItem is not EventNode evt || evt.NodeKind == EventNodeKind.TaskGroup || _actionGrid.CurrentRow == null)
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
            Title = "Open Event Project"
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
            Title = "Save Event Project",
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
        UpdateStatus("Saved");
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
            Title = "Export Runtime Graph",
            FileName = $"{_project.Name.Replace(' ', '_')}.runtime.events.json"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        var graph = EventProjectStore.BuildRuntimeGraph(_project);
        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(dlg.FileName, json);
        UpdateStatus("Exported runtime graph");
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
            ShowValidationResult(issues, "Validation failed: errors block save/export.");
            return false;
        }

        var msg = BuildValidationText(issues, "Validation passed with warnings. Continue save/export?");
        var result = MessageBox.Show(msg, "Project Validation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return result == DialogResult.Yes;
    }

    private void ValidateProjectAndReport()
    {
        var issues = EventProjectStore.ValidateProject(_project);
        if (issues.Count == 0)
        {
            MessageBox.Show("Validation passed: no issues found.", "Project Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ShowValidationResult(issues, "Validation issues:");
    }

    private void ShowValidationResult(List<ValidationIssue> issues, string title)
    {
        var msg = BuildValidationText(issues, title);
        var icon = issues.Any(x => x.Severity == ValidationSeverity.Error) ? MessageBoxIcon.Error : MessageBoxIcon.Warning;
        MessageBox.Show(msg, "Project Validation", MessageBoxButtons.OK, icon);
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
            lines.Add($"... {issues.Count - 20} more issues omitted");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private bool ConfirmSaveBeforeLoss()
    {
        if (!_dirty)
        {
            return true;
        }
        var result = MessageBox.Show("Current changes are not saved. Save now?", "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
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
        var fileName = string.IsNullOrWhiteSpace(_currentPath) ? "Untitled.events.json" : Path.GetFileName(_currentPath);
        Text = $"{(_dirty ? "*" : "")}EventStudio - {fileName}";
    }

    private void UpdateStatus(string? text = null)
    {
        var groups = _project.Events.Count(x => x.NodeKind == EventNodeKind.TaskGroup);
        var tasks = _project.Events.Count(x => x.NodeKind != EventNodeKind.TaskGroup);
        var triggers = _project.Events.Where(x => x.NodeKind != EventNodeKind.TaskGroup).Sum(x => x.Triggers.Count);
        var actions = _project.Events.Where(x => x.NodeKind != EventNodeKind.TaskGroup).Sum(x => x.Actions.Count);
        _statusText.Text = string.IsNullOrWhiteSpace(text)
            ? $"Events {tasks} | Groups {groups} | Triggers {triggers} | Actions {actions} | StartEvent: {_project.StartEventId}"
            : $"{text} | Events {tasks} | Groups {groups} | Triggers {triggers} | Actions {actions}";
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
        var group = _project.CreateEvent(EventNodeKind.TaskGroup);
        group.Title = "Main Quest";
        var task = _project.CreateEvent(EventNodeKind.Task);
        task.Title = "New Event";
        task.ParentGroupId = group.Id;
        InitializeNewEvent(task);
        _currentGroupId = group.Id;
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
            _project.CreateEvent(EventNodeKind.Task);
        }
        _currentGroupId = AllGroupsId;
        _currentPath = path;
        _dirty = false;
        BindProject();
        UpdateTitle();
    }

    private void TryOpenDefaultSample()
    {
        var samplePath = FindDefaultSamplePath();
        if (string.IsNullOrWhiteSpace(samplePath))
        {
            return;
        }

        try
        {
            OpenProjectFromPath(samplePath);
            _dirty = false;
            UpdateStatus("Loaded sample data for editing");
        }
        catch (Exception ex)
        {
            UpdateStatus("Sample load skipped: " + ex.Message);
        }
    }

    private static string? FindDefaultSamplePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "story1_map1_story2_map2_story3_demo.events.json"),
            Path.Combine(baseDir, "story1_map1_story2_demo.events.json"),
            Path.Combine(baseDir, "start_game_flow.events.json"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "GodotTools", "EventStudio", "Samples", "story1_map1_story2.events.json")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "GodotTools", "EventStudio", "Samples", "prologue_flow.events.json"))
        };

        return candidates.FirstOrDefault(File.Exists);
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
            "main.btnAddEventGroup",
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
            case "main.btnAddEventGroup":
                AddTaskGroup();
                response = ReplayOk("added event group");
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
            "main.btnAddEventGroup" => GetTreePoint(_groupTree, Math.Max(0, _groupTree.SelectedNode?.Level ?? 0)),
            "main.btnAddEvent" => GetGridPoint(_taskGrid, Math.Max(1, _taskGrid.Rows.Count), 0),
            "main.btnDeleteEvent" => GetGridPoint(_taskGrid, Math.Max(1, _taskGrid.CurrentRow?.Index + 1 ?? 1), 0),
            "main.btnSelectEvent" => GetGridPoint(_taskGrid, Math.Max(1, _taskGrid.CurrentRow?.Index + 1 ?? 1), 0),
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

    private Point GetTreePoint(TreeView tree, int index)
    {
        var y = 20 + Math.Max(0, index) * Math.Max(tree.ItemHeight, 24);
        return tree.PointToScreen(new Point(Math.Min(tree.Width - 20, 120), Math.Min(tree.Height - 20, y)));
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

internal sealed class TaskRow
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Group { get; set; } = "";
    public string Title { get; set; } = "";
    public string TriggerSource { get; set; } = "";
    public string TriggerReason { get; set; } = "";
    public string InteractionObjectId { get; set; } = "";
    public string Output { get; set; } = "";
    public string State { get; set; } = "";
    public string CompletionItem { get; set; } = "";
    public bool Enabled { get; set; }
    public int TriggerCount { get; set; }
    public int ActionCount { get; set; }
}

internal sealed record EventDragData(string EventId);

internal sealed record GroupDragData(string GroupId);
