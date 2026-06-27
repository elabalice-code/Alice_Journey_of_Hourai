using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms.Design;
using MapEditor.Godot;
using MapEditor.Godot.Tscn;
using MapEditor.Models;

namespace MapEditor;

public sealed partial class MainForm : Form
{
    private readonly MapProject _project = MapProject.CreateDefault();
    private string? _currentPath;
    private string? _godotRoot;
    private string _pinnedStartingMapPath = "";
    private MapDefinition? _selectedMap;
    private readonly UndoManager _undo = new();
    private readonly PortalSyncActor _portalSyncActor;

    private readonly SplitContainer _rootSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 200 };
    private readonly ListBox _mapsList = new() { Dock = DockStyle.Fill };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly SplitContainer _mapTabSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 740 };
    private readonly MapCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip _mapTools = new() { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
    private readonly ToolStripComboBox _viewModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ToolStripLabel _collisionEditModeLabel = new("编辑") { Visible = false };
    private readonly ToolStripComboBox _collisionEditModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Visible = false };
    private readonly ToolStripLabel _collisionModeLabel = new("生效") { Visible = false };
    private readonly ToolStripComboBox _collisionModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Visible = false };
    private readonly ToolStripLabel _toolModeLabel = new("工具") { Visible = false };
    private readonly ToolStripButton _toolSelect = new("选择(S)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripButton _toolVertex = new("顶点(Q)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripButton _toolMove = new("移动(W)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripButton _toolRotate = new("旋转(E)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripButton _toolScale = new("拉伸(R)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripButton _toolAddSquareCollision = new("添加方形(A)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripButton _toolRemoveCollision = new("移除碰撞(D)") { CheckOnClick = true, Visible = false };
    private readonly ToolStripLabel _collisionTargetLabel = new("碰撞编辑") { Visible = false };
    private readonly ToolStripComboBox _collisionTargetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Visible = false };
    private readonly ToolStripButton _collisionInitButton = new("初始化") { Visible = false };
    private readonly ToolStripButton _collisionLoadButton = new("加载") { Visible = false };
    private readonly ToolStripButton _collisionSaveButton = new("保存") { Visible = false };
    private readonly PropertyGrid _mapGrid = new() { Dock = DockStyle.Fill };
    private readonly SplitContainer _linksSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 420 };
    private readonly ListBox _linksList = new() { Dock = DockStyle.Fill };
    private readonly PropertyGrid _linkGrid = new() { Dock = DockStyle.Fill };
    private readonly SplitContainer _linksTabSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 520 };
    private readonly LinksGraphCanvas _linksGraph = new() { Dock = DockStyle.Fill };
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusText = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };
    private int _lastMapTipIndex = -1;
    private int _lastLinkTipIndex = -1;
    private readonly HashSet<Type> _resPathEditorProviderApplied = new();
    private CollisionEditTarget _collisionEditTarget = CollisionEditTarget.Tile;
    private CollisionLayoutData? _collisionLayout;
    private bool _collisionLayoutDirty;
    private readonly System.Windows.Forms.Timer _statusHintTimer = new();

    public MainForm()
    {
        MainFormContext.CurrentForm = this;
        ProjectContext.CurrentProject = _project;
        _portalSyncActor = new PortalSyncActor(this);
        Text = "地图编辑器";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Shown += (_, _) => BeginInvoke(() =>
        {
            ApplyRootSplitLayout();
            ImportFromGodot();
        });

        var menu = BuildMenu();

        _mapsList.FormattingEnabled = true;
        _mapsList.Format += FormatMapListItem;
        _mapsList.SelectedIndexChanged += (_, _) => OnSelectedMapChanged();
        _mapsList.HorizontalScrollbar = true;
        _mapsList.MouseMove += (_, e) => UpdateMapListToolTip(e.Location);
        _mapsList.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
                return;
            var idx = _mapsList.IndexFromPoint(e.Location);
            if (idx >= 0 && idx < _mapsList.Items.Count)
                _mapsList.SelectedIndex = idx;
        };
        var mapsMenu = new ContextMenuStrip();
        var mapsAdd = new ToolStripMenuItem("新增地图", null, (_, _) => AddMap());
        var mapsDel = new ToolStripMenuItem("删除地图", null, (_, _) => RemoveSelectedMap());
        var mapsPin = new ToolStripMenuItem("设为置顶", null, (_, _) => SetSelectedMapAsPinnedStart());
        mapsMenu.Items.AddRange([mapsAdd, mapsDel, new ToolStripSeparator(), mapsPin]);
        mapsMenu.Opening += (_, e) =>
        {
            var selectedMap = _mapsList.SelectedItem as MapDefinition;
            mapsDel.Enabled = selectedMap != null;
            mapsPin.Enabled = selectedMap != null;
            mapsPin.Checked = selectedMap != null && IsPinnedStartingMap(selectedMap);
        };
        _mapsList.ContextMenuStrip = mapsMenu;

        _rootSplit.Panel1.Controls.Add(_mapsList);

        _linksList.DisplayMember = nameof(MapLink.DisplayName);
        _linksList.SelectedIndexChanged += (_, _) => OnSelectedLinkChanged();
        _linksList.HorizontalScrollbar = true;
        _linksList.MouseMove += (_, e) => UpdateLinkListToolTip(e.Location);
        _linksSplit.Panel1.Controls.Add(_linksList);
        _linksSplit.Panel2.Controls.Add(_linkGrid);

        _linksGraph.MapSelected = mapId => JumpToMap(mapId);
        _linksGraph.PortalSelected = (mapId, portalId) => SelectOrCreateLinkForPortal(mapId, portalId);
        _linksGraph.PortalTargetSetRequested = (fromMapId, fromPortalId, toMapId, toPortalId) =>
            SetPortalLinkTarget(fromMapId, fromPortalId, toMapId, toPortalId);
        _linksGraph.LinkSelected = link =>
        {
            var idx = _project.Links.FindIndex(l => ReferenceEquals(l, link));
            if (idx >= 0)
            {
                _tabs.SelectedIndex = 1;
                _linksList.SelectedIndex = idx;
            }
        };
        _linksGraph.ShowHoverHint = (text, p) =>
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            _toolTip.Show(text, _linksGraph, p.X + 18, p.Y + 18, 6000);
        };
        _linksGraph.HideHoverHint = () => _toolTip.Hide(_linksGraph);

        _mapGrid.HelpVisible = true;
        _mapGrid.ToolbarVisible = false;
        _mapGrid.SelectedGridItemChanged += (_, _) => ShowGridHelpToolTip(_mapGrid);
        _mapGrid.SelectedObjectsChanged += (_, _) => AutoFitPropertyGridLabelWidth(_mapGrid);
        _mapGrid.PropertyValueChanged += (_, e) => OnMapGridPropertyValueChanged(e);
        _mapGrid.Resize += (_, _) => AutoFitPropertyGridLabelWidth(_mapGrid);
        HookResourceBrowse(_mapGrid);

        _linkGrid.HelpVisible = true;
        _linkGrid.ToolbarVisible = false;
        _linkGrid.SelectedGridItemChanged += (_, _) => ShowGridHelpToolTip(_linkGrid);
        _linkGrid.SelectedObjectsChanged += (_, _) => AutoFitPropertyGridLabelWidth(_linkGrid);
        _linkGrid.Resize += (_, _) => AutoFitPropertyGridLabelWidth(_linkGrid);
        HookResourceBrowse(_linkGrid);

        _canvas.CommitRequested = info => ConfirmAndApply(info);
        _canvas.LayoutCollisionChanged = () =>
        {
            _collisionLayoutDirty = true;
            _collisionSaveButton.Enabled = true;
            UpdateStatus();
        };
        _canvas.LayoutCollisionCommitted = (name, before, after) =>
        {
            _undo.Push(new CollisionLayoutUndoAction(
                $"碰撞布局: {name}",
                before,
                after,
                snapshot =>
                {
                    _collisionLayout = snapshot;
                    _canvas.SetLayoutCollision(_collisionLayout);
                    _collisionLayoutDirty = true;
                    _collisionSaveButton.Enabled = true;
                    UpdateStatus();
                }));
            _collisionLayoutDirty = true;
            _collisionSaveButton.Enabled = true;
            UpdateStatus();
        };
        _canvas.ShowHoverHint = (text, p) =>
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            _toolTip.Show(text, _canvas, p.X + 18, p.Y + 18, 6000);
        };
        _canvas.HideHoverHint = () => _toolTip.Hide(_canvas);
        _canvas.GetPortalHoverText = portal => BuildPortalHoverText(portal);
        _canvas.GetEntityHoverText = ent => BuildEntityHoverText(ent);
        _canvas.PortalRightClick = portal => JumpToPortalTarget(portal);
        _canvas.AddPortalRequested = (x, y) => AddPortalAtWorld(x, y);

        _mapTools.Items.Add(new ToolStripLabel("视图"));
        _viewModeCombo.Items.AddRange(["地图", "碰撞"]);
        _viewModeCombo.SelectedIndex = 0;
        _viewModeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_viewModeCombo.SelectedIndex == 0)
            {
                _canvas.SetViewMode(MapCanvas.CanvasViewMode.Map);
                SetCollisionToolsVisible(MapCanvas.CanvasViewMode.Map);
                return;
            }

            var mode = _collisionEditModeCombo.SelectedIndex == 0 ? MapCanvas.CanvasViewMode.TileSetCollision : MapCanvas.CanvasViewMode.LayoutCollision;
            _canvas.SetViewMode(mode);
            SetCollisionToolsVisible(mode);
            if (mode == MapCanvas.CanvasViewMode.LayoutCollision)
                ReloadCollisionGridForSelectedMap();
        };
        _mapTools.Items.Add(_viewModeCombo);

        _collisionEditModeCombo.Items.AddRange(["TileSet碰撞", "碰撞布局"]);
        _collisionEditModeCombo.SelectedIndex = 1;
        _collisionEditModeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_viewModeCombo.SelectedIndex != 1)
                return;
            var mode = _collisionEditModeCombo.SelectedIndex == 0 ? MapCanvas.CanvasViewMode.TileSetCollision : MapCanvas.CanvasViewMode.LayoutCollision;
            _canvas.SetViewMode(mode);
            SetCollisionToolsVisible(mode);
            if (mode == MapCanvas.CanvasViewMode.LayoutCollision)
                ReloadCollisionGridForSelectedMap();
        };

        _collisionModeCombo.Items.AddRange(["Tile前景", "前景纹理"]);
        _collisionModeCombo.SelectedIndex = 0;
        _collisionModeCombo.SelectedIndexChanged += CollisionModeComboSelectedIndexChangedGuard;

        _mapTools.Items.Add(new ToolStripSeparator());
        _mapTools.Items.Add(_collisionEditModeLabel);
        _mapTools.Items.Add(_collisionEditModeCombo);
        _mapTools.Items.Add(_collisionModeLabel);
        _mapTools.Items.Add(_collisionModeCombo);
        _mapTools.Items.Add(new ToolStripSeparator());
        _mapTools.Items.Add(_toolModeLabel);
        _mapTools.Items.Add(_toolSelect);
        _mapTools.Items.Add(_toolVertex);
        _mapTools.Items.Add(_toolMove);
        _mapTools.Items.Add(_toolRotate);
        _mapTools.Items.Add(_toolScale);
        _mapTools.Items.Add(new ToolStripSeparator());
        _mapTools.Items.Add(_toolAddSquareCollision);
        _mapTools.Items.Add(_toolRemoveCollision);
        _mapTools.Items.Add(_collisionTargetLabel);
        _mapTools.Items.Add(_collisionTargetCombo);
        _mapTools.Items.Add(_collisionInitButton);
        _mapTools.Items.Add(_collisionLoadButton);
        _mapTools.Items.Add(_collisionSaveButton);
        _toolSelect.CheckedChanged += ToolCheckedChangedGuard;
        _toolVertex.CheckedChanged += ToolCheckedChangedGuard;
        _toolMove.CheckedChanged += ToolCheckedChangedGuard;
        _toolRotate.CheckedChanged += ToolCheckedChangedGuard;
        _toolScale.CheckedChanged += ToolCheckedChangedGuard;
        _toolAddSquareCollision.CheckedChanged += (_, _) =>
        {
            if (_toolAddSquareCollision.Checked)
            {
                _toolRemoveCollision.Checked = false;
                SetToolMode(MapCanvas.CollisionToolMode.Select);
                _canvas.BeginAddSquareTileCollision();
            }
            else
            {
                _canvas.CancelPendingTileCollisionAction();
            }
        };
        _toolRemoveCollision.CheckedChanged += (_, _) =>
        {
            if (_toolRemoveCollision.Checked)
            {
                _toolAddSquareCollision.Checked = false;
                SetToolMode(MapCanvas.CollisionToolMode.Select);
                _canvas.BeginRemoveTileCollision();
            }
            else
            {
                _canvas.CancelPendingTileCollisionAction();
            }
        };

        _canvas.ToolModeChanged = mode => BeginInvoke(() => UpdateToolButtons(mode));

        _collisionTargetCombo.Items.AddRange(["Tile碰撞文件", "前景纹理碰撞文件"]);
        _collisionTargetCombo.SelectedIndex = 0;
        _collisionTargetCombo.SelectedIndexChanged += (_, _) =>
        {
            _collisionEditTarget = _collisionTargetCombo.SelectedIndex == 1 ? CollisionEditTarget.ForegroundTexture : CollisionEditTarget.Tile;
            ReloadCollisionGridForSelectedMap();
        };
        _collisionInitButton.Click += (_, _) => InitializeCollisionForSelectedMap();
        _collisionLoadButton.Click += (_, _) => ReloadCollisionGridForSelectedMap();
        _collisionSaveButton.Click += (_, _) => SaveCollisionGridForSelectedMap();

        var mapCanvasHost = new Panel { Dock = DockStyle.Fill };
        mapCanvasHost.Controls.Add(_canvas);
        mapCanvasHost.Controls.Add(_mapTools);
        _mapTabSplit.Panel1.Controls.Add(mapCanvasHost);
        _mapTabSplit.Panel2.Controls.Add(_mapGrid);

        var mapTab = new TabPage("地图");
        mapTab.Controls.Add(_mapTabSplit);
        var linksTab = new TabPage("连接");
        _linksTabSplit.Panel1.Controls.Add(_linksGraph);
        _linksTabSplit.Panel2.Controls.Add(_linksSplit);
        linksTab.Controls.Add(_linksTabSplit);
        _tabs.TabPages.Add(mapTab);
        _tabs.TabPages.Add(linksTab);

        _rootSplit.Panel2.Controls.Add(_tabs);

        _status.Items.Add(_statusText);

        Controls.Add(_rootSplit);
        Controls.Add(_status);
        Controls.Add(menu);

        MainMenuStrip = menu;

        _statusHintTimer.Tick += (_, _) =>
        {
            _statusHintTimer.Stop();
            UpdateStatus();
        };

        ReloadMapList(selectFirst: true);
        UpdateStatus();
    }

    private void ShowStatusHint(string text, int ms = 2200)
    {
        _statusText.Text = text ?? "";
        _statusHintTimer.Stop();
        _statusHintTimer.Interval = Math.Clamp(ms, 250, 10000);
        _statusHintTimer.Start();
    }

    private void SetCollisionToolsVisible(MapCanvas.CanvasViewMode mode)
    {
        var tileSetCollision = mode == MapCanvas.CanvasViewMode.TileSetCollision;
        var layoutCollision = mode == MapCanvas.CanvasViewMode.LayoutCollision;
        var inCollisionView = tileSetCollision || layoutCollision;
        var layoutPoly = layoutCollision && _collisionLayout != null && _collisionLayout.Polygons.Count > 0;

        _collisionEditModeLabel.Visible = inCollisionView;
        _collisionEditModeCombo.Visible = inCollisionView;
        _collisionModeLabel.Visible = inCollisionView;
        _collisionModeCombo.Visible = inCollisionView;

        var showTransformTools = tileSetCollision || layoutCollision;
        _toolModeLabel.Visible = showTransformTools;
        _toolSelect.Visible = showTransformTools;
        _toolVertex.Visible = showTransformTools;
        _toolMove.Visible = showTransformTools;
        _toolRotate.Visible = showTransformTools;
        _toolScale.Visible = showTransformTools;
        _toolAddSquareCollision.Visible = tileSetCollision;
        _toolRemoveCollision.Visible = tileSetCollision;

        if (showTransformTools)
        {
            if (layoutCollision && !layoutPoly)
                _canvas.SetToolMode(MapCanvas.CollisionToolMode.Select);
            UpdateToolButtons(_canvas.GetToolMode());
        }
        else
        {
            _canvas.SetToolMode(MapCanvas.CollisionToolMode.Select);
            _toolAddSquareCollision.Checked = false;
            _toolRemoveCollision.Checked = false;
            _canvas.CancelPendingTileCollisionAction();
        }

        var enableTransform = tileSetCollision || layoutPoly;
        _toolVertex.Enabled = enableTransform;
        _toolMove.Enabled = enableTransform;
        _toolRotate.Enabled = enableTransform;
        _toolScale.Enabled = enableTransform;

        _collisionTargetLabel.Visible = layoutCollision;
        _collisionTargetCombo.Visible = layoutCollision;
        _collisionInitButton.Visible = layoutCollision;
        _collisionLoadButton.Visible = layoutCollision;
        _collisionSaveButton.Visible = layoutCollision;
        _collisionSaveButton.Enabled = layoutCollision && _collisionLayoutDirty;
    }

    private void UpdateToolButtons(MapCanvas.CollisionToolMode mode)
    {
        _toolSelect.CheckedChanged -= ToolCheckedChangedGuard;
        _toolVertex.CheckedChanged -= ToolCheckedChangedGuard;
        _toolMove.CheckedChanged -= ToolCheckedChangedGuard;
        _toolRotate.CheckedChanged -= ToolCheckedChangedGuard;
        _toolScale.CheckedChanged -= ToolCheckedChangedGuard;

        _toolSelect.Checked = mode == MapCanvas.CollisionToolMode.Select;
        _toolVertex.Checked = mode == MapCanvas.CollisionToolMode.Vertex;
        _toolMove.Checked = mode == MapCanvas.CollisionToolMode.Move;
        _toolRotate.Checked = mode == MapCanvas.CollisionToolMode.Rotate;
        _toolScale.Checked = mode == MapCanvas.CollisionToolMode.Scale;

        _toolSelect.CheckedChanged += ToolCheckedChangedGuard;
        _toolVertex.CheckedChanged += ToolCheckedChangedGuard;
        _toolMove.CheckedChanged += ToolCheckedChangedGuard;
        _toolRotate.CheckedChanged += ToolCheckedChangedGuard;
        _toolScale.CheckedChanged += ToolCheckedChangedGuard;
    }

    private void ToolCheckedChangedGuard(object? sender, EventArgs e)
    {
        if (sender == _toolSelect && _toolSelect.Checked)
            SetToolMode(MapCanvas.CollisionToolMode.Select);
        else if (sender == _toolVertex && _toolVertex.Checked)
            SetToolMode(MapCanvas.CollisionToolMode.Vertex);
        else if (sender == _toolMove && _toolMove.Checked)
            SetToolMode(MapCanvas.CollisionToolMode.Move);
        else if (sender == _toolRotate && _toolRotate.Checked)
            SetToolMode(MapCanvas.CollisionToolMode.Rotate);
        else if (sender == _toolScale && _toolScale.Checked)
            SetToolMode(MapCanvas.CollisionToolMode.Scale);

        if (sender == _toolVertex || sender == _toolMove || sender == _toolRotate || sender == _toolScale)
        {
            _toolAddSquareCollision.Checked = false;
            _toolRemoveCollision.Checked = false;
            _canvas.CancelPendingTileCollisionAction();
        }
    }

    private void SetToolMode(MapCanvas.CollisionToolMode mode)
    {
        _canvas.SetToolMode(mode);
        UpdateToolButtons(mode);
    }

    private enum CollisionEditTarget
    {
        Tile = 0,
        ForegroundTexture = 1
    }

    private sealed class CollisionLayoutData
    {
        public int RoomWidth { get; set; }
        public int RoomHeight { get; set; }
        public bool[] Solid { get; set; } = [];
        public List<List<GodotVector2>> Polygons { get; set; } = [];

        public static CollisionLayoutData Create(int roomWidth, int roomHeight)
        {
            roomWidth = Math.Max(1, roomWidth);
            roomHeight = Math.Max(1, roomHeight);
            return new CollisionLayoutData
            {
                RoomWidth = roomWidth,
                RoomHeight = roomHeight,
                Solid = new bool[roomWidth * roomHeight],
                Polygons = []
            };
        }
    }

    private static CollisionLayoutData CloneCollisionLayoutData(CollisionLayoutData src)
    {
        var clone = new CollisionLayoutData
        {
            RoomWidth = src.RoomWidth,
            RoomHeight = src.RoomHeight,
            Solid = (bool[])src.Solid.Clone(),
            Polygons = []
        };

        if (src.Polygons != null && src.Polygons.Count > 0)
        {
            clone.Polygons = src.Polygons
                .Select(p => p?.Select(v => v).ToList() ?? [])
                .ToList();
        }

        return clone;
    }

    private static bool CollisionLayoutDataEquals(CollisionLayoutData a, CollisionLayoutData b)
    {
        if (a.RoomWidth != b.RoomWidth || a.RoomHeight != b.RoomHeight)
            return false;
        if (a.Solid.Length != b.Solid.Length)
            return false;
        for (var i = 0; i < a.Solid.Length; i++)
        {
            if (a.Solid[i] != b.Solid[i])
                return false;
        }

        var ap = a.Polygons ?? [];
        var bp = b.Polygons ?? [];
        if (ap.Count != bp.Count)
            return false;
        for (var pi = 0; pi < ap.Count; pi++)
        {
            var p1 = ap[pi] ?? [];
            var p2 = bp[pi] ?? [];
            if (p1.Count != p2.Count)
                return false;
            for (var vi = 0; vi < p1.Count; vi++)
            {
                if (p1[vi].X != p2[vi].X || p1[vi].Y != p2[vi].Y)
                    return false;
            }
        }

        return true;
    }

    private void ReloadCollisionGridForSelectedMap()
    {
        var map = _selectedMap;
        if (map == null)
        {
            _collisionLayout = null;
            _collisionLayoutDirty = false;
            _canvas.SetLayoutCollision(null);
            _collisionSaveButton.Enabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(_godotRoot))
            return;

        var resPath = GetCollisionDataResPath(map, _collisionEditTarget, ensureDefault: false);
        if (string.IsNullOrWhiteSpace(resPath))
        {
            _collisionLayout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            _collisionLayoutDirty = false;
            _canvas.SetLayoutCollision(_collisionLayout);
            _collisionSaveButton.Enabled = false;
            return;
        }

        var abs = ToAbsoluteGodotPath(_godotRoot, resPath);
        CollisionLayoutData layout;
        if (File.Exists(abs))
        {
            try
            {
                var json = File.ReadAllText(abs);
                layout = JsonSerializer.Deserialize<CollisionLayoutData>(json, JsonOptions.Default) ?? CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            }
            catch
            {
                layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            }
        }
        else
        {
            layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
        }
        layout.Polygons ??= [];

        if (layout.RoomWidth != Math.Max(1, map.RoomWidth) || layout.RoomHeight != Math.Max(1, map.RoomHeight))
        {
            var resized = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            var copyW = Math.Min(layout.RoomWidth, resized.RoomWidth);
            var copyH = Math.Min(layout.RoomHeight, resized.RoomHeight);
            for (var y = 0; y < copyH; y++)
            {
                for (var x = 0; x < copyW; x++)
                {
                    var fromIdx = y * layout.RoomWidth + x;
                    var toIdx = y * resized.RoomWidth + x;
                    if (fromIdx >= 0 && fromIdx < layout.Solid.Length && toIdx >= 0 && toIdx < resized.Solid.Length)
                        resized.Solid[toIdx] = layout.Solid[fromIdx];
                }
            }
            resized.Polygons = layout.Polygons;
            layout = resized;
        }

        if (layout.Solid.Length != layout.RoomWidth * layout.RoomHeight)
        {
            var fixedLayout = CollisionLayoutData.Create(layout.RoomWidth, layout.RoomHeight);
            var copy = Math.Min(layout.Solid.Length, fixedLayout.Solid.Length);
            Array.Copy(layout.Solid, fixedLayout.Solid, copy);
            fixedLayout.Polygons = layout.Polygons;
            layout = fixedLayout;
        }

        _collisionLayout = layout;
        _collisionLayoutDirty = false;
        _canvas.SetLayoutCollision(_collisionLayout);
        _collisionSaveButton.Enabled = false;

        if (_viewModeCombo.SelectedIndex == 1 && _collisionEditModeCombo.SelectedIndex == 1)
            SetCollisionToolsVisible(MapCanvas.CanvasViewMode.LayoutCollision);
    }

    private void SaveCollisionGridForSelectedMap()
    {
        var map = _selectedMap;
        if (map == null || _collisionLayout == null)
            return;
        if (string.IsNullOrWhiteSpace(_godotRoot))
            return;

        var resPath = GetCollisionDataResPath(map, _collisionEditTarget, ensureDefault: true);
        if (string.IsNullOrWhiteSpace(resPath))
            return;

        var abs = ToAbsoluteGodotPath(_godotRoot, resPath);
        try
        {
            var dir = Path.GetDirectoryName(abs);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_collisionLayout, JsonOptions.Default);
            File.WriteAllText(abs, json);
            _collisionLayoutDirty = false;
            _collisionSaveButton.Enabled = false;
            if (!TryWriteBackCollisionMeta(map))
                return;
            UpdateStatus();
            ShowStatusHint($"保存成功：{resPath}");
        }
        catch (Exception ex)
        {
            ShowStatusHint("保存失败");
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void InitializeCollisionForSelectedMap()
    {
        var map = _selectedMap;
        if (map == null)
            return;

        CollisionLayoutData layout;
        if (_collisionEditTarget == CollisionEditTarget.ForegroundTexture)
            layout = BuildForegroundTextureCollisionLayout(map);
        else
            layout = BuildTileCollisionLayout(map);

        _collisionLayout = layout;
        _collisionLayoutDirty = true;
        _canvas.SetLayoutCollision(_collisionLayout);
        _collisionSaveButton.Enabled = true;
        if (_viewModeCombo.SelectedIndex == 1 && _collisionEditModeCombo.SelectedIndex == 1)
            SetCollisionToolsVisible(MapCanvas.CanvasViewMode.LayoutCollision);
        UpdateStatus();
    }

    private CollisionLayoutData BuildTileCollisionLayout(MapDefinition map)
    {
        var layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
        if (map.TileLayers.Count == 0)
            return layout;

        var layer = map.TileLayers.FirstOrDefault(l =>
                (l.Name ?? "").Contains("Foreground", StringComparison.OrdinalIgnoreCase)
                || (l.Name ?? "").Contains("前景", StringComparison.OrdinalIgnoreCase))
            ?? map.TileLayers.FirstOrDefault(l => l.ZIndex == 3)
            ?? map.TileLayers.OrderByDescending(l => l.ZIndex).FirstOrDefault();
        if (layer == null || layer.Cells.Count == 0)
            return layout;

        var w = layout.RoomWidth;
        var h = layout.RoomHeight;
        foreach (var cell in layer.Cells)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= w || cell.Y >= h)
                continue;
            var idx = cell.Y * w + cell.X;
            if (idx >= 0 && idx < layout.Solid.Length)
                layout.Solid[idx] = true;
        }

        var worldW = Math.Max(1, map.RoomWidth) * 32;
        var worldH = Math.Max(1, map.RoomHeight) * 32;
        layout.Polygons = BuildCollisionPolygonsFromSolidTiles(layout.Solid, w, h, worldW, worldH);
        return layout;
    }

    private CollisionLayoutData BuildForegroundTextureCollisionLayout(MapDefinition map)
    {
        var layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
        if (string.IsNullOrWhiteSpace(_godotRoot) || string.IsNullOrWhiteSpace(map.ForegroundTexturePath))
            return BuildForegroundTextureCollisionLayoutFallbackToTiles(map, layout);

        var abs = ToAbsoluteGodotPath(_godotRoot, map.ForegroundTexturePath);
        if (!File.Exists(abs))
            return BuildForegroundTextureCollisionLayoutFallbackToTiles(map, layout);

        Bitmap? bmp = null;
        try
        {
            bmp = new Bitmap(abs);
        }
        catch
        {
            bmp?.Dispose();
            return BuildForegroundTextureCollisionLayoutFallbackToTiles(map, layout);
        }

        using (bmp)
        {
            using var bmp32 = Ensure32bppArgb(bmp);
            var w = layout.RoomWidth;
            var h = layout.RoomHeight;
            if (w <= 0 || h <= 0 || bmp32.Width <= 0 || bmp32.Height <= 0)
                return BuildForegroundTextureCollisionLayoutFallbackToTiles(map, layout);

            const int threshold = 254;
            var roomWorldW = Math.Max(1, map.RoomWidth) * 32;
            var roomWorldH = Math.Max(1, map.RoomHeight) * 32;
            var upscale = Math.Max(0.0001f, map.ForegroundTextureUpscale);
            var texWorldWf = bmp32.Width * upscale;
            var texWorldHf = bmp32.Height * upscale;
            var texWorldW = Math.Max(1, (int)MathF.Round(texWorldWf));
            var texWorldH = Math.Max(1, (int)MathF.Round(texWorldHf));
            var (ox, oy) = ComputeAnchorOffset(map.ForegroundTextureAnchor, roomWorldW, roomWorldH, texWorldWf, texWorldHf);

            layout.Polygons = BuildCollisionPolygonsFromAlpha(bmp32, texWorldW, texWorldH, threshold);
            if (layout.Polygons.Count > 0)
            {
                OffsetPolygons(layout.Polygons, ox, oy);
                return layout;
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var worldX0 = x * 32f;
                    var worldY0 = y * 32f;
                    var worldX1 = (x + 1) * 32f;
                    var worldY1 = (y + 1) * 32f;

                    var lx0 = (worldX0 - ox) / upscale;
                    var ly0 = (worldY0 - oy) / upscale;
                    var lx1 = (worldX1 - ox) / upscale;
                    var ly1 = (worldY1 - oy) / upscale;

                    var px0 = (int)MathF.Floor(lx0);
                    var py0 = (int)MathF.Floor(ly0);
                    var px1 = (int)MathF.Floor(lx1) - 1;
                    var py1 = (int)MathF.Floor(ly1) - 1;

                    if (px1 < 0 || py1 < 0 || px0 >= bmp32.Width || py0 >= bmp32.Height)
                        continue;

                    px0 = Math.Clamp(px0, 0, bmp32.Width - 1);
                    py0 = Math.Clamp(py0, 0, bmp32.Height - 1);
                    px1 = Math.Clamp(px1, 0, bmp32.Width - 1);
                    py1 = Math.Clamp(py1, 0, bmp32.Height - 1);

                    var a = SampleRectAverageAlpha(bmp32, px0, py0, px1, py1);
                    if (a > threshold)
                    {
                        var idx = y * w + x;
                        if (idx >= 0 && idx < layout.Solid.Length)
                            layout.Solid[idx] = true;
                    }
                }
            }
        }

        layout.Polygons = BuildCollisionPolygonsFromSolidTiles(layout.Solid, layout.RoomWidth, layout.RoomHeight, Math.Max(1, map.RoomWidth) * 32, Math.Max(1, map.RoomHeight) * 32);
        if (layout.Polygons.Count > 0)
            return layout;

        return BuildForegroundTextureCollisionLayoutFallbackToTiles(map, layout);
    }

    private static (float x, float y) ComputeAnchorOffset(TextureAnchor anchor, float roomW, float roomH, float texW, float texH)
    {
        return anchor switch
        {
            TextureAnchor.TopLeft => (0, 0),
            TextureAnchor.TopRight => (roomW - texW, 0),
            TextureAnchor.BottomLeft => (0, roomH - texH),
            TextureAnchor.BottomRight => (roomW - texW, roomH - texH),
            TextureAnchor.Center => ((roomW - texW) / 2f, (roomH - texH) / 2f),
            _ => (0, 0)
        };
    }

    private static void OffsetPolygons(List<List<GodotVector2>> polygons, float ox, float oy)
    {
        if (polygons == null || polygons.Count == 0)
            return;
        for (var pi = 0; pi < polygons.Count; pi++)
        {
            var p = polygons[pi];
            if (p == null || p.Count == 0)
                continue;
            for (var i = 0; i < p.Count; i++)
            {
                var v = p[i];
                p[i] = new GodotVector2(v.X + ox, v.Y + oy);
            }
        }
    }

    private static int SampleRectAverageAlpha(Bitmap bmp, int x0, int y0, int x1, int y1)
    {
        var w = Math.Max(1, x1 - x0 + 1);
        var h = Math.Max(1, y1 - y0 + 1);
        var sx = w >= 8 ? 4 : 2;
        var sy = h >= 8 ? 4 : 2;

        var sum = 0;
        var count = 0;
        for (var yi = 0; yi < sy; yi++)
        {
            var y = y0 + (int)MathF.Round(yi * (h - 1) / (float)Math.Max(1, sy - 1));
            for (var xi = 0; xi < sx; xi++)
            {
                var x = x0 + (int)MathF.Round(xi * (w - 1) / (float)Math.Max(1, sx - 1));
                var c = bmp.GetPixel(x, y);
                sum += c.A;
                count++;
            }
        }
        return count == 0 ? 0 : (int)MathF.Round(sum / (float)count);
    }

    private static List<List<GodotVector2>> BuildCollisionPolygonsFromAlpha(Bitmap bmp, int worldW, int worldH, int threshold)
    {
        worldW = Math.Max(1, worldW);
        worldH = Math.Max(1, worldH);
        if (bmp.Width <= 0 || bmp.Height <= 0)
            return [];

        threshold = Math.Clamp(threshold, 0, 254);

        const int maxDim = 4096;
        var gridW = Math.Max(1, Math.Min(bmp.Width, maxDim));
        var gridH = Math.Max(1, Math.Min(bmp.Height, maxDim));
        var scaleX = bmp.Width / (float)gridW;
        var scaleY = bmp.Height / (float)gridH;

        var solidPixels = new bool[gridW * gridH];

        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData? data = null;
        try
        {
            data = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
            var bytesPerPixel = Image.GetPixelFormatSize(data.PixelFormat) / 8;
            if (bytesPerPixel < 4)
                return [];

            var stride = data.Stride;
            var absStride = Math.Abs(stride);
            var totalBytes = absStride * data.Height;
            var buffer = new byte[totalBytes];
            Marshal.Copy(data.Scan0, buffer, 0, totalBytes);

            var anySolid = false;
            var anyEmpty = false;
            for (var y = 0; y < gridH; y++)
            {
                var srcY = (int)MathF.Round((y + 0.5f) * scaleY - 0.5f);
                srcY = Math.Clamp(srcY, 0, bmp.Height - 1);
                var rowY = stride > 0 ? srcY : (bmp.Height - 1 - srcY);
                var row = rowY * absStride;
                for (var x = 0; x < gridW; x++)
                {
                    var srcX = (int)MathF.Round((x + 0.5f) * scaleX - 0.5f);
                    srcX = Math.Clamp(srcX, 0, bmp.Width - 1);
                    var idx = row + srcX * bytesPerPixel;
                    var a = buffer[idx + 3];
                    var solid = a > threshold;
                    solidPixels[y * gridW + x] = solid;
                    anySolid |= solid;
                    anyEmpty |= !solid;
                }
            }

            if (!anySolid)
                return [];
            if (!anyEmpty)
            {
                return
                [
                    [
                        new GodotVector2(0, 0),
                        new GodotVector2(worldW, 0),
                        new GodotVector2(worldW, worldH),
                        new GodotVector2(0, worldH)
                    ]
                ];
            }
        }
        catch
        {
            return [];
        }
        finally
        {
            if (data != null)
                bmp.UnlockBits(data);
        }

        return BuildCollisionPolygonsFromSolidPixelEdges(solidPixels, gridW, gridH, worldW, worldH, 5f);
    }

    private static List<List<GodotVector2>> BuildCollisionPolygonsFromSolidPixelEdges(bool[] solidPixels, int gridW, int gridH, int worldW, int worldH, float stepPx)
    {
        gridW = Math.Max(1, gridW);
        gridH = Math.Max(1, gridH);
        worldW = Math.Max(1, worldW);
        worldH = Math.Max(1, worldH);
        if (solidPixels.Length != gridW * gridH)
            return [];

        static int VKey(int x, int y) => (x << 16) | (y & 0xFFFF);

        var edges = new List<Edge>(Math.Min(1_000_000, gridW * gridH / 2));
        var outgoing = new Dictionary<int, List<int>>();
        void AddEdge(int sx, int sy, int ex, int ey, byte dir)
        {
            var idx = edges.Count;
            edges.Add(new Edge(sx, sy, ex, ey, dir));
            var key = VKey(sx, sy);
            if (!outgoing.TryGetValue(key, out var list))
            {
                list = [];
                outgoing[key] = list;
            }
            list.Add(idx);
        }

        bool IsSolid(int x, int y) => x >= 0 && y >= 0 && x < gridW && y < gridH && solidPixels[y * gridW + x];

        const int maxEdges = 6_000_000;
        for (var y = 0; y < gridH; y++)
        {
            var row = y * gridW;
            for (var x = 0; x < gridW; x++)
            {
                if (!solidPixels[row + x])
                    continue;

                if (y == 0 || !IsSolid(x, y - 1))
                {
                    AddEdge(x, y, x + 1, y, 0);
                    if (edges.Count > maxEdges)
                        return [];
                }
                if (x == gridW - 1 || !IsSolid(x + 1, y))
                {
                    AddEdge(x + 1, y, x + 1, y + 1, 1);
                    if (edges.Count > maxEdges)
                        return [];
                }
                if (y == gridH - 1 || !IsSolid(x, y + 1))
                {
                    AddEdge(x + 1, y + 1, x, y + 1, 2);
                    if (edges.Count > maxEdges)
                        return [];
                }
                if (x == 0 || !IsSolid(x - 1, y))
                {
                    AddEdge(x, y + 1, x, y, 3);
                    if (edges.Count > maxEdges)
                        return [];
                }
            }
        }

        if (edges.Count == 0)
            return [];

        var polys = new List<List<GodotVector2>>();
        var nextStart = 0;
        var maxWalk = edges.Count + 16;
        while (true)
        {
            while (nextStart < edges.Count && edges[nextStart].Used)
                nextStart++;
            if (nextStart >= edges.Count)
                break;

            var startEdgeIndex = nextStart;
            var startEdge = edges[startEdgeIndex];
            startEdge.Used = true;
            edges[startEdgeIndex] = startEdge;

            var startX = startEdge.Sx;
            var startY = startEdge.Sy;
            var curX = startEdge.Ex;
            var curY = startEdge.Ey;
            var prevDir = startEdge.Dir;

            var loop = new List<Point>(512) { new Point(startX, startY), new Point(curX, curY) };
            for (var guard = 0; guard < maxWalk; guard++)
            {
                if (curX == startX && curY == startY)
                    break;

                var curKey = VKey(curX, curY);
                if (!outgoing.TryGetValue(curKey, out var candidates))
                    break;

                var best = -1;
                var bestRank = int.MaxValue;
                for (var i = 0; i < candidates.Count; i++)
                {
                    var ei = candidates[i];
                    var e = edges[ei];
                    if (e.Used)
                        continue;

                    var cost = (e.Dir - prevDir + 4) & 3;
                    var rank = cost switch
                    {
                        1 => 0,
                        0 => 1,
                        3 => 2,
                        _ => 3
                    };

                    if (rank < bestRank)
                    {
                        bestRank = rank;
                        best = ei;
                        if (rank == 0)
                            break;
                    }
                }

                if (best < 0)
                    break;

                var chosen = edges[best];
                chosen.Used = true;
                edges[best] = chosen;

                prevDir = chosen.Dir;
                curX = chosen.Ex;
                curY = chosen.Ey;
                loop.Add(new Point(curX, curY));
            }

            if (loop.Count < 4)
                continue;
            if (loop[^1].X != startX || loop[^1].Y != startY)
                continue;
            loop.RemoveAt(loop.Count - 1);

            loop = RemoveCollinear(loop);
            if (loop.Count < 3)
                continue;

            var sampled = ResampleClosed(loop, Math.Max(1f, stepPx));
            if (sampled.Count < 3)
                continue;

            var worldPoly = new List<GodotVector2>(sampled.Count);
            for (var i = 0; i < sampled.Count; i++)
            {
                var wx = sampled[i].X / gridW * worldW;
                var wy = sampled[i].Y / gridH * worldH;
                worldPoly.Add(new GodotVector2(wx, wy));
            }

            if (worldPoly.Count >= 3)
                polys.Add(worldPoly);
        }

        polys.Sort((a, b) => MathF.Abs(PolygonArea(b)).CompareTo(MathF.Abs(PolygonArea(a))));
        return polys;
    }

    private static float PolygonArea(List<GodotVector2> pts)
    {
        if (pts.Count < 3)
            return 0f;
        double sum = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            sum += a.X * b.Y - a.Y * b.X;
        }
        return (float)(sum * 0.5);
    }

    private static List<Point> RemoveCollinear(List<Point> pts)
    {
        if (pts.Count < 4)
            return pts;

        var outPts = new List<Point>(pts.Count);
        for (var i = 0; i < pts.Count; i++)
        {
            var prev = pts[(i - 1 + pts.Count) % pts.Count];
            var cur = pts[i];
            var next = pts[(i + 1) % pts.Count];
            var dx1 = cur.X - prev.X;
            var dy1 = cur.Y - prev.Y;
            var dx2 = next.X - cur.X;
            var dy2 = next.Y - cur.Y;
            if (dx1 == dx2 && dy1 == dy2)
                continue;
            outPts.Add(cur);
        }

        return outPts.Count >= 3 ? outPts : pts;
    }

    private static List<PointF> ResampleClosed(List<Point> pts, float step)
    {
        if (pts.Count < 3)
            return [];

        var input = new List<PointF>(pts.Count);
        for (var i = 0; i < pts.Count; i++)
            input.Add(new PointF(pts[i].X, pts[i].Y));

        var sampled = new List<PointF>();
        var first = input[0];
        sampled.Add(first);

        var acc = 0f;
        var lastAdded = first;
        for (var i = 0; i < input.Count; i++)
        {
            var a = input[i];
            var b = input[(i + 1) % input.Count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var segLen = MathF.Sqrt(dx * dx + dy * dy);
            if (segLen < 0.0001f)
                continue;

            var t0 = 0f;
            while (acc + segLen * (1f - t0) >= step)
            {
                var need = step - acc;
                var t = t0 + need / segLen;
                var p = new PointF(a.X + dx * t, a.Y + dy * t);
                if (MathF.Abs(p.X - lastAdded.X) > 0.001f || MathF.Abs(p.Y - lastAdded.Y) > 0.001f)
                {
                    sampled.Add(p);
                    lastAdded = p;
                }
                t0 = t;
                acc = 0f;
            }
            acc += segLen * (1f - t0);
        }

        if (sampled.Count >= 2)
        {
            var last = sampled[^1];
            if (MathF.Abs(last.X - first.X) < 0.001f && MathF.Abs(last.Y - first.Y) < 0.001f)
                sampled.RemoveAt(sampled.Count - 1);
        }

        return sampled;
    }

    private struct Edge(int sx, int sy, int ex, int ey, byte dir)
    {
        public int Sx = sx;
        public int Sy = sy;
        public int Ex = ex;
        public int Ey = ey;
        public byte Dir = dir;
        public bool Used;
    }

    private static Bitmap Ensure32bppArgb(Bitmap src)
    {
        if (src.PixelFormat == PixelFormat.Format32bppArgb || src.PixelFormat == PixelFormat.Format32bppPArgb)
            return (Bitmap)src.Clone();

        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.DrawImage(src, 0, 0, src.Width, src.Height);
        return dst;
    }

    private static List<List<GodotVector2>> BuildCollisionPolygonsFromSolidTiles(bool[] solidTiles, int roomW, int roomH, int worldW, int worldH)
    {
        worldW = Math.Max(1, worldW);
        worldH = Math.Max(1, worldH);
        roomW = Math.Max(1, roomW);
        roomH = Math.Max(1, roomH);
        if (solidTiles.Length != roomW * roomH)
            return [];

        var gridW = worldW;
        var gridH = worldH;
        var samplesW = gridW + 1;
        var samplesH = gridH + 1;
        var samples = new bool[samplesW * samplesH];
        for (var y = 0; y <= gridH; y++)
        {
            var cellY = Math.Min(roomH - 1, y / 32);
            var rowBase = cellY * roomW;
            for (var x = 0; x <= gridW; x++)
            {
                var cellX = Math.Min(roomW - 1, x / 32);
                samples[y * samplesW + x] = solidTiles[rowBase + cellX];
            }
        }
        return BuildCollisionPolygonsFromSamples(samples, gridW, gridH, worldW, worldH);
    }

    private static List<List<GodotVector2>> BuildCollisionPolygonsFromSamples(bool[] samples, int gridW, int gridH, int worldW, int worldH)
    {
        gridW = Math.Max(1, gridW);
        gridH = Math.Max(1, gridH);
        worldW = Math.Max(1, worldW);
        worldH = Math.Max(1, worldH);

        var samplesW = gridW + 1;
        var samplesH = gridH + 1;
        if (samples.Length != samplesW * samplesH)
            return [];

        var neighbors = new Dictionary<int, List<int>>();
        var edges = new HashSet<long>();

        static int Key(int x, int y) => (x << 16) | (y & 0xFFFF);
        static long EdgeKey(int a, int b)
        {
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }
        static void AddNeighbor(Dictionary<int, List<int>> map, int a, int b)
        {
            if (!map.TryGetValue(a, out var list))
            {
                list = [];
                map[a] = list;
            }
            list.Add(b);
        }
        void AddEdge(int a, int b)
        {
            var ek = EdgeKey(a, b);
            if (!edges.Add(ek))
                return;
            AddNeighbor(neighbors, a, b);
            AddNeighbor(neighbors, b, a);
        }

        for (var y = 0; y < gridH; y++)
        {
            for (var x = 0; x < gridW; x++)
            {
                var tl = samples[y * samplesW + x];
                var tr = samples[y * samplesW + (x + 1)];
                var br = samples[(y + 1) * samplesW + (x + 1)];
                var bl = samples[(y + 1) * samplesW + x];

                var idx = (tl ? 1 : 0) | (tr ? 2 : 0) | (br ? 4 : 0) | (bl ? 8 : 0);
                if (idx == 0 || idx == 15)
                    continue;

                var top = Key(2 * x + 1, 2 * y);
                var right = Key(2 * x + 2, 2 * y + 1);
                var bottom = Key(2 * x + 1, 2 * y + 2);
                var left = Key(2 * x, 2 * y + 1);

                switch (idx)
                {
                    case 1:
                        AddEdge(left, top);
                        break;
                    case 2:
                        AddEdge(top, right);
                        break;
                    case 3:
                        AddEdge(left, right);
                        break;
                    case 4:
                        AddEdge(right, bottom);
                        break;
                    case 5:
                        AddEdge(left, top);
                        AddEdge(right, bottom);
                        break;
                    case 6:
                        AddEdge(top, bottom);
                        break;
                    case 7:
                        AddEdge(left, bottom);
                        break;
                    case 8:
                        AddEdge(bottom, left);
                        break;
                    case 9:
                        AddEdge(top, bottom);
                        break;
                    case 10:
                        AddEdge(top, right);
                        AddEdge(bottom, left);
                        break;
                    case 11:
                        AddEdge(right, bottom);
                        break;
                    case 12:
                        AddEdge(left, right);
                        break;
                    case 13:
                        AddEdge(top, right);
                        break;
                    case 14:
                        AddEdge(left, top);
                        break;
                }
            }
        }

        if (edges.Count == 0)
            return [];

        static (int x, int y) Decode(int key)
        {
            var x = key >> 16;
            var y = (short)(key & 0xFFFF);
            return (x, y);
        }

        bool HasUnusedEdge(int a, int b) => edges.Contains(EdgeKey(a, b));
        void RemoveEdge(int a, int b) => edges.Remove(EdgeKey(a, b));

        int PickNext(int current, int previous)
        {
            if (!neighbors.TryGetValue(current, out var list))
                return -1;
            for (var i = 0; i < list.Count; i++)
            {
                var cand = list[i];
                if (cand == previous)
                    continue;
                if (HasUnusedEdge(current, cand))
                    return cand;
            }
            for (var i = 0; i < list.Count; i++)
            {
                var cand = list[i];
                if (HasUnusedEdge(current, cand))
                    return cand;
            }
            return -1;
        }

        bool SampleWorld(float wx, float wy)
        {
            var sx = (int)MathF.Round(wx / worldW * gridW);
            var sy = (int)MathF.Round(wy / worldH * gridH);
            sx = Math.Clamp(sx, 0, gridW);
            sy = Math.Clamp(sy, 0, gridH);
            return samples[sy * samplesW + sx];
        }

        var polys = new List<List<GodotVector2>>();
        var maxWalk = edges.Count + 16;
        while (edges.Count > 0)
        {
            var first = edges.First();
            var a = (int)(first >> 32);
            var b = (int)(first & 0xFFFFFFFF);

            var loopKeys = new List<int> { a };
            var prev = a;
            var cur = b;
            RemoveEdge(a, b);

            for (var guard = 0; guard < maxWalk; guard++)
            {
                loopKeys.Add(cur);
                var next = PickNext(cur, prev);
                if (next < 0)
                    break;
                prev = cur;
                cur = next;
                RemoveEdge(prev, cur);
                if (cur == a)
                    break;
            }

            if (loopKeys.Count < 4 || loopKeys[^1] != a)
                continue;

            loopKeys.RemoveAt(loopKeys.Count - 1);
            var loop = new List<GodotVector2>(loopKeys.Count);
            for (var i = 0; i < loopKeys.Count; i++)
            {
                var (kx, ky) = Decode(loopKeys[i]);
                var gx = kx / 2f;
                var gy = ky / 2f;
                var wx = gx / gridW * worldW;
                var wy = gy / gridH * worldH;
                loop.Add(new GodotVector2(wx, wy));
            }

            loop = SimplifyPolygon(loop, 0.9f);
            loop = SimplifyPolygonRdp(loop, 6f);
            if (loop.Count < 3)
                continue;

            var c = AveragePoint(loop);
            if (!SampleWorld(c.X, c.Y))
                continue;

            polys.Add(loop);
        }

        return polys;
    }

    private static GodotVector2 AveragePoint(List<GodotVector2> pts)
    {
        if (pts.Count == 0)
            return new GodotVector2(0, 0);
        float sx = 0;
        float sy = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            sx += pts[i].X;
            sy += pts[i].Y;
        }
        return new GodotVector2(sx / pts.Count, sy / pts.Count);
    }

    private static List<GodotVector2> SimplifyPolygonRdp(List<GodotVector2> pts, float epsilon)
    {
        if (pts.Count < 4)
            return pts;
        epsilon = MathF.Max(0.0001f, epsilon);

        var line = new List<GodotVector2>(pts.Count + 1);
        line.AddRange(pts);
        line.Add(pts[0]);

        var keep = new bool[line.Count];
        keep[0] = true;
        keep[^1] = true;

        var stack = new Stack<(int a, int b)>();
        stack.Push((0, line.Count - 1));

        var eps2 = epsilon * epsilon;
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            if (b <= a + 1)
                continue;

            var maxD2 = -1f;
            var maxI = -1;
            for (var i = a + 1; i < b; i++)
            {
                var d2 = DistancePointToSegmentSquared(line[i], line[a], line[b]);
                if (d2 > maxD2)
                {
                    maxD2 = d2;
                    maxI = i;
                }
            }

            if (maxI >= 0 && maxD2 > eps2)
            {
                keep[maxI] = true;
                stack.Push((a, maxI));
                stack.Push((maxI, b));
            }
        }

        var simplified = new List<GodotVector2>(line.Count);
        for (var i = 0; i < line.Count; i++)
        {
            if (keep[i])
                simplified.Add(line[i]);
        }

        if (simplified.Count > 1)
            simplified.RemoveAt(simplified.Count - 1);

        return simplified.Count >= 3 ? simplified : pts;
    }

    private static float DistancePointToSegmentSquared(GodotVector2 p, GodotVector2 a, GodotVector2 b)
    {
        var vx = b.X - a.X;
        var vy = b.Y - a.Y;
        var wx = p.X - a.X;
        var wy = p.Y - a.Y;

        var denom = vx * vx + vy * vy;
        if (denom < 0.000001f)
        {
            var dx = p.X - a.X;
            var dy = p.Y - a.Y;
            return dx * dx + dy * dy;
        }

        var t = (wx * vx + wy * vy) / denom;
        if (t <= 0f)
        {
            var dx = p.X - a.X;
            var dy = p.Y - a.Y;
            return dx * dx + dy * dy;
        }
        if (t >= 1f)
        {
            var dx = p.X - b.X;
            var dy = p.Y - b.Y;
            return dx * dx + dy * dy;
        }

        var projX = a.X + t * vx;
        var projY = a.Y + t * vy;
        var ddx = p.X - projX;
        var ddy = p.Y - projY;
        return ddx * ddx + ddy * ddy;
    }

    private CollisionLayoutData BuildForegroundTextureCollisionLayoutFallbackToTiles(MapDefinition map, CollisionLayoutData layout)
    {
        if (map.TileLayers.Count == 0)
            return layout;
        var layer = map.TileLayers.FirstOrDefault(l => l.ZIndex == 3)
            ?? map.TileLayers.FirstOrDefault(l =>
                (l.Name ?? "").Contains("Foreground", StringComparison.OrdinalIgnoreCase)
                || (l.Name ?? "").Contains("前景", StringComparison.OrdinalIgnoreCase))
            ?? map.TileLayers.OrderByDescending(l => l.ZIndex).FirstOrDefault();
        if (layer == null || layer.Cells.Count == 0)
            return layout;

        var w = layout.RoomWidth;
        var h = layout.RoomHeight;
        foreach (var cell in layer.Cells)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= w || cell.Y >= h)
                continue;
            var idx = cell.Y * w + cell.X;
            if (idx >= 0 && idx < layout.Solid.Length)
                layout.Solid[idx] = true;
        }

        var worldW = Math.Max(1, map.RoomWidth) * 32;
        var worldH = Math.Max(1, map.RoomHeight) * 32;
        layout.Polygons = BuildCollisionPolygonsFromSolidTiles(layout.Solid, w, h, worldW, worldH);
        return layout;
    }

    private static List<GodotVector2> SimplifyPolygon(List<GodotVector2> pts, float minSegmentLen)
    {
        if (pts.Count < 4)
            return pts;
        minSegmentLen = MathF.Max(0.0001f, minSegmentLen);
        var minSeg2 = minSegmentLen * minSegmentLen;

        var filtered = new List<GodotVector2>(pts.Count);
        filtered.Add(pts[0]);
        for (var i = 1; i < pts.Count; i++)
        {
            var p = pts[i];
            var last = filtered[^1];
            var dx = p.X - last.X;
            var dy = p.Y - last.Y;
            if (dx * dx + dy * dy >= minSeg2)
                filtered.Add(p);
        }
        if (filtered.Count >= 3)
        {
            var first = filtered[0];
            var last = filtered[^1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            if (dx * dx + dy * dy < minSeg2)
                filtered.RemoveAt(filtered.Count - 1);
        }

        if (filtered.Count < 4)
            return filtered;

        var simplified = new List<GodotVector2>(filtered.Count);
        for (var i = 0; i < filtered.Count; i++)
        {
            var prev = filtered[(i - 1 + filtered.Count) % filtered.Count];
            var cur = filtered[i];
            var next = filtered[(i + 1) % filtered.Count];
            var ax = cur.X - prev.X;
            var ay = cur.Y - prev.Y;
            var bx = next.X - cur.X;
            var by = next.Y - cur.Y;
            var cross = ax * by - ay * bx;
            if (MathF.Abs(cross) > 0.01f)
                simplified.Add(cur);
        }

        return simplified.Count >= 3 ? simplified : filtered;
    }

    private string GetCollisionDataResPath(MapDefinition map, CollisionEditTarget target, bool ensureDefault)
    {
        var resPath = target == CollisionEditTarget.ForegroundTexture ? map.ForegroundTextureCollisionDataPath : map.TileCollisionDataPath;
        resPath = (resPath ?? "").Trim();
        if (resPath.Length > 0)
        {
            if (!resPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                resPath = "";
        }
        if (resPath.Length > 0)
            return resPath;
        if (!ensureDefault)
            return "";

        var def = BuildDefaultCollisionDataResPath(map, target);
        if (def.Length == 0)
            return "";

        if (target == CollisionEditTarget.ForegroundTexture)
            map.ForegroundTextureCollisionDataPath = def;
        else
            map.TileCollisionDataPath = def;

        _mapGrid.Refresh();
        return def;
    }

    private string BuildDefaultCollisionDataResPath(MapDefinition map, CollisionEditTarget target)
    {
        var scene = (map.ScenePath ?? "").Trim();
        if (!scene.StartsWith("res://", StringComparison.Ordinal))
            return "";
        var rel = scene["res://".Length..].TrimStart('/').Replace('\\', '/');
        var sceneDir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
        var baseName = Path.GetFileNameWithoutExtension(rel) ?? "";
        if (baseName.Length == 0)
            baseName = map.DisplayName.Length > 0 ? map.DisplayName : "Map";
        var safe = SanitizeFolderName(baseName);
        if (safe.Length == 0)
            safe = "Map";

        var fileName = target == CollisionEditTarget.ForegroundTexture ? "collision_fgtex.json" : "collision_tile.json";
        var resRel = (sceneDir.Length > 0 ? (sceneDir.TrimEnd('/') + "/") : "") + "Resources/" + safe + "/" + fileName;
        return "res://" + resRel;
    }

    private bool TryWriteBackCollisionMeta(MapDefinition map)
    {
        if (string.IsNullOrWhiteSpace(_godotRoot))
            return false;
        if (string.IsNullOrWhiteSpace(map.ScenePath) || !map.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            return false;

        var sceneAbsPath = ToAbsoluteGodotPath(_godotRoot, map.ScenePath);
        if (!File.Exists(sceneAbsPath))
            return false;

        try
        {
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var mapNode = FindTscnNode(scene, "Map");
            if (mapNode == null)
                return false;

            if (map.CollisionUsed == CollisionMode.ForegroundTexture && !IsTemplateRoomMap(scene, mapNode))
            {
                if (EnsureForegroundTextureWorldNodesInSceneFile(sceneAbsPath))
                {
                    scene = TscnParser.ParseFile(sceneAbsPath);
                    mapNode = FindTscnNode(scene, "Map");
                    if (mapNode == null)
                        return false;
                }
            }

            var mode = map.CollisionUsed == CollisionMode.ForegroundTexture ? "foreground_texture" : "tile_foreground";
            var tilePath = GetCollisionDataResPath(map, CollisionEditTarget.Tile, ensureDefault: true);
            var fgPath = GetCollisionDataResPath(map, CollisionEditTarget.ForegroundTexture, ensureDefault: true);
            mapNode.RawProps["metadata/collision_mode"] = $"\"{mode}\"";
            mapNode.RawProps["metadata/collision_tile_path"] = $"\"{tilePath}\"";
            mapNode.RawProps["metadata/collision_fgtex_path"] = $"\"{fgPath}\"";
            TscnWriter.PatchFile(sceneAbsPath, scene, ["metadata/collision_mode", "metadata/collision_tile_path", "metadata/collision_fgtex_path"]);
            return true;
        }
        catch (Exception ex)
        {
            ShowStatusHint("写回碰撞元数据失败");
            MessageBox.Show(this, ex.Message, "写回失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void ApplyRootSplitLayout()
    {
        const int leftWidth = 200;
        _rootSplit.FixedPanel = FixedPanel.Panel1;
        _rootSplit.Panel1MinSize = leftWidth;
        if (_rootSplit.SplitterDistance != leftWidth)
            _rootSplit.SplitterDistance = leftWidth;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { ShowItemToolTips = true };

        var file = new ToolStripMenuItem("文件");
        var importFromGodot = new ToolStripMenuItem("从 Godot 重新加载...", null, (_, _) => ImportFromGodot()) { ToolTipText = "扫描当前 Godot 工程内所有疑似地图的 .tscn 并载入（实时编辑模式）。" };
        var exit = new ToolStripMenuItem("退出", null, (_, _) => Close());
        var newProject = new ToolStripMenuItem("New Project", null, (_, _) => NewProject()) { ShortcutKeys = Keys.Control | Keys.N, ToolTipText = "Create a blank MapEditor project." };
        var openProject = new ToolStripMenuItem("Open Project...", null, (_, _) => OpenProject()) { ShortcutKeys = Keys.Control | Keys.O, ToolTipText = "Open a MapEditor project JSON file." };
        var saveProject = new ToolStripMenuItem("Save Project", null, (_, _) => SaveProject()) { ShortcutKeys = Keys.Control | Keys.S, ToolTipText = "Save the current MapEditor project JSON file." };
        var saveProjectAs = new ToolStripMenuItem("Save Project As...", null, (_, _) => SaveProjectAs()) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.S, ToolTipText = "Save the current MapEditor project JSON to a new file." };
        file.DropDownItems.AddRange([newProject, openProject, saveProject, saveProjectAs, new ToolStripSeparator(), importFromGodot, new ToolStripSeparator(), exit]);

        var edit = new ToolStripMenuItem("编辑");
        var undo = new ToolStripMenuItem("撤回", null, (_, _) => Undo()) { ShortcutKeys = Keys.Control | Keys.Z };
        var redo = new ToolStripMenuItem("重做", null, (_, _) => Redo()) { ShortcutKeys = Keys.Control | Keys.Y };
        edit.DropDownOpening += (_, _) =>
        {
            undo.Enabled = _undo.CanUndo;
            redo.Enabled = _undo.CanRedo;
            undo.Text = _undo.CanUndo ? $"撤回: {_undo.PeekUndoName()}" : "撤回";
            redo.Text = _undo.CanRedo ? $"重做: {_undo.PeekRedoName()}" : "重做";
        };
        edit.DropDownItems.AddRange([undo, redo]);

        menu.Items.AddRange([file, edit]);
        menu.Dock = DockStyle.Top;
        return menu;
    }

    private void NewProject()
    {
        _project.ResetToDefault();
        _currentPath = null;
        ReloadMapList(selectFirst: true);
        ReloadLinksList(selectFirst: true);
        UpdateStatus();
    }

    private void OpenProject()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "地图工程 (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "打开地图工程"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK)
            return;

        var json = File.ReadAllText(ofd.FileName);
        var loaded = JsonSerializer.Deserialize<MapProject>(json, JsonOptions.Default);
        if (loaded == null)
            return;

        _project.CopyFrom(loaded);
        _currentPath = ofd.FileName;
        _undo.Clear();
        ReloadMapList(selectFirst: true);
        ReloadLinksList(selectFirst: true);
        UpdateStatus();
    }

    private void ImportFromGodot()
    {
        try
        {
            var root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
            var loaded = GodotMapImporter.ImportFromGodot(root);
            _project.CopyFrom(loaded);
            _currentPath = null;
            _godotRoot = root;
            GodotRootContext.CurrentRoot = root;
            _pinnedStartingMapPath = ReadPinnedStartingMapPath(root);
            _undo.Clear();
            ReloadMapList(selectFirst: true);
            ReloadLinksList(selectFirst: true);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveProject()
    {
        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            SaveProjectAs();
            return;
        }

        var json = JsonSerializer.Serialize(_project, JsonOptions.Default);
        File.WriteAllText(_currentPath, json);
        UpdateStatus();
    }

    private void SaveProjectAs()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "地图工程 (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "保存地图工程",
            FileName = Path.GetFileName(_currentPath) ?? "map_project.json"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK)
            return;

        _currentPath = sfd.FileName;
        SaveProject();
    }

    private void AddMap()
    {
        var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            MessageBox.Show(this, "未找到 Godot 工程根目录。请先从 Godot 重新加载工程。", "新增地图", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var mapsAbsDir = Path.Combine(root, "CoreEngine", "Maps");
        if (!Directory.Exists(mapsAbsDir))
        {
            MessageBox.Show(this, "未找到 CoreEngine/Maps 目录。", "新增地图", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var defaultName = $"NewMap{_project.Maps.Count + 1}";
        var name = PromptText(this, "新增地图", "请输入地图名称：", defaultName);
        if (name == null)
            return;
        name = name.Trim();
        if (name.Length == 0)
            name = defaultName;

        var safeBase = SanitizeFolderName(name);
        if (safeBase.Length == 0)
            safeBase = defaultName;

        var fileBase = safeBase;
        var tryIndex = 1;
        while (File.Exists(Path.Combine(mapsAbsDir, fileBase + ".tscn"))
            || _project.Maps.Any(m => string.Equals(Path.GetFileNameWithoutExtension(m.ScenePath), fileBase, StringComparison.OrdinalIgnoreCase)))
        {
            tryIndex++;
            fileBase = $"{safeBase}{tryIndex}";
        }

        var sceneResPath = $"res://CoreEngine/Maps/{fileBase}.tscn";
        var tileColResPath = $"res://CoreEngine/Maps/Resources/{fileBase}/collision_tile.json";
        var fgColResPath = $"res://CoreEngine/Maps/Resources/{fileBase}/collision_fgtex.json";

        var sceneAbsPath = ToAbsoluteGodotPath(root, sceneResPath);
        var tileColAbsPath = ToAbsoluteGodotPath(root, tileColResPath);
        var fgColAbsPath = ToAbsoluteGodotPath(root, fgColResPath);
        Directory.CreateDirectory(Path.GetDirectoryName(sceneAbsPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(tileColAbsPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(fgColAbsPath)!);

        var blankLayout = CollisionLayoutData.Create(27, 15);
        File.WriteAllText(tileColAbsPath, JsonSerializer.Serialize(blankLayout, JsonOptions.Default));
        File.WriteAllText(fgColAbsPath, JsonSerializer.Serialize(blankLayout, JsonOptions.Default));

        var sceneText =
$"""
[gd_scene load_steps=3 format=4]

[ext_resource type="TileSet" path="res://CoreEngine/Resources/Tileset.tres" id="1_tileset"]
[ext_resource type="PackedScene" path="res://addons/MetroidvaniaSystem/Nodes/RoomInstance.tscn" id="2_room"]

[node name="Map" type="Node2D"]
metadata/collision_mode = "tile_foreground"
metadata/collision_tile_path = "{tileColResPath}"
metadata/collision_fgtex_path = "{fgColResPath}"

[node name="TileMap" type="Node2D" parent="."]

[node name="Foreground" type="TileMapLayer" parent="TileMap"]
use_parent_material = true
tile_set = ExtResource("1_tileset")
tile_map_data = PackedByteArray()

[node name="Background" type="TileMapLayer" parent="TileMap"]
visible = false
z_index = -1
use_parent_material = true
tile_set = ExtResource("1_tileset")
tile_map_data = PackedByteArray()

[node name="RoomInstance" parent="." instance=ExtResource("2_room")]
""";
        File.WriteAllText(sceneAbsPath, sceneText.Replace("\r\n", "\n"));

        var map = new MapDefinition
        {
            Id = sceneResPath,
            DisplayName = name,
            ScenePath = sceneResPath,
            Kind = MapKind.Vertical,
            RoomWidth = 27,
            RoomHeight = 15,
            CollisionUsed = CollisionMode.TileForeground,
            TileCollisionDataPath = tileColResPath,
            ForegroundTextureCollisionDataPath = fgColResPath,
            TileLayers =
            [
                new TileLayer
                {
                    Name = "Foreground",
                    NodePath = "TileMap/Foreground",
                    TileSetPath = "res://CoreEngine/Resources/Tileset.tres",
                    Visible = true,
                    ZIndex = 0
                },
                new TileLayer
                {
                    Name = "Background",
                    NodePath = "TileMap/Background",
                    TileSetPath = "res://CoreEngine/Resources/Tileset.tres",
                    Visible = false,
                    ZIndex = -1
                }
            ]
        };

        _project.Maps.Add(map);
        _godotRoot = root;
        GodotRootContext.CurrentRoot = root;
        ReloadMapList(selectLast: true);
        ReloadLinksList();
        UpdateStatus();
    }

    private void RemoveSelectedMap()
    {
        var selected = _mapsList.SelectedItem as MapDefinition;
        if (selected == null)
            return;

        var confirm = MessageBox.Show(
            this,
            $"确定删除地图“{selected.DisplayName}”？\n\n将从工程中移除，并尝试删除对应的场景与资源文件。",
            "删除地图",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.OK)
            return;

        var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
        var removedPinnedStart = IsPinnedStartingMap(selected);
        if (removedPinnedStart && !string.IsNullOrWhiteSpace(root))
        {
            try
            {
                WritePinnedStartingMapPath(root, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "清除置顶失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(root))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(selected.ScenePath) && selected.ScenePath.StartsWith("res://", StringComparison.Ordinal))
                {
                    var abs = ToAbsoluteGodotPath(root, selected.ScenePath);
                    if (File.Exists(abs))
                        File.Delete(abs);
                }

                var resPaths = new[] { selected.TileCollisionDataPath, selected.ForegroundTextureCollisionDataPath }
                    .Where(p => !string.IsNullOrWhiteSpace(p) && p.StartsWith("res://", StringComparison.Ordinal))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var rp in resPaths)
                {
                    var abs = ToAbsoluteGodotPath(root, rp);
                    if (File.Exists(abs))
                        File.Delete(abs);
                }

                var anyRes = resPaths.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(anyRes))
                {
                    var dir = Path.GetDirectoryName(ToAbsoluteGodotPath(root, anyRes));
                    var rootAbs = Path.GetFullPath(root);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        dir = Path.GetFullPath(dir);
                        if (dir.StartsWith(rootAbs, StringComparison.OrdinalIgnoreCase) && Directory.Exists(dir))
                            Directory.Delete(dir, recursive: true);
                    }
                }
            }
            catch
            {
            }
        }

        _project.RemoveMapById(selected.Id);
        if (removedPinnedStart)
            _pinnedStartingMapPath = "";
        ReloadMapList(selectFirst: true);
        ReloadLinksList(selectFirst: true);
        UpdateStatus();
    }

    private static string? PromptText(IWin32Window owner, string title, string label, string defaultValue)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowIcon = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Width = 420,
            Height = 160
        };

        var lbl = new Label { Left = 12, Top = 12, Width = 380, Text = label };
        var tb = new TextBox { Left = 12, Top = 38, Width = 380, Text = defaultValue ?? "" };
        var ok = new Button { Text = "确定", Left = 232, Width = 80, Top = 74, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", Left = 312, Width = 80, Top = 74, DialogResult = DialogResult.Cancel };

        form.Controls.AddRange([lbl, tb, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        form.Shown += (_, _) =>
        {
            tb.SelectionStart = tb.TextLength;
            tb.SelectionLength = 0;
            tb.Focus();
        };

        return form.ShowDialog(owner) == DialogResult.OK ? tb.Text : null;
    }

    private void AddLink()
    {
        var fromMap = _mapsList.SelectedItem as MapDefinition;
        var toMap = _project.Maps.FirstOrDefault(m => !ReferenceEquals(m, fromMap)) ?? fromMap ?? _project.Maps.FirstOrDefault();
        if (fromMap == null || toMap == null)
            return;

        var link = new MapLink
        {
            From = new LinkEndpoint { MapId = fromMap.Id, PortalId = "" },
            To = new LinkEndpoint { MapId = toMap.Id, PortalId = "" }
        };
        _project.Links.Add(link);
        ReloadLinksList(selectLast: true);
        UpdateStatus();
        _tabs.SelectedIndex = 1;
    }

    private void RemoveSelectedLink()
    {
        var selected = _linksList.SelectedItem as MapLink;
        if (selected == null)
            return;

        _project.Links.Remove(selected);
        ReloadLinksList(selectFirst: true);
        UpdateStatus();
    }

    private void FormatMapListItem(object? sender, ListControlConvertEventArgs e)
    {
        if (e.ListItem is not MapDefinition map)
            return;
        var name = string.IsNullOrWhiteSpace(map.DisplayName) ? map.Id : map.DisplayName;
        e.Value = IsPinnedStartingMap(map) ? $"[置顶] {name}" : name;
    }

    private bool IsPinnedStartingMap(MapDefinition map)
    {
        var scenePath = NormalizeResPath(map.ScenePath);
        return scenePath.Length > 0 && string.Equals(scenePath, NormalizeResPath(_pinnedStartingMapPath), StringComparison.OrdinalIgnoreCase);
    }

    private void SetSelectedMapAsPinnedStart()
    {
        if (_mapsList.SelectedItem is not MapDefinition map)
            return;
        if (string.IsNullOrWhiteSpace(map.ScenePath) || !map.ScenePath.StartsWith("res://", StringComparison.Ordinal))
        {
            MessageBox.Show(this, "该地图没有有效的 Godot 场景路径，不能设为启动地图。", "设为置顶", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
        try
        {
            WritePinnedStartingMapPath(root, map.ScenePath);
            _godotRoot = root;
            GodotRootContext.CurrentRoot = root;
            _pinnedStartingMapPath = NormalizeResPath(map.ScenePath);
            _mapsList.Refresh();
            ShowStatusHint($"置顶房间已设为 {map.DisplayName}");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "设为置顶失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ReadPinnedStartingMapPath(string godotRoot)
    {
        var gamePath = Path.Combine(godotRoot, "CoreEngine", "Game.tscn");
        if (!File.Exists(gamePath))
            return "";

        var scene = TscnParser.ParseFile(gamePath);
        var gameNode = scene.Nodes.FirstOrDefault(x => string.Equals(x.Name, "Game", StringComparison.Ordinal));
        if (gameNode == null || !gameNode.RawProps.TryGetValue("starting_map", out var raw))
            return "";

        var value = UnquoteGodotString(raw);
        if (!value.StartsWith("uid://", StringComparison.OrdinalIgnoreCase))
            return NormalizeResPath(value);

        var resolved = ResolveUidToResPath(godotRoot, value);
        return NormalizeResPath(resolved);
    }

    private static void WritePinnedStartingMapPath(string godotRoot, string scenePath)
    {
        var gamePath = Path.Combine(godotRoot, "CoreEngine", "Game.tscn");
        if (!File.Exists(gamePath))
            throw new FileNotFoundException("未找到 CoreEngine/Game.tscn。", gamePath);

        var scene = TscnParser.ParseFile(gamePath);
        var gameNode = scene.Nodes.FirstOrDefault(x => string.Equals(x.Name, "Game", StringComparison.Ordinal));
        if (gameNode == null)
            throw new InvalidOperationException("CoreEngine/Game.tscn 中没有找到 Game 节点。");

        gameNode.RawProps["starting_map"] = string.IsNullOrWhiteSpace(scenePath) ? "\"\"" : $"\"{NormalizeResPath(scenePath)}\"";
        TscnWriter.PatchFile(gamePath, scene, ["starting_map"]);
    }

    private static string ResolveUidToResPath(string godotRoot, string uid)
    {
        foreach (var file in Directory.EnumerateFiles(godotRoot, "*.tscn", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}GodotTools{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var scene = TscnParser.ParseFile(file);
                if (!string.Equals(scene.SceneUid, uid, StringComparison.OrdinalIgnoreCase))
                    continue;
                return TryMakeResPath(godotRoot, file);
            }
            catch
            {
            }
        }
        return "";
    }

    private static string UnquoteGodotString(string raw)
    {
        raw = raw.Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw[1..^1];
        return raw;
    }

    private static string NormalizeResPath(string? value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0)
            return "";
        return value.Replace('\\', '/');
    }

    private void ReloadMapList(bool selectFirst = false, bool selectLast = false)
    {
        var prev = _mapsList.SelectedItem as MapDefinition;

        _mapsList.BeginUpdate();
        _mapsList.Items.Clear();
        foreach (var map in _project.Maps)
            _mapsList.Items.Add(map);
        _mapsList.EndUpdate();

        if (_mapsList.Items.Count == 0)
        {
            _mapGrid.SelectedObject = _project;
            return;
        }

        if (selectLast)
        {
            _mapsList.SelectedIndex = _mapsList.Items.Count - 1;
            return;
        }

        if (prev != null)
        {
            var idx = _project.Maps.FindIndex(m => m.Id == prev.Id);
            if (idx >= 0)
            {
                _mapsList.SelectedIndex = idx;
                return;
            }
        }

        if (selectFirst)
            _mapsList.SelectedIndex = 0;

        _linksGraph.SetData(_project, _selectedMap, _linksList.SelectedItem as MapLink);
    }

    private void ReloadLinksList(bool selectFirst = false, bool selectLast = false)
    {
        var prev = _linksList.SelectedItem as MapLink;

        _linksList.BeginUpdate();
        _linksList.Items.Clear();
        foreach (var link in _project.Links)
            _linksList.Items.Add(link);
        _linksList.EndUpdate();

        if (_linksList.Items.Count == 0)
        {
            _linkGrid.SelectedObject = _project;
            return;
        }

        if (selectLast)
        {
            _linksList.SelectedIndex = _linksList.Items.Count - 1;
            return;
        }

        if (prev != null)
        {
            var idx = _project.Links.FindIndex(l => ReferenceEquals(l, prev));
            if (idx >= 0)
            {
                _linksList.SelectedIndex = idx;
                return;
            }
        }

        if (selectFirst)
            _linksList.SelectedIndex = 0;

        _linksGraph.SetData(_project, _selectedMap, _linksList.SelectedItem as MapLink);
    }

    private void OnSelectedMapChanged()
    {
        var selected = _mapsList.SelectedItem as MapDefinition;
        _selectedMap = selected;
        SelectedMapContext.CurrentMap = selected;
        EnsureResPathEditorProvider((object?)selected ?? _project);
        _mapGrid.SelectedObject = (object?)selected ?? _project;
        AutoFitPropertyGridLabelWidth(_mapGrid);
        _canvas.SetMap(selected, _godotRoot);
        UpdateCollisionModeComboForSelectedMap();
        if (_viewModeCombo.SelectedIndex == 1 && _collisionEditModeCombo.SelectedIndex == 1)
            ReloadCollisionGridForSelectedMap();
        _linksGraph.SetData(_project, _selectedMap, _linksList.SelectedItem as MapLink);
        UpdateStatus();
    }

    private void UpdateCollisionModeComboForSelectedMap()
    {
        var map = _selectedMap;
        if (map == null)
            return;
        _collisionModeCombo.SelectedIndexChanged -= CollisionModeComboSelectedIndexChangedGuard;
        _collisionModeCombo.SelectedIndex = map.CollisionUsed == CollisionMode.ForegroundTexture ? 1 : 0;
        _collisionModeCombo.SelectedIndexChanged += CollisionModeComboSelectedIndexChangedGuard;
    }

    private void CollisionModeComboSelectedIndexChangedGuard(object? sender, EventArgs e)
    {
        var map = _selectedMap;
        if (map == null)
            return;
        var mode = _collisionModeCombo.SelectedIndex == 1 ? CollisionMode.ForegroundTexture : CollisionMode.TileForeground;
        if (map.CollisionUsed == mode)
            return;
        map.CollisionUsed = mode;
        _mapGrid.Refresh();
        TryWriteBackCollisionMeta(map);
        UpdateStatus();
    }

    private void OnSelectedLinkChanged()
    {
        var selected = _linksList.SelectedItem as MapLink;
        EnsureResPathEditorProvider((object?)selected ?? _project);
        _linkGrid.SelectedObject = (object?)selected ?? _project;
        AutoFitPropertyGridLabelWidth(_linkGrid);
        _linksGraph.SetData(_project, _selectedMap, selected);
    }

    private void EnsureResPathEditorProvider(object obj)
    {
        var t = obj.GetType();
        if (_resPathEditorProviderApplied.Add(t))
            TypeDescriptor.AddProvider(new AutoResPathEditorTypeDescriptionProvider(), t);
    }

    private void UpdateStatus()
    {
        var root = string.IsNullOrWhiteSpace(_godotRoot) ? "(未加载 Godot 项目)" : _godotRoot;
        var current = _selectedMap == null ? "" : (_selectedMap.ScenePath.Length > 0 ? _selectedMap.ScenePath : _selectedMap.DisplayName);
        var tail = current.Length > 0 ? $" | 当前: {current}" : "";
        var undoTail = _undo.CanUndo || _undo.CanRedo ? $" | 撤回:{_undo.UndoCount} 重做:{_undo.RedoCount}" : "";
        _statusText.Text = $"实时编辑 | 地图: {_project.Maps.Count} | 连接: {_project.Links.Count} | Godot: {root}{tail}{undoTail}";
    }

    private string BuildEntityHoverText(PlacedEntity ent)
    {
        var name = ent.Prefab.Length > 0 ? Path.GetFileNameWithoutExtension(ent.Prefab) : "";
        var kind = ent.Type.Length > 0 ? ent.Type : "Entity";
        return name.Length > 0 ? $"{kind}: {name}" : kind;
    }

    private string BuildPortalHoverText(Portal portal)
    {
        var targetId = ResolvePortalTargetMapId(portal);
        var targetName = ResolveMapDisplayName(targetId);
        if (targetName.Length == 0)
            targetName = ResolveFallbackDisplayName(portal.TargetMapId);

        if (targetName.Length == 0)
            return $"Portal: {portal.Name}";

        return $"Portal: {portal.Name}\n→ {targetName}";
    }

    private void JumpToPortalTarget(Portal portal)
    {
        var targetId = ResolvePortalTargetMapId(portal);
        if (targetId.Length == 0)
            return;
        JumpToMap(targetId);
    }

    private void JumpToMap(string mapId)
    {
        if (mapId.Length == 0)
            return;
        var idx = _project.Maps.FindIndex(m =>
            string.Equals(m.Id, mapId, StringComparison.Ordinal)
            || string.Equals(m.ScenePath, mapId, StringComparison.Ordinal));
        if (idx < 0)
            return;
        _mapsList.SelectedIndex = idx;
        _tabs.SelectedIndex = 0;
    }

    private void AddPortalAtWorld(float x, float y)
    {
        var map = _selectedMap;
        if (map == null)
            return;

        var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            MessageBox.Show(this, "未找到 Godot 工程根目录。请先从 Godot 重新加载工程。", "新增传送门", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var name = PromptText(this, "新增传送门", "请输入传送门名称：", "Portal");
        if (name == null)
            return;
        name = name.Trim();
        if (name.Length == 0)
            name = "Portal";

        var sceneAbsPath = EnsureMapSceneExists(root, map);
        if (string.IsNullOrWhiteSpace(sceneAbsPath))
            return;

        var portalResPath = FindExistingPortalPrefabResPath(sceneAbsPath) ?? "res://CoreEngine/Objects/Portal.tscn";
        var portalAbs = ToAbsoluteGodotPath(root, portalResPath);
        if (!File.Exists(portalAbs))
        {
            MessageBox.Show(this, $"未找到 Portal 预制体：{portalResPath}", "新增传送门", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var scene = TscnParser.ParseFile(sceneAbsPath);
        var existingNames = new HashSet<string>(
            scene.Nodes.Select(n => (n.Name ?? "").Trim()).Where(s => s.Length > 0),
            StringComparer.Ordinal);

        var safeBase = SanitizeFolderName(name);
        if (safeBase.Length == 0)
            safeBase = "Portal";

        var uniqueName = MakeUniqueName(safeBase, n => existingNames.Contains(n));

        if (!TryAppendPortalNodeToTscn(sceneAbsPath, scene, portalResPath, ".", uniqueName, x, y))
            return;

        map.Portals.Add(new Portal
        {
            Id = uniqueName,
            Name = uniqueName,
            NodePath = uniqueName,
            X = x,
            Y = y,
            TargetMapId = "",
            TargetPortalId = ""
        });

        _mapGrid.Refresh();
        _canvas.Invalidate();
        _linksGraph.SetData(_project, _selectedMap, _linksList.SelectedItem as MapLink);
        _linksGraph.Invalidate();
        UpdateStatus();
    }

    private static string MakeUniqueName(string baseName, Func<string, bool> exists)
    {
        baseName = (baseName ?? "").Trim();
        if (baseName.Length == 0)
            baseName = "Portal";
        var n = baseName;
        var i = 0;
        while (exists(n))
        {
            i++;
            n = $"{baseName}_{i}";
        }
        return n;
    }

    private string EnsureMapSceneExists(string godotRoot, MapDefinition map)
    {
        var sceneResPath = (map.ScenePath ?? "").Trim();
        var mapId = (map.Id ?? "").Trim();
        if (sceneResPath.Length == 0 && mapId.StartsWith("res://", StringComparison.Ordinal))
        {
            sceneResPath = mapId;
            map.ScenePath = sceneResPath;
        }
        if (sceneResPath.Length == 0 || !sceneResPath.StartsWith("res://", StringComparison.Ordinal))
        {
            MessageBox.Show(this, "该地图缺少有效的 ScenePath（res://...）。", "新增传送门", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return "";
        }

        var sceneAbsPath = ToAbsoluteGodotPath(godotRoot, sceneResPath);
        if (File.Exists(sceneAbsPath))
            return sceneAbsPath;

        Directory.CreateDirectory(Path.GetDirectoryName(sceneAbsPath)!);

        var baseName = Path.GetFileNameWithoutExtension(sceneResPath);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "NewMap";

        if (string.IsNullOrWhiteSpace(map.TileCollisionDataPath))
            map.TileCollisionDataPath = $"res://CoreEngine/Maps/Resources/{baseName}/collision_tile.json";
        if (string.IsNullOrWhiteSpace(map.ForegroundTextureCollisionDataPath))
            map.ForegroundTextureCollisionDataPath = $"res://CoreEngine/Maps/Resources/{baseName}/collision_fgtex.json";

        try
        {
            var blankLayout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            var tileColAbs = ToAbsoluteGodotPath(godotRoot, map.TileCollisionDataPath);
            var fgColAbs = ToAbsoluteGodotPath(godotRoot, map.ForegroundTextureCollisionDataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(tileColAbs)!);
            Directory.CreateDirectory(Path.GetDirectoryName(fgColAbs)!);
            if (!File.Exists(tileColAbs))
                File.WriteAllText(tileColAbs, JsonSerializer.Serialize(blankLayout, JsonOptions.Default));
            if (!File.Exists(fgColAbs))
                File.WriteAllText(fgColAbs, JsonSerializer.Serialize(blankLayout, JsonOptions.Default));
        }
        catch
        {
        }

        var sceneText =
$"""
[gd_scene load_steps=3 format=4]

[ext_resource type="TileSet" path="res://CoreEngine/Resources/Tileset.tres" id="1_tileset"]
[ext_resource type="PackedScene" path="res://addons/MetroidvaniaSystem/Nodes/RoomInstance.tscn" id="2_room"]

[node name="Map" type="Node2D"]
metadata/collision_mode = "tile_foreground"
metadata/collision_tile_path = "{(map.TileCollisionDataPath ?? "").Trim()}"
metadata/collision_fgtex_path = "{(map.ForegroundTextureCollisionDataPath ?? "").Trim()}"

[node name="TileMap" type="Node2D" parent="."]

[node name="Foreground" type="TileMapLayer" parent="TileMap"]
use_parent_material = true
tile_set = ExtResource("1_tileset")
tile_map_data = PackedByteArray()

[node name="Background" type="TileMapLayer" parent="TileMap"]
visible = false
z_index = -1
use_parent_material = true
tile_set = ExtResource("1_tileset")
tile_map_data = PackedByteArray()

[node name="RoomInstance" parent="." instance=ExtResource("2_room")]
""";
        File.WriteAllText(sceneAbsPath, sceneText.Replace("\r\n", "\n"));
        return sceneAbsPath;
    }

    private static string? FindExistingPortalPrefabResPath(string sceneAbsPath)
    {
        try
        {
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var hit = scene.ExtResources.FirstOrDefault(r =>
                string.Equals((r.Type ?? "").Trim(), "PackedScene", StringComparison.Ordinal)
                && ((r.Path ?? "").Trim().EndsWith("/Portal.tscn", StringComparison.OrdinalIgnoreCase)));
            var p = (hit?.Path ?? "").Trim();
            return p.Length == 0 ? null : p;
        }
        catch
        {
            return null;
        }
    }

    private bool TryAppendPortalNodeToTscn(string sceneAbsPath, TscnScene scene, string portalResPath, string nodeParent, string nodeName, float x, float y)
    {
        var portalResNorm = (portalResPath ?? "").Trim();
        if (portalResNorm.Length == 0)
            return false;

        nodeParent = (nodeParent ?? "").Trim();
        if (nodeParent.Length == 0)
            nodeParent = ".";
        nodeName = (nodeName ?? "").Trim();
        if (nodeName.Length == 0)
            return false;

        var nodePath = ComputeNodePath(nodeParent, nodeName);
        if (scene.Nodes.Any(n => string.Equals(ComputeNodePath(n.Parent, n.Name), nodePath, StringComparison.Ordinal)))
        {
            PatchNodePosition(sceneAbsPath, nodePath, x, y);
            return true;
        }

        var portalExt = scene.ExtResources.FirstOrDefault(r =>
            string.Equals((r.Type ?? "").Trim(), "PackedScene", StringComparison.Ordinal)
            && string.Equals((r.Path ?? "").Trim(), portalResNorm, StringComparison.Ordinal));
        var extId = (portalExt?.Id ?? "").Trim();

        var lines = File.ReadAllLines(sceneAbsPath).ToList();

        if (extId.Length == 0)
        {
            var existingIds = new HashSet<string>(
                scene.ExtResources.Select(r => (r.Id ?? "").Trim()).Where(s => s.Length > 0),
                StringComparer.Ordinal);
            var num = 1;
            foreach (var id in existingIds)
            {
                var p = id.Split('_', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (p.Length >= 1 && int.TryParse(p[0], out var n) && n >= num)
                    num = n + 1;
            }

            extId = $"{num}_portal";
            while (existingIds.Contains(extId))
            {
                num++;
                extId = $"{num}_portal";
            }

            var insertAt = FindExtResourceInsertIndex(lines);
            lines.Insert(insertAt, $"[ext_resource type=\"PackedScene\" path=\"{portalResNorm.Replace("\\", "/")}\" id=\"{extId}\"]");
        }

        UpdateGdSceneLoadSteps(lines);

        var posRaw = FormatVector2(x, y);
        lines.Add("");
        lines.Add($"[node name=\"{nodeName}\" parent=\"{nodeParent}\" instance=ExtResource(\"{extId}\")]");
        lines.Add($"position = {posRaw}");
        lines.Add("target_map = \"\"");
        lines.Add("target_area = &\"\"");

        File.WriteAllLines(sceneAbsPath, lines);
        return true;
    }

    private static int FindExtResourceInsertIndex(List<string> lines)
    {
        var lastExt = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[ext_resource", StringComparison.Ordinal))
                lastExt = i;
        }
        if (lastExt >= 0)
            return lastExt + 1;

        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[node", StringComparison.Ordinal) || t.StartsWith("[sub_resource", StringComparison.Ordinal))
                return i;
        }
        return lines.Count;
    }

    private static void UpdateGdSceneLoadSteps(List<string> lines)
    {
        var extCount = 0;
        var subCount = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[ext_resource", StringComparison.Ordinal))
                extCount++;
            else if (t.StartsWith("[sub_resource", StringComparison.Ordinal))
                subCount++;
        }

        var loadSteps = extCount + subCount + 1;

        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (!t.StartsWith("[gd_scene", StringComparison.Ordinal))
                continue;
            var line = lines[i];
            var m = Regex.Match(line, "\\bload_steps=(?<n>\\d+)\\b", RegexOptions.CultureInvariant);
            if (!m.Success)
                return;
            lines[i] = Regex.Replace(line, "\\bload_steps=\\d+\\b", $"load_steps={loadSteps}", RegexOptions.CultureInvariant);
            return;
        }
    }

    private void SelectOrCreateLinkForPortal(string fromMapId, string fromPortalId)
    {
        if (fromMapId.Length == 0 || fromPortalId.Length == 0)
            return;

        var fromMap = _project.Maps.FirstOrDefault(m => string.Equals(m.Id, fromMapId, StringComparison.Ordinal));
        if (fromMap == null)
            return;
        var portal = fromMap.Portals.FirstOrDefault(p => string.Equals(p.Id, fromPortalId, StringComparison.Ordinal));
        if (portal == null)
            return;

        var link = _project.Links.FirstOrDefault(l =>
            string.Equals(l.From.MapId, fromMapId, StringComparison.Ordinal) &&
            string.Equals(l.From.PortalId, fromPortalId, StringComparison.Ordinal));

        if (link == null)
        {
            var toMapId = portal.TargetMapId?.Trim() ?? "";
            if (toMapId.Length == 0)
            {
                var other = _project.Maps.FirstOrDefault(m => !string.Equals(m.Id, fromMapId, StringComparison.Ordinal));
                toMapId = other?.Id ?? fromMapId;
            }

            link = new MapLink
            {
                From = new LinkEndpoint { MapId = fromMapId, PortalId = fromPortalId },
                To = new LinkEndpoint { MapId = toMapId, PortalId = portal.TargetPortalId?.Trim() ?? "" }
            };
            _project.Links.Add(link);
            ReloadLinksList(selectLast: true);
            UpdateStatus();
        }
        else
        {
            var idx = _project.Links.FindIndex(l => ReferenceEquals(l, link));
            if (idx >= 0)
            {
                _tabs.SelectedIndex = 1;
                _linksList.SelectedIndex = idx;
            }
        }
    }

    private void SetPortalLinkTarget(string fromMapId, string fromPortalId, string toMapId, string toPortalId)
    {
        if (fromMapId.Length == 0 || fromPortalId.Length == 0)
            return;

        var fromMap = _project.Maps.FirstOrDefault(m => string.Equals(m.Id, fromMapId, StringComparison.Ordinal));
        if (fromMap == null)
            return;
        var portal = fromMap.Portals.FirstOrDefault(p => string.Equals(p.Id, fromPortalId, StringComparison.Ordinal));
        if (portal == null)
            return;

        var link = _project.Links.FirstOrDefault(l =>
            string.Equals(l.From.MapId, fromMapId, StringComparison.Ordinal) &&
            string.Equals(l.From.PortalId, fromPortalId, StringComparison.Ordinal));

        var oldTargetMapId = (link?.To?.MapId ?? portal.TargetMapId ?? "").Trim();
        var oldTargetPortalId = (link?.To?.PortalId ?? portal.TargetPortalId ?? "").Trim();
        var newTargetMapId = (toMapId ?? "").Trim();
        var newTargetPortalId = (toPortalId ?? "").Trim();

        if (string.Equals(oldTargetMapId, newTargetMapId, StringComparison.Ordinal)
            && string.Equals(oldTargetPortalId, newTargetPortalId, StringComparison.Ordinal))
            return;

        var root = _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
        var sceneResPath = fromMap.ScenePath.Length > 0 ? fromMap.ScenePath : fromMap.Id;
        var sceneAbsPath = sceneResPath.Length > 0 ? ToAbsoluteGodotPath(root, sceneResPath) : "";

        if (newTargetMapId.Length == 0)
        {
            portal.TargetMapId = "";
            portal.TargetPortalId = "";
            _project.Links.RemoveAll(l =>
                string.Equals(l.From.MapId, fromMapId, StringComparison.Ordinal) &&
                string.Equals(l.From.PortalId, fromPortalId, StringComparison.Ordinal));
            if (sceneAbsPath.Length > 0 && portal.NodePath.Length > 0)
                PatchPortalTarget(sceneAbsPath, portal.NodePath, "", "");

            _undo.Push(new PortalTargetUndoAction(
                $"传送门: {GetPortalFriendlyName(portal)}",
                _project,
                fromMap,
                sceneAbsPath,
                portal.NodePath,
                fromPortalId,
                oldTargetMapId,
                oldTargetPortalId,
                "",
                "",
                () =>
                {
                    ReloadLinksList(selectFirst: true);
                    UpdateStatus();
                }));

            ReloadLinksList(selectFirst: true);
            UpdateStatus();
            return;
        }

        portal.TargetMapId = newTargetMapId;
        portal.TargetPortalId = newTargetPortalId;

        if (link == null)
        {
            link = new MapLink
            {
                From = new LinkEndpoint { MapId = fromMapId, PortalId = fromPortalId },
                To = new LinkEndpoint { MapId = newTargetMapId, PortalId = newTargetPortalId }
            };
            _project.Links.Add(link);
        }
        else
        {
            link.To.MapId = newTargetMapId;
            link.To.PortalId = newTargetPortalId;
        }

        if (sceneAbsPath.Length > 0 && portal.NodePath.Length > 0)
            PatchPortalTarget(sceneAbsPath, portal.NodePath, portal.TargetMapId, portal.TargetPortalId);

        _undo.Push(new PortalTargetUndoAction(
            $"传送门: {GetPortalFriendlyName(portal)}",
            _project,
            fromMap,
            sceneAbsPath,
            portal.NodePath,
            fromPortalId,
            oldTargetMapId,
            oldTargetPortalId,
            newTargetMapId,
            newTargetPortalId,
            () =>
            {
                ReloadLinksList(selectFirst: false);
                UpdateStatus();
            }));

        ReloadLinksList(selectFirst: false);
        var idx = _project.Links.FindIndex(l => ReferenceEquals(l, link));
        if (idx >= 0)
        {
            _tabs.SelectedIndex = 1;
            _linksList.SelectedIndex = idx;
        }
        UpdateStatus();
    }

    private sealed class PortalTargetUndoAction : IUndoableAction
    {
        public string Name { get; }

        private readonly MapProject _project;
        private readonly MapDefinition _map;
        private readonly string _sceneAbsPath;
        private readonly string _nodePath;
        private readonly string _portalId;
        private readonly string _fromTargetMapId;
        private readonly string _fromTargetPortalId;
        private readonly string _toTargetMapId;
        private readonly string _toTargetPortalId;
        private readonly Action _afterApply;

        public PortalTargetUndoAction(
            string name,
            MapProject project,
            MapDefinition map,
            string sceneAbsPath,
            string nodePath,
            string portalId,
            string fromTargetMapId,
            string fromTargetPortalId,
            string toTargetMapId,
            string toTargetPortalId,
            Action afterApply)
        {
            Name = name;
            _project = project;
            _map = map;
            _sceneAbsPath = sceneAbsPath;
            _nodePath = nodePath;
            _portalId = portalId;
            _fromTargetMapId = fromTargetMapId ?? "";
            _fromTargetPortalId = fromTargetPortalId ?? "";
            _toTargetMapId = toTargetMapId ?? "";
            _toTargetPortalId = toTargetPortalId ?? "";
            _afterApply = afterApply;
        }

        public void Undo()
        {
            ApplyToModel(_fromTargetMapId, _fromTargetPortalId);
            if (_sceneAbsPath.Length > 0 && _nodePath.Length > 0)
                PatchPortalTarget(_sceneAbsPath, _nodePath, _fromTargetMapId, _fromTargetPortalId);
            _afterApply();
        }

        public void Redo()
        {
            ApplyToModel(_toTargetMapId, _toTargetPortalId);
            if (_sceneAbsPath.Length > 0 && _nodePath.Length > 0)
                PatchPortalTarget(_sceneAbsPath, _nodePath, _toTargetMapId, _toTargetPortalId);
            _afterApply();
        }

        private void ApplyToModel(string targetMapId, string targetPortalId)
        {
            var portal = _map.Portals.FirstOrDefault(p => string.Equals(p.Id, _portalId, StringComparison.Ordinal));
            if (portal == null)
                return;

            portal.TargetMapId = targetMapId ?? "";
            portal.TargetPortalId = targetPortalId ?? "";

            _project.Links.RemoveAll(l =>
                string.Equals(l.From.MapId, _map.Id, StringComparison.Ordinal) &&
                string.Equals(l.From.PortalId, _portalId, StringComparison.Ordinal));

            if (portal.TargetMapId.Length == 0)
                return;

            _project.Links.Add(new MapLink
            {
                From = new LinkEndpoint { MapId = _map.Id, PortalId = _portalId },
                To = new LinkEndpoint { MapId = portal.TargetMapId, PortalId = portal.TargetPortalId }
            });
        }
    }

    private string ResolvePortalTargetMapId(Portal portal)
    {
        if (_selectedMap == null)
            return "";
        var link = _project.Links.FirstOrDefault(l =>
            string.Equals(l.From.MapId, _selectedMap.Id, StringComparison.Ordinal)
            && string.Equals(l.From.PortalId, portal.Id, StringComparison.Ordinal));
        if (link != null)
            return link.To.MapId ?? "";
        return portal.TargetMapId ?? "";
    }

    private string ResolveMapDisplayName(string mapId)
    {
        if (mapId.Length == 0)
            return "";
        var m = _project.Maps.FirstOrDefault(x =>
            string.Equals(x.Id, mapId, StringComparison.Ordinal)
            || string.Equals(x.ScenePath, mapId, StringComparison.Ordinal));
        return m?.DisplayName ?? "";
    }

    private static string ResolveFallbackDisplayName(string? value)
    {
        value ??= "";
        value = value.Trim();
        if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileNameWithoutExtension(value);
        if (value.StartsWith("uid://", StringComparison.OrdinalIgnoreCase))
            return value;
        return value;
    }

    private static string GetPortalFriendlyName(Portal p)
    {
        var name = (p.Name ?? "").Trim();
        if (name.Length > 0 && !string.Equals(name, "Portal", StringComparison.OrdinalIgnoreCase))
            return name;

        var nodePath = (p.NodePath ?? "").Trim().Trim('/');
        if (nodePath.Length > 0)
        {
            var slash = nodePath.LastIndexOf('/');
            return slash >= 0 ? nodePath[(slash + 1)..] : nodePath;
        }

        return (p.Id ?? "").Trim();
    }

    private bool ConfirmAndApply(MapCanvas.CommitInfo info)
    {
        try
        {
            if (info.Kind == MapCanvas.CommitKind.NodePosition)
            {
                if (info.NodePosition == null)
                    return false;
                if (string.IsNullOrWhiteSpace(info.NodePosition.ScenePath) || string.IsNullOrWhiteSpace(info.NodePosition.NodePath))
                    return false;

                var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
                var sceneAbs = ToAbsoluteGodotPath(root, info.NodePosition.ScenePath);

                PatchNodePosition(sceneAbs, info.NodePosition.NodePath, info.NodePosition.ToX, info.NodePosition.ToY);

                var action = new NodePositionUndoAction(
                    $"移动: {info.Name}",
                    info.Map,
                    sceneAbs,
                    info.NodePosition.NodePath,
                    info.NodePosition.FromX,
                    info.NodePosition.FromY,
                    info.NodePosition.ToX,
                    info.NodePosition.ToY);
                _undo.Push(action);
                UpdateStatus();
                return true;
            }

            if (info.Kind == MapCanvas.CommitKind.TileCollisionPolygon)
            {
                if (info.Map.ScenePath.Length == 0)
                    return false;
                if (info.TileCollisions == null || info.TileCollisions.Count == 0)
                    return false;

                var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
                var sceneAbs = ToAbsoluteGodotPath(root, info.Map.ScenePath);
                var tilesetAbs = info.TileCollisions
                    .Select(e => ToAbsoluteGodotPath(root, e.TileSetResPath))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var beforeFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [sceneAbs] = File.ReadAllText(sceneAbs)
                };
                foreach (var t in tilesetAbs)
                    beforeFiles[t] = File.ReadAllText(t);

                var beforeAlt = CaptureAlternatives(info.Map, info.TileCollisions);

                ApplyTileCollisionsToGodot(info.Map, info.TileCollisions);

                var afterFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [sceneAbs] = File.ReadAllText(sceneAbs)
                };
                foreach (var t in tilesetAbs)
                    afterFiles[t] = File.ReadAllText(t);

                var afterAlt = CaptureAlternatives(info.Map, info.TileCollisions);

                var action = new CollisionUndoAction(
                    $"碰撞: {info.DisplayKind}",
                    info.Map,
                    beforeFiles,
                    afterFiles,
                    beforeAlt,
                    afterAlt,
                    () =>
                    {
                        foreach (var t in info.TileCollisions.Select(e => e.TileSetResPath).Distinct(StringComparer.OrdinalIgnoreCase))
                            _canvas.EvictTileSetCacheForResPath(t);
                        _canvas.ClearCollisionSelection();
                        _canvas.Invalidate();
                        UpdateStatus();
                    });

                _undo.Push(action);
                foreach (var t in info.TileCollisions.Select(e => e.TileSetResPath).Distinct(StringComparer.OrdinalIgnoreCase))
                    _canvas.EvictTileSetCacheForResPath(t);
                _canvas.ClearCollisionSelection();
                _canvas.Invalidate();
                UpdateStatus();
                return true;
            }

            if (info.Kind == MapCanvas.CommitKind.TileCollisionAlternative)
            {
                if (info.Map.ScenePath.Length == 0)
                    return false;
                if (info.TileCollisionAlternatives == null || info.TileCollisionAlternatives.Count == 0)
                    return false;

                var root = string.IsNullOrWhiteSpace(_godotRoot) ? GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory) : _godotRoot;
                var sceneAbs = ToAbsoluteGodotPath(root, info.Map.ScenePath);

                var beforeFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [sceneAbs] = File.ReadAllText(sceneAbs)
                };

                var beforeAlt = CaptureAlternatives(info.Map, info.TileCollisionAlternatives);

                ApplyTileCollisionAlternativesToGodot(info.Map, info.TileCollisionAlternatives);

                var afterFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [sceneAbs] = File.ReadAllText(sceneAbs)
                };

                var afterAlt = CaptureAlternatives(info.Map, info.TileCollisionAlternatives);

                var action = new CollisionUndoAction(
                    $"碰撞: {info.DisplayKind}",
                    info.Map,
                    beforeFiles,
                    afterFiles,
                    beforeAlt,
                    afterAlt,
                    () =>
                    {
                        _canvas.ClearCollisionSelection();
                        _canvas.Invalidate();
                        UpdateStatus();
                    });

                _undo.Push(action);
                _canvas.ClearCollisionSelection();
                _canvas.Invalidate();
                UpdateStatus();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "写入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static Dictionary<(string layerNodePath, int x, int y), int> CaptureAlternatives(MapDefinition map, IReadOnlyList<MapCanvas.TileCollisionCommit> edits)
    {
        var d = new Dictionary<(string layerNodePath, int x, int y), int>();
        foreach (var e in edits)
        {
            var layer = map.TileLayers.FirstOrDefault(l => string.Equals(l.NodePath, e.LayerNodePath, StringComparison.Ordinal));
            var cell = layer?.Cells.FirstOrDefault(c => c.X == e.CellX && c.Y == e.CellY);
            d[(e.LayerNodePath, e.CellX, e.CellY)] = cell?.Alternative ?? 0;
        }
        return d;
    }

    private static Dictionary<(string layerNodePath, int x, int y), int> CaptureAlternatives(MapDefinition map, IReadOnlyList<MapCanvas.TileCollisionAltCommit> edits)
    {
        var d = new Dictionary<(string layerNodePath, int x, int y), int>();
        foreach (var e in edits)
        {
            var layer = map.TileLayers.FirstOrDefault(l => string.Equals(l.NodePath, e.LayerNodePath, StringComparison.Ordinal));
            var cell = layer?.Cells.FirstOrDefault(c => c.X == e.CellX && c.Y == e.CellY);
            d[(e.LayerNodePath, e.CellX, e.CellY)] = cell?.Alternative ?? 0;
        }
        return d;
    }

    private void Undo()
    {
        if (!_undo.TryUndo())
            return;
        _canvas.ClearCollisionSelection();
        _canvas.Invalidate();
        _linksGraph.SetData(_project, _selectedMap, _linksList.SelectedItem as MapLink);
        _linksGraph.Invalidate();
        UpdateStatus();
    }

    private void Redo()
    {
        if (!_undo.TryRedo())
            return;
        _canvas.ClearCollisionSelection();
        _canvas.Invalidate();
        _linksGraph.SetData(_project, _selectedMap, _linksList.SelectedItem as MapLink);
        _linksGraph.Invalidate();
        UpdateStatus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z))
        {
            Undo();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Y))
        {
            Redo();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateMapListToolTip(Point location)
    {
        var idx = _mapsList.IndexFromPoint(location);
        if (idx < 0 || idx >= _mapsList.Items.Count)
        {
            _lastMapTipIndex = -1;
            return;
        }
        if (idx == _lastMapTipIndex)
            return;
        _lastMapTipIndex = idx;
        if (_mapsList.Items[idx] is not MapDefinition map)
            return;
        var text = string.IsNullOrWhiteSpace(map.ScenePath) ? map.DisplayName : $"{map.DisplayName}\n{map.ScenePath}";
        _toolTip.SetToolTip(_mapsList, text);
    }

    private void UpdateLinkListToolTip(Point location)
    {
        var idx = _linksList.IndexFromPoint(location);
        if (idx < 0 || idx >= _linksList.Items.Count)
        {
            _lastLinkTipIndex = -1;
            return;
        }
        if (idx == _lastLinkTipIndex)
            return;
        _lastLinkTipIndex = idx;
        if (_linksList.Items[idx] is not MapLink link)
            return;
        _toolTip.SetToolTip(_linksList, link.DisplayName);
    }

    private void ShowGridHelpToolTip(PropertyGrid grid)
    {
        var desc = grid.SelectedGridItem?.PropertyDescriptor?.Description;
        var value = grid.SelectedGridItem?.Value?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(value))
            return;
        var p = grid.PointToClient(Cursor.Position);
        var text = string.IsNullOrWhiteSpace(desc) ? value : (string.IsNullOrWhiteSpace(value) ? desc : $"{desc}\n{value}");
        _toolTip.Show(text, grid, p.X + 18, p.Y + 18, 5000);
        if (!string.IsNullOrWhiteSpace(value))
            _toolTip.SetToolTip(grid, value);
    }

    private void HookResourceBrowse(PropertyGrid grid)
    {
        grid.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;
            BrowseAndAssign(grid);
        };

        grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            if (BrowseAndAssign(grid))
                e.Handled = true;
        };
    }

    private static void AutoFitPropertyGridLabelWidth(PropertyGrid grid)
    {
        var selected = grid.SelectedObject;
        if (selected == null)
            return;

        var props = TypeDescriptor.GetProperties(selected);
        if (props == null || props.Count == 0)
            return;

        var max = 0;
        foreach (PropertyDescriptor p in props)
        {
            var name = p.DisplayName ?? p.Name ?? "";
            if (name.Length == 0)
                continue;
            var w = TextRenderer.MeasureText(name, grid.Font).Width;
            if (w > max)
                max = w;
        }

        max = Math.Clamp(max + 26, 120, Math.Max(120, (int)(grid.Width * 0.7)));
        TrySetPropertyGridLabelWidth(grid, max);
    }

    private static void TrySetPropertyGridLabelWidth(PropertyGrid grid, int width)
    {
        try
        {
            var gridViewField = typeof(PropertyGrid).GetField("gridView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var gridView = gridViewField?.GetValue(grid);
            if (gridView == null)
                return;

            var labelWidthProp = gridView.GetType().GetProperty("LabelWidth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (labelWidthProp != null && labelWidthProp.CanWrite)
            {
                labelWidthProp.SetValue(gridView, width);
                grid.Refresh();
                return;
            }

            var labelWidthField = gridView.GetType().GetField("labelWidth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (labelWidthField != null)
            {
                labelWidthField.SetValue(gridView, width);
                grid.Refresh();
            }
        }
        catch
        {
        }
    }

    private bool BrowseAndAssign(PropertyGrid grid)
    {
        if (_godotRoot == null || _godotRoot.Length == 0)
            return false;

        var item = grid.SelectedGridItem;
        var prop = item?.PropertyDescriptor;
        if (item == null || prop == null)
            return false;
        if (prop.IsReadOnly)
            return false;
        if (prop.PropertyType != typeof(string))
            return false;

        var name = prop.Name ?? "";
        var isDir = name.EndsWith("Dir", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Directory", StringComparison.OrdinalIgnoreCase);

        var currentValue = item.Value as string ?? "";
        var absCurrent = TryResolveToExistingPath(_godotRoot, currentValue);
        var initialDir = ResolveInitialDirectory(_godotRoot, absCurrent);

        string? chosenAbsPath;
        if (isDir)
        {
            using var dlg = new FolderBrowserDialog
            {
                InitialDirectory = initialDir,
                UseDescriptionForTitle = true,
                Description = "选择文件夹"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return false;
            chosenAbsPath = dlg.SelectedPath;
        }
        else
        {
            using var dlg = new OpenFileDialog
            {
                Title = "选择文件",
                Filter = BuildFilter(name, currentValue),
                InitialDirectory = initialDir,
                FileName = absCurrent ?? ""
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return false;
            chosenAbsPath = dlg.FileName;
        }

        if (string.IsNullOrWhiteSpace(chosenAbsPath))
            return false;

        var resPath = ConvertToResPathWithAutoImport(chosenAbsPath, initialDir, name);
        var component = item.Parent?.Value;
        if (component == null || component is string)
            component = grid.SelectedObject;
        if (component == null)
            return false;

        prop.SetValue(component, resPath);
        if (grid.SelectedObject is MapDefinition map)
        {
            if (string.Equals(prop.Name, nameof(MapDefinition.BackgroundTexturePath), StringComparison.Ordinal)
                || string.Equals(prop.Name, nameof(MapDefinition.TemplateTexturePath), StringComparison.Ordinal))
                TryWriteBackMapTexture(map);
        }
        grid.Refresh();
        UpdateStatus();
        return true;
    }

    private static string BuildFilter(string propName, string currentValue)
    {
        var ext = "";
        if (!string.IsNullOrWhiteSpace(currentValue))
            ext = Path.GetExtension(currentValue.Trim());

        if (string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase) || propName.Contains("Video", StringComparison.OrdinalIgnoreCase))
            return "视频 (*.mp4)|*.mp4|所有文件 (*.*)|*.*";

        if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase))
            return "图片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有文件 (*.*)|*.*";

        if (string.Equals(ext, ".tscn", StringComparison.OrdinalIgnoreCase))
            return "Godot 场景 (*.tscn)|*.tscn|所有文件 (*.*)|*.*";

        if (string.Equals(ext, ".tres", StringComparison.OrdinalIgnoreCase))
            return "Godot 资源 (*.tres)|*.tres|所有文件 (*.*)|*.*";

        if (propName.Contains("TileSet", StringComparison.OrdinalIgnoreCase))
            return "Godot TileSet (*.tres)|*.tres|所有文件 (*.*)|*.*";

        if (propName.Contains("Scene", StringComparison.OrdinalIgnoreCase))
            return "Godot 场景 (*.tscn)|*.tscn|所有文件 (*.*)|*.*";

        if (propName.Contains("Texture", StringComparison.OrdinalIgnoreCase))
            return "图片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有文件 (*.*)|*.*";

        return "所有文件 (*.*)|*.*";
    }

    private static string? TryResolveToExistingPath(string godotRoot, string value)
    {
        value = value.Trim();
        if (value.Length == 0)
            return null;
        if (value.StartsWith("res://", StringComparison.Ordinal))
        {
            var abs = ToAbsoluteGodotPath(godotRoot, value);
            if (File.Exists(abs) || Directory.Exists(abs))
                return abs;
            return null;
        }
        if (Path.IsPathRooted(value) && (File.Exists(value) || Directory.Exists(value)))
            return value;
        return null;
    }

    private static string ResolveInitialDirectory(string godotRoot, string? absCurrent)
    {
        if (!string.IsNullOrWhiteSpace(absCurrent))
        {
            if (Directory.Exists(absCurrent))
                return absCurrent;
            var dir = Path.GetDirectoryName(absCurrent);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                return dir;
        }
        return Directory.Exists(godotRoot) ? godotRoot : Environment.CurrentDirectory;
    }

    private static string TryMakeResPath(string godotRoot, string absPath)
    {
        if (!Path.IsPathRooted(absPath))
            return absPath;
        var rel = Path.GetRelativePath(godotRoot, absPath);
        if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            return "res://" + rel.Replace('\\', '/');
        return absPath;
    }

    private string ConvertToResPathWithAutoImport(string chosenAbsPath, string initialDir, string propName)
    {
        if (_godotRoot == null || _godotRoot.Length == 0)
            return chosenAbsPath;

        chosenAbsPath = chosenAbsPath.Trim();
        if (chosenAbsPath.Length == 0)
            return chosenAbsPath;

        if (!Path.IsPathRooted(chosenAbsPath))
            return chosenAbsPath;

        if (IsUnderRoot(_godotRoot, chosenAbsPath))
            return TryMakeResPath(_godotRoot, chosenAbsPath);

        var destBaseDir = GetPreferredImportDirectory(initialDir);
        destBaseDir = EnsureDirectoryExists(destBaseDir);

        if (File.Exists(chosenAbsPath))
        {
            var destAbs = ImportFileToDirectory(chosenAbsPath, destBaseDir);
            return TryMakeResPath(_godotRoot, destAbs);
        }

        if (Directory.Exists(chosenAbsPath))
        {
            var destAbs = ImportDirectoryToDirectory(chosenAbsPath, destBaseDir);
            return TryMakeResPath(_godotRoot, destAbs);
        }

        return chosenAbsPath;
    }

    private string GetPreferredImportDirectory(string initialDir)
    {
        if (_godotRoot == null || _godotRoot.Length == 0)
            return initialDir;

        if (_selectedMap != null && !string.IsNullOrWhiteSpace(_selectedMap.ScenePath) && _selectedMap.ScenePath.StartsWith("res://", StringComparison.Ordinal))
        {
            var rel = _selectedMap.ScenePath["res://".Length..].TrimStart('/').Replace('\\', '/');
            var sceneBaseName = Path.GetFileNameWithoutExtension(rel);
            if (sceneBaseName.Length == 0)
                sceneBaseName = _selectedMap.DisplayName.Length > 0 ? _selectedMap.DisplayName : "Map";

            var safeName = SanitizeFolderName(sceneBaseName);
            if (safeName.Length == 0)
                safeName = "Map";

            var idx = rel.IndexOf("/CoreEngine/Maps/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var prefix = rel[..(idx + 1)];
                var resRel = prefix + "CoreEngine/Resources/Maps/" + safeName;
                return Path.Combine(_godotRoot, resRel.Replace('/', Path.DirectorySeparatorChar));
            }

            var absScene = Path.Combine(_godotRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            var sceneDir = Path.GetDirectoryName(absScene);
            if (!string.IsNullOrWhiteSpace(sceneDir))
                return Path.Combine(sceneDir, "Resources", safeName);
        }

        if (!string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir) && IsUnderRoot(_godotRoot, initialDir))
            return initialDir;

        var sampleProjectAbs = Path.Combine(_godotRoot, "CoreEngine");
        if (Directory.Exists(sampleProjectAbs))
            return Path.Combine(_godotRoot, "CoreEngine", "Resources", "Imported");

        return _godotRoot;
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var pathFull = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureDirectoryExists(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            dir = Environment.CurrentDirectory;
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ImportFileToDirectory(string sourceAbsPath, string destDirAbs)
    {
        Directory.CreateDirectory(destDirAbs);
        var fileName = Path.GetFileName(sourceAbsPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "ImportedFile";
        var destAbs = GetUniquePath(Path.Combine(destDirAbs, fileName));
        File.Copy(sourceAbsPath, destAbs, overwrite: false);
        return destAbs;
    }

    private static string ImportDirectoryToDirectory(string sourceDirAbs, string destDirAbs)
    {
        Directory.CreateDirectory(destDirAbs);
        var name = new DirectoryInfo(sourceDirAbs).Name;
        if (string.IsNullOrWhiteSpace(name))
            name = "ImportedFolder";
        var destAbs = GetUniquePath(Path.Combine(destDirAbs, name));
        CopyDirectory(sourceDirAbs, destAbs);
        return destAbs;
    }

    private static string GetUniquePath(string desiredAbsPath)
    {
        if (!File.Exists(desiredAbsPath) && !Directory.Exists(desiredAbsPath))
            return desiredAbsPath;

        var dir = Path.GetDirectoryName(desiredAbsPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(desiredAbsPath);
        var ext = Path.GetExtension(desiredAbsPath);
        if (name.Length == 0)
            name = "Imported";

        for (var i = 2; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var name = new DirectoryInfo(sub).Name;
            var dest = Path.Combine(destDir, name);
            CopyDirectory(sub, dest);
        }
    }

    private static string SanitizeFolderName(string name)
    {
        if (name.Length == 0)
            return "";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        var w = 0;
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (invalid.Contains(c))
                continue;
            if (c == '/' || c == '\\')
                continue;
            chars[w++] = c;
        }
        return new string(chars, 0, w).Trim();
    }

    private void OnMapGridPropertyValueChanged(PropertyValueChangedEventArgs e)
    {
        if (_mapGrid.SelectedObject is not MapDefinition map)
            return;
        var descriptor = e.ChangedItem?.PropertyDescriptor;
        var propName = descriptor?.Name ?? "";
        if (string.Equals(propName, nameof(MapDefinition.ForegroundTexturePath), StringComparison.Ordinal))
        {
            var newPath = (map.ForegroundTexturePath ?? "").Trim();
            if (newPath.Length > 0 && !TryValidateForegroundTextureHasAlpha(newPath))
            {
                descriptor?.SetValue(map, e.OldValue);
                map.ForegroundTextureEnabled = false;
                _mapGrid.Refresh();
                return;
            }
        }
        if (string.Equals(propName, nameof(MapDefinition.ForegroundTextureEnabled), StringComparison.Ordinal) && map.ForegroundTextureEnabled)
        {
            var resPath = (map.ForegroundTexturePath ?? "").Trim();
            if (resPath.Length == 0 || !TryValidateForegroundTextureHasAlpha(resPath))
            {
                map.ForegroundTextureEnabled = false;
                _mapGrid.Refresh();
                return;
            }
        }

        var writeTexture = string.Equals(propName, nameof(MapDefinition.BackgroundTexturePath), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.TemplateTexturePath), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.BackgroundTextureEnabled), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.ForegroundTexturePath), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.ForegroundTextureEnabled), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.ForegroundTextureAnchor), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.ForegroundTextureUpscale), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.BackgroundTextureAnchor), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.BackgroundTextureUpscale), StringComparison.Ordinal);
        var writeBgLayer = string.Equals(propName, nameof(MapDefinition.BackgroundTileLayerVisible), StringComparison.Ordinal);
        var writeCollision = string.Equals(propName, nameof(MapDefinition.CollisionUsed), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.TileCollisionDataPath), StringComparison.Ordinal)
            || string.Equals(propName, nameof(MapDefinition.ForegroundTextureCollisionDataPath), StringComparison.Ordinal);

        if (!writeTexture && !writeBgLayer && !writeCollision)
            return;

        if (string.Equals(propName, nameof(MapDefinition.BackgroundTextureEnabled), StringComparison.Ordinal) && map.BackgroundTextureEnabled)
        {
            if (map.BackgroundTileLayerVisible)
            {
                map.BackgroundTileLayerVisible = false;
                _mapGrid.Refresh();
                writeBgLayer = true;
            }
        }

        if (string.Equals(propName, nameof(MapDefinition.BackgroundTileLayerVisible), StringComparison.Ordinal) && map.BackgroundTileLayerVisible)
        {
            if (map.BackgroundTextureEnabled)
            {
                map.BackgroundTextureEnabled = false;
                _mapGrid.Refresh();
                writeTexture = true;
            }
        }

        if (writeTexture)
        {
            TryWriteBackMapTexture(map);
            TryWriteBackTextureTransformMeta(map);
        }
        if (writeBgLayer)
            TryWriteBackBackgroundTileLayerVisibility(map);
        if (writeCollision)
        {
            TryWriteBackCollisionMeta(map);
            if (_viewModeCombo.SelectedIndex == 1 && _collisionEditModeCombo.SelectedIndex == 1)
                ReloadCollisionGridForSelectedMap();
        }

        if (writeBgLayer)
        {
            foreach (var l in map.TileLayers)
            {
                if (IsBackgroundTileLayerName(l.Name))
                    l.Visible = map.BackgroundTileLayerVisible;
            }
        }

        _canvas.Invalidate();
    }

    private void TryWriteBackTextureTransformMeta(MapDefinition map)
    {
        if (string.IsNullOrWhiteSpace(_godotRoot))
            return;
        if (string.IsNullOrWhiteSpace(map.ScenePath) || !map.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            return;

        var sceneAbsPath = ToAbsoluteGodotPath(_godotRoot, map.ScenePath);
        if (!File.Exists(sceneAbsPath))
            return;

        try
        {
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var mapNode = FindTscnNode(scene, "Map");
            if (mapNode == null)
                return;

            mapNode.RawProps["metadata/foreground_texture_anchor"] = $"\"{map.ForegroundTextureAnchor}\"";
            mapNode.RawProps["metadata/foreground_texture_upscale"] = $"\"{Math.Max(0.0001f, map.ForegroundTextureUpscale)}\"";
            mapNode.RawProps["metadata/background_texture_anchor"] = $"\"{map.BackgroundTextureAnchor}\"";
            mapNode.RawProps["metadata/background_texture_upscale"] = $"\"{Math.Max(0.0001f, map.BackgroundTextureUpscale)}\"";
            TscnWriter.PatchFile(sceneAbsPath, scene, ["metadata/foreground_texture_anchor", "metadata/foreground_texture_upscale", "metadata/background_texture_anchor", "metadata/background_texture_upscale"]);
        }
        catch
        {
        }
    }

    private bool TryValidateForegroundTextureHasAlpha(string resPath)
    {
        if (string.IsNullOrWhiteSpace(_godotRoot))
            return false;
        resPath = (resPath ?? "").Trim();
        if (resPath.Length == 0)
            return false;
        var abs = ToAbsoluteGodotPath(_godotRoot, resPath);
        if (!File.Exists(abs))
            return false;
        try
        {
            using var bmp = new Bitmap(abs);
            var pf = bmp.PixelFormat;
            var hasAlpha = (pf & System.Drawing.Imaging.PixelFormat.Alpha) != 0;
            if (hasAlpha)
                return true;
        }
        catch
        {
        }

        MessageBox.Show("前景纹理缺少透明通道（Alpha），已拒绝录入。请更换为带透明通道的图片。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void TryWriteBackMapTexture(MapDefinition map)
    {
        if (_godotRoot == null || _godotRoot.Length == 0)
            return;
        if (string.IsNullOrWhiteSpace(map.ScenePath) || !map.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            return;

        var sceneAbsPath = ToAbsoluteGodotPath(_godotRoot, map.ScenePath);
        if (!File.Exists(sceneAbsPath))
            return;

        try
        {
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var mapNode = FindTscnNode(scene, "Map");
            if (mapNode == null)
                return;

            var isTemplate = IsTemplateRoomMap(scene, mapNode);

            var keys = new List<string>();
            if (isTemplate)
            {
                keys.AddRange(ApplyTemplateMapTexturePatch(scene, mapNode, "background_texture", map.BackgroundTextureEnabled ? map.BackgroundTexturePath : "", "bg_"));
                keys.AddRange(ApplyTemplateMapTexturePatch(scene, mapNode, "foreground_texture", map.ForegroundTextureEnabled ? map.ForegroundTexturePath : "", "fg_"));
                keys.AddRange(ApplyTemplateMapTexturePatch(scene, mapNode, "template", map.TemplateTexturePath, "tpl_"));
            }
            else
            {
                if (map.BackgroundTextureEnabled && (string.IsNullOrWhiteSpace(map.BackgroundNodePath) || FindTscnNode(scene, map.BackgroundNodePath) == null))
                {
                    if (EnsureBackgroundLayerNodesInSceneFile(sceneAbsPath))
                    {
                        scene = TscnParser.ParseFile(sceneAbsPath);
                        mapNode = FindTscnNode(scene, "Map");
                        if (mapNode == null)
                            return;
                    }

                    map.BackgroundNodePath = "BackgroundLayer/BackgroundTexture";
                }

                if (map.ForegroundTextureEnabled && (string.IsNullOrWhiteSpace(map.ForegroundTextureNodePath) || FindTscnNode(scene, map.ForegroundTextureNodePath) == null))
                {
                    if (EnsureForegroundTextureWorldNodesInSceneFile(sceneAbsPath))
                    {
                        scene = TscnParser.ParseFile(sceneAbsPath);
                        mapNode = FindTscnNode(scene, "Map");
                        if (mapNode == null)
                            return;
                    }

                    map.ForegroundTextureNodePath = "ForegroundTextureLayer/ForegroundTexture";
                }

                if (!string.IsNullOrWhiteSpace(map.BackgroundNodePath))
                {
                    var bgNode = FindTscnNode(scene, map.BackgroundNodePath);
                    if (bgNode != null)
                        keys.AddRange(ApplyTextureNodeTexturePatch(scene, bgNode, map.BackgroundTextureEnabled ? map.BackgroundTexturePath : "", "bg_"));
                }

                if (!string.IsNullOrWhiteSpace(map.ForegroundTextureNodePath))
                {
                    var fgNode = FindTscnNode(scene, map.ForegroundTextureNodePath);
                    if (fgNode != null)
                        keys.AddRange(ApplyTextureNodeTexturePatch(scene, fgNode, map.ForegroundTextureEnabled ? map.ForegroundTexturePath : "", "fg_"));
                }
            }

            if (keys.Count > 0)
                TscnWriter.PatchFileWithExtResources(sceneAbsPath, scene, keys.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch
        {
        }
    }

    private void TryWriteBackBackgroundTileLayerVisibility(MapDefinition map)
    {
        if (_godotRoot == null || _godotRoot.Length == 0)
            return;
        if (string.IsNullOrWhiteSpace(map.ScenePath) || !map.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            return;

        var sceneAbsPath = ToAbsoluteGodotPath(_godotRoot, map.ScenePath);
        if (!File.Exists(sceneAbsPath))
            return;

        try
        {
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var keys = new List<string>();
            foreach (var node in scene.Nodes)
            {
                if (!string.Equals(node.Type, "TileMapLayer", StringComparison.Ordinal))
                    continue;
                if (!IsBackgroundTileLayerName(node.Name))
                    continue;
                node.RawProps["visible"] = map.BackgroundTileLayerVisible ? "true" : "false";
                keys.Add("visible");
            }
            if (keys.Count > 0)
                TscnWriter.PatchFile(sceneAbsPath, scene, keys.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch
        {
        }
    }

    private static bool IsBackgroundTileLayerName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0)
            return false;
        if (string.Equals(name, "Foreground", StringComparison.OrdinalIgnoreCase))
            return false;
        return name.Contains("back", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EnsureBackgroundLayerNodesInSceneFile(string sceneAbsPath)
    {
        var lines = File.ReadAllLines(sceneAbsPath).ToList();
        var hasBackground = lines.Any(l => l.TrimStart().StartsWith("[node name=\"BackgroundLayer\" type=\"CanvasLayer\"", StringComparison.Ordinal));
        var hasForeground = lines.Any(l => l.TrimStart().StartsWith("[node name=\"ForegroundTextureLayer\"", StringComparison.Ordinal));
        if (hasBackground && hasForeground)
            return false;

        var mapHeaderIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), "[node name=\"Map\" type=\"Node2D\"]", StringComparison.Ordinal))
            {
                mapHeaderIndex = i;
                break;
            }
        }
        if (mapHeaderIndex < 0)
            return false;

        var insertAt = mapHeaderIndex + 1;
        while (insertAt < lines.Count && !lines[insertAt].TrimStart().StartsWith("[", StringComparison.Ordinal))
            insertAt++;

        var block = new List<string>
        {
            "",
            "[node name=\"ForegroundTextureLayer\" type=\"Node2D\" parent=\".\"]",
            "z_index = -1",
            "",
            "[node name=\"ForegroundTexture\" type=\"Sprite2D\" parent=\"ForegroundTextureLayer\"]",
            "centered = false",
            "position = Vector2(0, 0)",
            "",
            "[node name=\"BackgroundLayer\" type=\"CanvasLayer\" parent=\".\"]",
            "layer = -100",
            "",
            "[node name=\"BackgroundTexture\" type=\"TextureRect\" parent=\"BackgroundLayer\"]",
            "anchors_preset = 15",
            "anchor_right = 1.0",
            "anchor_bottom = 1.0",
            "mouse_filter = 2",
            "expand_mode = 1",
            "stretch_mode = 6",
            ""
        };

        if (hasBackground && !hasForeground)
        {
            var fgOnly = block.TakeWhile(l => !l.StartsWith("[node name=\"BackgroundLayer\"", StringComparison.Ordinal)).ToList();
            lines.InsertRange(insertAt, fgOnly);
        }
        else if (!hasBackground && hasForeground)
        {
            var bgStart = block.FindIndex(l => l.StartsWith("[node name=\"BackgroundLayer\"", StringComparison.Ordinal));
            var bgOnly = bgStart >= 0 ? block.Skip(bgStart).ToList() : [];
            lines.InsertRange(insertAt, bgOnly);
        }
        else
        {
            lines.InsertRange(insertAt, block);
        }
        File.WriteAllLines(sceneAbsPath, lines);
        return true;
    }

    private static bool EnsureForegroundTextureWorldNodesInSceneFile(string sceneAbsPath)
    {
        var changed = false;
        if (!File.Exists(sceneAbsPath))
            return false;

        var lines = File.ReadAllLines(sceneAbsPath).ToList();

        var hasFgLayerNode2D = lines.Any(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith("[node name=\"ForegroundTextureLayer\" ", StringComparison.Ordinal)
                && t.Contains("type=\"Node2D\"", StringComparison.Ordinal);
        });

        if (hasFgLayerNode2D)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var t = lines[i].TrimStart();
                if (!t.StartsWith("[node name=\"ForegroundTextureLayer\" ", StringComparison.Ordinal)
                    || !t.Contains("type=\"CanvasLayer\"", StringComparison.Ordinal))
                    continue;

                var end = i + 1;
                while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                    end++;
                lines.RemoveRange(i, end - i);
                changed = true;

                if (i < lines.Count)
                {
                    var next = lines[i].TrimStart();
                    if (next.StartsWith("[node name=\"ForegroundTexture\" ", StringComparison.Ordinal)
                        && next.Contains("parent=\"ForegroundTextureLayer\"", StringComparison.Ordinal)
                        && next.Contains("type=\"TextureRect\"", StringComparison.Ordinal))
                    {
                        end = i + 1;
                        while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                            end++;
                        lines.RemoveRange(i, end - i);
                        changed = true;
                    }
                }

                i = Math.Max(-1, i - 1);
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (!t.StartsWith("[node name=\"ForegroundTexture\" ", StringComparison.Ordinal)
                || !t.Contains("parent=\"ForegroundTextureLayer\"", StringComparison.Ordinal)
                || !t.Contains("type=\"TextureRect\"", StringComparison.Ordinal))
                continue;

            var end = i + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                end++;
            lines.RemoveRange(i, end - i);
            changed = true;
            i = Math.Max(-1, i - 1);
        }

        var hasFgLayerAny = lines.Any(l => l.TrimStart().StartsWith("[node name=\"ForegroundTextureLayer\" ", StringComparison.Ordinal));
        if (!hasFgLayerAny)
        {
            if (EnsureBackgroundLayerNodesInSceneFile(sceneAbsPath))
            {
                lines = File.ReadAllLines(sceneAbsPath).ToList();
                changed = true;
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[node name=\"ForegroundTextureLayer\" ", StringComparison.Ordinal) && t.Contains("type=\"CanvasLayer\"", StringComparison.Ordinal))
            {
                lines[i] = lines[i].Replace("type=\"CanvasLayer\"", "type=\"Node2D\"", StringComparison.Ordinal);
                changed = true;

                var j = i + 1;
                var insertedZ = false;
                while (j < lines.Count && !lines[j].TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    var prop = lines[j].Trim();
                    if (prop.StartsWith("layer =", StringComparison.Ordinal))
                    {
                        lines.RemoveAt(j);
                        changed = true;
                        continue;
                    }
                    if (!insertedZ && prop.Length > 0)
                    {
                        lines.Insert(j, "z_index = -1");
                        insertedZ = true;
                        changed = true;
                        j++;
                    }
                    j++;
                }

                if (!insertedZ)
                {
                    lines.Insert(i + 1, "z_index = -1");
                    changed = true;
                }
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[node name=\"ForegroundTexture\" ", StringComparison.Ordinal)
                && t.Contains("parent=\"ForegroundTextureLayer\"", StringComparison.Ordinal)
                && t.Contains("type=\"TextureRect\"", StringComparison.Ordinal))
            {
                lines[i] = lines[i].Replace("type=\"TextureRect\"", "type=\"Sprite2D\"", StringComparison.Ordinal);
                changed = true;

                var j = i + 1;
                var hasCentered = false;
                var hasPos = false;
                while (j < lines.Count && !lines[j].TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    var prop = lines[j].Trim();
                    if (prop.StartsWith("anchors_preset", StringComparison.Ordinal)
                        || prop.StartsWith("anchor_left", StringComparison.Ordinal)
                        || prop.StartsWith("anchor_top", StringComparison.Ordinal)
                        || prop.StartsWith("anchor_right", StringComparison.Ordinal)
                        || prop.StartsWith("anchor_bottom", StringComparison.Ordinal)
                        || prop.StartsWith("offset_left", StringComparison.Ordinal)
                        || prop.StartsWith("offset_top", StringComparison.Ordinal)
                        || prop.StartsWith("offset_right", StringComparison.Ordinal)
                        || prop.StartsWith("offset_bottom", StringComparison.Ordinal)
                        || prop.StartsWith("grow_horizontal", StringComparison.Ordinal)
                        || prop.StartsWith("grow_vertical", StringComparison.Ordinal)
                        || prop.StartsWith("size", StringComparison.Ordinal)
                        || prop.StartsWith("expand_mode", StringComparison.Ordinal)
                        || prop.StartsWith("stretch_mode", StringComparison.Ordinal)
                        || prop.StartsWith("mouse_filter", StringComparison.Ordinal))
                    {
                        lines.RemoveAt(j);
                        changed = true;
                        continue;
                    }
                    if (prop.StartsWith("centered", StringComparison.Ordinal))
                        hasCentered = true;
                    if (prop.StartsWith("position", StringComparison.Ordinal))
                        hasPos = true;
                    j++;
                }

                var insertAt = i + 1;
                if (!hasCentered)
                {
                    lines.Insert(insertAt, "centered = false");
                    insertAt++;
                    changed = true;
                }
                if (!hasPos)
                {
                    lines.Insert(insertAt, "position = Vector2(0, 0)");
                    changed = true;
                }
            }
        }

        if (!changed)
            return false;

        File.WriteAllLines(sceneAbsPath, lines);
        return true;
    }

    private static IEnumerable<string> ApplyTemplateMapTexturePatch(TscnScene scene, TscnNode mapNode, string key, string resPath, string idPrefix)
    {
        resPath = (resPath ?? "").Trim();
        if (resPath.Length == 0)
        {
            mapNode.RawProps[key] = "null";
            return [key];
        }

        var id = EnsureExtResource(scene, "Texture2D", resPath, idPrefix);
        mapNode.RawProps[key] = $"ExtResource(\"{id}\")";
        return [key];
    }

    private static IEnumerable<string> ApplyTextureRectTexturePatch(TscnScene scene, TscnNode textureRect, string resPath, string idPrefix)
    {
        resPath = (resPath ?? "").Trim();
        if (resPath.Length == 0)
        {
            textureRect.RawProps["texture"] = "null";
            return ["texture"];
        }

        var id = EnsureExtResource(scene, "Texture2D", resPath, idPrefix);
        textureRect.RawProps["texture"] = $"ExtResource(\"{id}\")";
        return ["texture"];
    }

    private static IEnumerable<string> ApplySprite2DTexturePatch(TscnScene scene, TscnNode sprite, string resPath, string idPrefix)
    {
        resPath = (resPath ?? "").Trim();
        if (resPath.Length == 0)
        {
            sprite.RawProps["texture"] = "null";
            return ["texture"];
        }

        var id = EnsureExtResource(scene, "Texture2D", resPath, idPrefix);
        sprite.RawProps["texture"] = $"ExtResource(\"{id}\")";
        return ["texture"];
    }

    private static IEnumerable<string> ApplyTextureNodeTexturePatch(TscnScene scene, TscnNode node, string resPath, string idPrefix)
    {
        if (string.Equals(node.Type, "Sprite2D", StringComparison.Ordinal))
            return ApplySprite2DTexturePatch(scene, node, resPath, idPrefix);
        return ApplyTextureRectTexturePatch(scene, node, resPath, idPrefix);
    }

    private static bool IsTemplateRoomMap(TscnScene scene, TscnNode mapNode)
    {
        if (!mapNode.RawProps.TryGetValue("script", out var scriptRaw))
            return false;
        var id = TryExtractExtResourceId(scriptRaw);
        if (id == null || id.Length == 0)
            return false;
        var scriptPath = scene.FindExtResourcePathById(id) ?? "";
        return scriptPath.EndsWith("/TemplateRoomMap.gd", StringComparison.OrdinalIgnoreCase)
            || scriptPath.EndsWith("\\TemplateRoomMap.gd", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractExtResourceId(string raw)
    {
        raw = (raw ?? "").Trim();
        const string token = "ExtResource(\"";
        var start = raw.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += token.Length;
        var end = raw.IndexOf("\")", start, StringComparison.Ordinal);
        if (end < 0)
            return null;
        return raw[start..end];
    }

    private static string EnsureExtResource(TscnScene scene, string type, string resPath, string idPrefix)
    {
        var existing = scene.ExtResources.FirstOrDefault(r => string.Equals((r.Path ?? "").Trim(), resPath, StringComparison.Ordinal));
        if (existing != null && !string.IsNullOrWhiteSpace(existing.Id))
            return existing.Id;

        var baseId = BuildExtResourceId(resPath, idPrefix);
        var id = baseId;
        var i = 2;
        while (scene.ExtResources.Any(r => string.Equals(r.Id, id, StringComparison.Ordinal)))
        {
            id = $"{baseId}_{i}";
            i++;
        }

        scene.ExtResources.Add(new TscnExtResource
        {
            Type = type,
            Path = resPath,
            Id = id
        });
        return id;
    }

    private static string BuildExtResourceId(string resPath, string idPrefix)
    {
        var name = Path.GetFileNameWithoutExtension(resPath) ?? "";
        var sb = new System.Text.StringBuilder();
        var prevUnderscore = false;
        foreach (var ch in name)
        {
            var c = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                prevUnderscore = false;
            }
            else if (!prevUnderscore)
            {
                sb.Append('_');
                prevUnderscore = true;
            }
        }
        var core = sb.ToString().Trim('_');
        if (core.Length == 0)
            core = "res";
        return (idPrefix ?? "") + core;
    }

    private static TscnNode? FindTscnNode(TscnScene scene, string nodePath)
    {
        foreach (var n in scene.Nodes)
        {
            var p = ComputeTscnNodePath(n.Parent, n.Name);
            if (string.Equals(p, nodePath, StringComparison.Ordinal))
                return n;
        }
        return null;
    }

    private static string ComputeTscnNodePath(string? parent, string name)
    {
        parent = parent ?? "";
        if (parent.Length == 0 || parent == ".")
            return name;
        return parent + "/" + name;
    }

    private sealed class LinksGraphCanvas : Control
    {
        public Action<string>? MapSelected { get; set; }
        public Action<string, string>? PortalSelected { get; set; }
        public Action<string, string, string, string>? PortalTargetSetRequested { get; set; }
        public Action<MapLink>? LinkSelected { get; set; }
        public Action<string, Point>? ShowHoverHint { get; set; }
        public Action? HideHoverHint { get; set; }

        private MapProject? _project;
        private MapDefinition? _selectedMap;
        private MapLink? _selectedLink;

        private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
        private readonly List<GraphEdge> _edges = [];
        private object? _hoverItem;

        private float _zoom = 1f;
        private PointF _pan = new(0, 0);
        private bool _viewDirty;
        private bool _panning;
        private Point _panStart;
        private PointF _panOrigin;

        private const float NodePadX = 14f;
        private const float NodePadY = 10f;
        private const float NodeMinW = 140f;
        private const float NodeMinH = 44f;
        private const float ArrowSize = 9f;

        public LinksGraphCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9f);
            TabStop = true;
        }

        public void SetData(MapProject project, MapDefinition? selectedMap, MapLink? selectedLink)
        {
            _project = project;
            _selectedMap = selectedMap;
            _selectedLink = selectedLink;
            RebuildGraph();
        }

        private void RebuildGraph()
        {
            _nodes.Clear();
            _edges.Clear();

            if (_project == null)
            {
                Invalidate();
                return;
            }

            foreach (var map in _project.Maps)
            {
                if (map.Id.Length == 0)
                    continue;
                _nodes[map.Id] = new GraphNode(map.Id, map.DisplayName.Length > 0 ? map.DisplayName : map.Id, map);
            }

            foreach (var link in _project.Links)
            {
                var fromId = link.From?.MapId ?? "";
                var toId = link.To?.MapId ?? "";
                if (fromId.Length == 0 || toId.Length == 0)
                    continue;

                if (!_nodes.TryGetValue(fromId, out var from))
                    from = EnsureGhostNode(fromId);
                if (!_nodes.TryGetValue(toId, out var to))
                    to = EnsureGhostNode(toId);

                _edges.Add(new GraphEdge(link, from, to));
            }

            LayoutGraph();
            Invalidate();
        }

        private GraphNode EnsureGhostNode(string mapId)
        {
            var node = new GraphNode(mapId, mapId, null) { IsGhost = true };
            _nodes[mapId] = node;
            return node;
        }

        private void LayoutGraph()
        {
            if (_nodes.Count == 0)
                return;

            var nodes = _nodes.Values.OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            var nCount = nodes.Length;

            for (var i = 0; i < nCount; i++)
            {
                var a = (float)(i * (Math.PI * 2.0 / Math.Max(1, nCount)));
                var r = 240f + 22f * nCount;
                nodes[i].Pos = new PointF(MathF.Cos(a) * r, MathF.Sin(a) * r);
            }

            var idToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < nCount; i++)
                idToIndex[nodes[i].MapId] = i;

            var adj = new int[_edges.Count, 2];
            for (var i = 0; i < _edges.Count; i++)
            {
                adj[i, 0] = idToIndex[_edges[i].From.MapId];
                adj[i, 1] = idToIndex[_edges[i].To.MapId];
            }

            var area = 1200f * 800f;
            var k = MathF.Sqrt(area / Math.Max(1, nCount));
            var temp = 320f;
            var disp = new PointF[nCount];

            for (var iter = 0; iter < 120; iter++)
            {
                for (var i = 0; i < nCount; i++)
                    disp[i] = new PointF(0, 0);

                for (var v = 0; v < nCount; v++)
                {
                    for (var u = v + 1; u < nCount; u++)
                    {
                        var dx = nodes[v].Pos.X - nodes[u].Pos.X;
                        var dy = nodes[v].Pos.Y - nodes[u].Pos.Y;
                        var dist = MathF.Sqrt(dx * dx + dy * dy) + 0.01f;
                        var f = (k * k) / dist;
                        var fx = dx / dist * f;
                        var fy = dy / dist * f;
                        disp[v] = new PointF(disp[v].X + fx, disp[v].Y + fy);
                        disp[u] = new PointF(disp[u].X - fx, disp[u].Y - fy);
                    }
                }

                for (var e = 0; e < _edges.Count; e++)
                {
                    var v = adj[e, 0];
                    var u = adj[e, 1];
                    var dx = nodes[v].Pos.X - nodes[u].Pos.X;
                    var dy = nodes[v].Pos.Y - nodes[u].Pos.Y;
                    var dist = MathF.Sqrt(dx * dx + dy * dy) + 0.01f;
                    var f = (dist * dist) / k;
                    var fx = dx / dist * f;
                    var fy = dy / dist * f;
                    disp[v] = new PointF(disp[v].X - fx, disp[v].Y - fy);
                    disp[u] = new PointF(disp[u].X + fx, disp[u].Y + fy);
                }

                for (var v = 0; v < nCount; v++)
                {
                    var dx = disp[v].X;
                    var dy = disp[v].Y;
                    var dist = MathF.Sqrt(dx * dx + dy * dy) + 0.01f;
                    var lim = MathF.Min(dist, temp);
                    nodes[v].Pos = new PointF(
                        nodes[v].Pos.X + dx / dist * lim,
                        nodes[v].Pos.Y + dy / dist * lim
                    );
                }

                temp *= 0.96f;
            }

            if (!_viewDirty)
                FitToView();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (_project == null || _nodes.Count == 0)
            {
                using var br = new SolidBrush(ForeColor);
                g.DrawString("暂无连接数据：先从 Godot 重新加载，或在右侧列表添加连接。", Font, br, new PointF(12, 12));
                return;
            }

            foreach (var n in _nodes.Values)
                n.Bounds = MeasureNodeBounds(g, n);

            DrawEdges(g);
            DrawNodes(g);
        }

        private RectangleF MeasureNodeBounds(Graphics g, GraphNode n)
        {
            var label = n.DisplayName.Length > 0 ? n.DisplayName : n.MapId;
            var size = g.MeasureString(label, Font);
            var w = MathF.Max(NodeMinW, size.Width + NodePadX * 2f);
            var h = MathF.Max(NodeMinH, size.Height + NodePadY * 2f);
            return new RectangleF(n.Pos.X - w / 2f, n.Pos.Y - h / 2f, w, h);
        }

        private void DrawEdges(Graphics g)
        {
            foreach (var edge in _edges)
            {
                var from = edge.From;
                var to = edge.To;

                var p0 = new PointF(from.Pos.X, from.Pos.Y);
                var p1 = new PointF(to.Pos.X, to.Pos.Y);

                var selected = _selectedLink != null && ReferenceEquals(edge.Link, _selectedLink);
                var penColor = selected ? Color.FromArgb(230, 190, 110) : Color.FromArgb(140, 140, 140);
                if (from.IsGhost || to.IsGhost)
                    penColor = Color.FromArgb(170, 100, 100);

                using var pen = new Pen(penColor, selected ? 2.2f : 1.4f);
                var sp0 = WorldToScreen(p0);
                var sp1 = WorldToScreen(p1);
                g.DrawLine(pen, sp0, sp1);

                DrawArrow(g, penColor, sp0, sp1);
            }
        }

        private void DrawArrow(Graphics g, Color c, PointF a, PointF b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.01f)
                return;
            var ux = dx / len;
            var uy = dy / len;
            var px = -uy;
            var py = ux;

            var tip = b;
            var left = new PointF(b.X - ux * ArrowSize - px * (ArrowSize * 0.6f), b.Y - uy * ArrowSize - py * (ArrowSize * 0.6f));
            var right = new PointF(b.X - ux * ArrowSize + px * (ArrowSize * 0.6f), b.Y - uy * ArrowSize + py * (ArrowSize * 0.6f));

            using var br = new SolidBrush(c);
            g.FillPolygon(br, [tip, left, right]);
        }

        private void DrawNodes(Graphics g)
        {
            foreach (var node in _nodes.Values.OrderBy(n => n.IsGhost ? 1 : 0).ThenBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var label = node.DisplayName.Length > 0 ? node.DisplayName : node.MapId;
                var isSelected = _selectedMap != null && string.Equals(node.MapId, _selectedMap.Id, StringComparison.Ordinal);
                var isHover = ReferenceEquals(_hoverItem, node);

                var fill = node.IsGhost ? Color.FromArgb(60, 30, 30) : Color.FromArgb(40, 40, 40);
                var border = isSelected ? Color.FromArgb(240, 200, 120) : (isHover ? Color.FromArgb(200, 200, 200) : Color.FromArgb(90, 90, 90));
                var text = node.IsGhost ? Color.FromArgb(220, 170, 170) : ForeColor;

                var r = node.Bounds;
                var sr = WorldToScreen(r);
                using var br = new SolidBrush(fill);
                using var pen = new Pen(border, isSelected ? 2.4f : 1.6f);
                using var textBr = new SolidBrush(text);

                g.FillRectangle(br, sr);
                g.DrawRectangle(pen, sr.X, sr.Y, sr.Width, sr.Height);

                var textRect = new RectangleF(sr.X + NodePadX, sr.Y + NodePadY, sr.Width - NodePadX * 2f, sr.Height - NodePadY * 2f);
                g.DrawString(label, Font, textBr, textRect);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _panStart = e.Location;
                _panOrigin = _pan;
                Capture = true;
                return;
            }

            var hit = HitTest(e.Location);
            if (e.Button == MouseButtons.Right)
            {
                if (hit is GraphNode nodeHit && nodeHit.MapRef != null)
                    ShowNodePortalMenu(nodeHit, e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            if (hit is GraphNode node)
            {
                MapSelected?.Invoke(node.MapRef?.Id ?? node.MapId);
                return;
            }

            if (hit is GraphEdge edge)
            {
                LinkSelected?.Invoke(edge.Link);
                return;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_panning && e.Button == MouseButtons.Middle)
            {
                var dx = (e.Location.X - _panStart.X) / MathF.Max(0.01f, _zoom);
                var dy = (e.Location.Y - _panStart.Y) / MathF.Max(0.01f, _zoom);
                _pan = new PointF(_panOrigin.X + dx, _panOrigin.Y + dy);
                _viewDirty = true;
                Invalidate();
                return;
            }

            var hit = HitTest(e.Location);
            if (ReferenceEquals(hit, _hoverItem))
                return;

            ClearHover();
            if (hit == null)
                return;

            _hoverItem = hit;
            var hint = BuildHoverText(hit);
            if (hint.Length > 0)
                ShowHoverHint?.Invoke(hint, e.Location);
            Cursor = hit is GraphNode ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Middle)
            {
                _panning = false;
                Capture = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            var oldZoom = _zoom;
            var delta = e.Delta > 0 ? 1.1f : 1f / 1.1f;
            _zoom = Math.Clamp(_zoom * delta, 0.2f, 4.0f);
            if (MathF.Abs(_zoom - oldZoom) < 0.0001f)
                return;

            var before = ScreenToWorld(e.Location, oldZoom);
            var after = ScreenToWorld(e.Location, _zoom);
            _pan = new PointF(_pan.X + (after.X - before.X), _pan.Y + (after.Y - before.Y));
            _viewDirty = true;
            Invalidate();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            _viewDirty = false;
            FitToView();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ClearHover();
        }

        private void FitToView()
        {
            if (_nodes.Count == 0)
                return;

            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var n in _nodes.Values)
            {
                var left = n.Pos.X - NodeMinW / 2f;
                var top = n.Pos.Y - NodeMinH / 2f;
                var right = n.Pos.X + NodeMinW / 2f;
                var bottom = n.Pos.Y + NodeMinH / 2f;
                minX = MathF.Min(minX, left);
                minY = MathF.Min(minY, top);
                maxX = MathF.Max(maxX, right);
                maxY = MathF.Max(maxY, bottom);
            }

            if (float.IsInfinity(minX) || float.IsInfinity(minY))
                return;

            var w = MathF.Max(1f, maxX - minX);
            var h = MathF.Max(1f, maxY - minY);
            var margin = 42f;
            var sx = (ClientSize.Width - margin * 2f) / w;
            var sy = (ClientSize.Height - margin * 2f) / h;
            _zoom = Math.Clamp(MathF.Min(sx, sy), 0.2f, 2.2f);

            var cx = (minX + maxX) / 2f;
            var cy = (minY + maxY) / 2f;
            _pan = new PointF(-cx, -cy);
        }

        private object? HitTest(Point screenPt)
        {
            if (_project == null)
                return null;

            var world = ScreenToWorld(screenPt, _zoom);
            foreach (var n in _nodes.Values)
            {
                var rect = new RectangleF(n.Pos.X - NodeMinW / 2f, n.Pos.Y - NodeMinH / 2f, NodeMinW, NodeMinH);
                if (rect.Contains(world))
                    return n;
            }

            GraphEdge? best = null;
            var bestDist = 10f;
            for (var i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                var a = e.From.Pos;
                var b = e.To.Pos;
                var d = DistanceToSegment(world, a, b);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }

            return best;
        }

        private static float DistanceToSegment(PointF p, PointF a, PointF b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = p.X - a.X;
            var wy = p.Y - a.Y;

            var c1 = vx * wx + vy * wy;
            if (c1 <= 0f)
                return MathF.Sqrt(wx * wx + wy * wy);

            var c2 = vx * vx + vy * vy;
            if (c2 <= c1)
            {
                var dx = p.X - b.X;
                var dy = p.Y - b.Y;
                return MathF.Sqrt(dx * dx + dy * dy);
            }

            var t = c1 / c2;
            var proj = new PointF(a.X + t * vx, a.Y + t * vy);
            var px = p.X - proj.X;
            var py = p.Y - proj.Y;
            return MathF.Sqrt(px * px + py * py);
        }

        private void ShowNodePortalMenu(GraphNode node, Point location)
        {
            if (_project == null || node.MapRef == null)
                return;

            var map = node.MapRef;
            var title = map.DisplayName.Length > 0 ? map.DisplayName : map.Id;
            var menu = new ContextMenuStrip();
            menu.Closed += (_, _) => BeginInvoke(() => menu.Dispose());

            menu.Items.Add(new ToolStripMenuItem(title) { Enabled = false });

            var portals = map.Portals
                .OrderBy(p => GetPortalFriendlyName(p), StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.NodePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (portals.Length == 0)
            {
                menu.Items.Add(new ToolStripMenuItem("(无传送门)") { Enabled = false });
                menu.Show(this, location);
                return;
            }

            foreach (var portal in portals)
            {
                var fromMapId = map.Id;
                var fromPortalId = portal.Id;
                var link = _project.Links.FirstOrDefault(l =>
                    string.Equals(l.From.MapId, fromMapId, StringComparison.Ordinal) &&
                    string.Equals(l.From.PortalId, fromPortalId, StringComparison.Ordinal));

                var toMapId = link?.To?.MapId?.Trim() ?? portal.TargetMapId?.Trim() ?? "";
                var toPortalId = link?.To?.PortalId?.Trim() ?? portal.TargetPortalId?.Trim() ?? "";

                var toMapName = ResolveMapName(toMapId);
                if (toMapName.Length == 0)
                    toMapName = toMapId;
                var toPortalName = ResolvePortalName(toMapId, toPortalId);

                var summary = toMapId.Length == 0
                    ? "未连接"
                    : $"→ {toMapName}{(toPortalName.Length > 0 ? $" / {toPortalName}" : "")}";

                var portalTitle = GetPortalFriendlyName(portal);
                var portalItem = new ToolStripMenuItem($"{portalTitle}  {summary}");

                var open = new ToolStripMenuItem("在右侧打开");
                open.Click += (_, _) => PortalSelected?.Invoke(fromMapId, fromPortalId);
                portalItem.DropDownItems.Add(open);

                var setTarget = new ToolStripMenuItem("设置目标");
                foreach (var m in _project.Maps.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var mapName = m.DisplayName.Length > 0 ? m.DisplayName : m.Id;
                    var mapItem = new ToolStripMenuItem(mapName);

                    var mapOnly = new ToolStripMenuItem("(仅到地图)");
                    mapOnly.Click += (_, _) => PortalTargetSetRequested?.Invoke(fromMapId, fromPortalId, m.Id, "");
                    mapItem.DropDownItems.Add(mapOnly);

                    foreach (var tp in m.Portals.OrderBy(x => GetPortalFriendlyName(x), StringComparer.OrdinalIgnoreCase))
                    {
                        var tpName = GetPortalFriendlyName(tp);
                        var tpItem = new ToolStripMenuItem(tpName);
                        tpItem.Click += (_, _) => PortalTargetSetRequested?.Invoke(fromMapId, fromPortalId, m.Id, tp.Id);
                        mapItem.DropDownItems.Add(tpItem);
                    }

                    setTarget.DropDownItems.Add(mapItem);
                }
                portalItem.DropDownItems.Add(setTarget);

                var clear = new ToolStripMenuItem("清空连接");
                clear.Click += (_, _) => PortalTargetSetRequested?.Invoke(fromMapId, fromPortalId, "", "");
                portalItem.DropDownItems.Add(clear);

                menu.Items.Add(portalItem);
            }

            menu.Show(this, location);
        }

        private string ResolveMapName(string mapId)
        {
            if (mapId.Length == 0)
                return "";
            if (_project == null)
                return FallbackMapName(mapId);
            var m = _project.Maps.FirstOrDefault(x =>
                string.Equals(x.Id, mapId, StringComparison.Ordinal) ||
                string.Equals(x.ScenePath, mapId, StringComparison.Ordinal));
            var n = (m?.DisplayName ?? "").Trim();
            if (n.Length > 0)
                return n;
            return m == null ? FallbackMapName(mapId) : (m.Id.Length > 0 ? m.Id : FallbackMapName(mapId));
        }

        private static string FallbackMapName(string mapId)
        {
            var v = mapId.Trim();
            if (v.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(v);
            return v;
        }

        private string ResolvePortalName(string mapId, string portalId)
        {
            if (_project == null || mapId.Length == 0 || portalId.Length == 0)
                return "";
            var m = _project.Maps.FirstOrDefault(x =>
                string.Equals(x.Id, mapId, StringComparison.Ordinal) ||
                string.Equals(x.ScenePath, mapId, StringComparison.Ordinal));
            if (m == null)
                return "";
            var p = m.Portals.FirstOrDefault(x => string.Equals(x.Id, portalId, StringComparison.Ordinal));
            return p == null ? "" : GetPortalFriendlyName(p);
        }

        private static string GetPortalFriendlyName(Portal p)
        {
            var name = (p.Name ?? "").Trim();
            if (name.Length > 0 && !string.Equals(name, "Portal", StringComparison.OrdinalIgnoreCase))
                return name;

            var nodePath = (p.NodePath ?? "").Trim();
            if (nodePath.Length > 0)
            {
                var seg = nodePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
                if (seg.Length > 0 && !string.Equals(seg, "Portal", StringComparison.OrdinalIgnoreCase))
                    return seg;
                if (seg.Length > 0)
                    return seg;
            }

            return name.Length > 0 ? name : "Portal";
        }

        private string BuildHoverText(object hit)
        {
            if (hit is GraphNode node)
            {
                var inCount = 0;
                var outCount = 0;
                foreach (var e in _edges)
                {
                    if (string.Equals(e.From.MapId, node.MapId, StringComparison.Ordinal))
                        outCount++;
                    if (string.Equals(e.To.MapId, node.MapId, StringComparison.Ordinal))
                        inCount++;
                }

                var label = node.DisplayName.Length > 0 ? node.DisplayName : node.MapId;
                return $"地图: {label}\n入:{inCount} 出:{outCount}";
            }

            if (hit is GraphEdge edge)
            {
                var from = edge.From.DisplayName.Length > 0 ? edge.From.DisplayName : edge.From.MapId;
                var to = edge.To.DisplayName.Length > 0 ? edge.To.DisplayName : edge.To.MapId;
                var fromPortal = edge.Link.From?.PortalId ?? "";
                var toPortal = edge.Link.To?.PortalId ?? "";
                var fromPortalName = fromPortal.Length == 0 ? "" : (ResolvePortalName(edge.From.MapId, fromPortal).Trim());
                if (fromPortalName.Length == 0)
                    fromPortalName = fromPortal;
                var toPortalName = toPortal.Length == 0 ? "" : (ResolvePortalName(edge.To.MapId, toPortal).Trim());
                if (toPortalName.Length == 0)
                    toPortalName = toPortal;
                var tail = (fromPortalName.Length > 0 || toPortalName.Length > 0) ? $"\n{fromPortalName} → {toPortalName}" : "";
                return $"{from} → {to}{tail}";
            }

            return "";
        }

        private void ClearHover()
        {
            _hoverItem = null;
            HideHoverHint?.Invoke();
            Cursor = Cursors.Default;
            Invalidate();
        }

        private PointF WorldToScreen(PointF world)
        {
            var cx = ClientSize.Width / 2f;
            var cy = ClientSize.Height / 2f;
            return new PointF(cx + (world.X + _pan.X) * _zoom, cy + (world.Y + _pan.Y) * _zoom);
        }

        private RectangleF WorldToScreen(RectangleF world)
        {
            var a = WorldToScreen(new PointF(world.Left, world.Top));
            var b = WorldToScreen(new PointF(world.Right, world.Bottom));
            var x = MathF.Min(a.X, b.X);
            var y = MathF.Min(a.Y, b.Y);
            var w = MathF.Abs(b.X - a.X);
            var h = MathF.Abs(b.Y - a.Y);
            return new RectangleF(x, y, w, h);
        }

        private PointF ScreenToWorld(Point screen, float zoom)
        {
            var cx = ClientSize.Width / 2f;
            var cy = ClientSize.Height / 2f;
            return new PointF((screen.X - cx) / MathF.Max(0.01f, zoom) - _pan.X, (screen.Y - cy) / MathF.Max(0.01f, zoom) - _pan.Y);
        }

        private sealed class GraphNode(string mapId, string displayName, MapDefinition? mapRef)
        {
            public string MapId { get; } = mapId;
            public string DisplayName { get; } = displayName;
            public MapDefinition? MapRef { get; } = mapRef;
            public bool IsGhost { get; set; }
            public PointF Pos { get; set; }
            public RectangleF Bounds { get; set; }
        }

        private sealed class GraphEdge(MapLink link, GraphNode from, GraphNode to)
        {
            public MapLink Link { get; } = link;
            public GraphNode From { get; } = from;
            public GraphNode To { get; } = to;
        }
    }

    private sealed class MapCanvas : Control
    {
        public enum CanvasViewMode
        {
            Map = 0,
            TileSetCollision = 1,
            LayoutCollision = 2
        }

        public enum CollisionToolMode
        {
            Select = 0,
            Vertex = 1,
            Move = 2,
            Rotate = 3,
            Scale = 4
        }

        public enum CommitKind
        {
            NodePosition = 0,
            TileCollisionPolygon = 1,
            TileCollisionAlternative = 2
        }

        public sealed record TileCollisionCommit(
            string TileSetResPath,
            string LayerNodePath,
            int SourceId,
            int AtlasX,
            int AtlasY,
            int CellX,
            int CellY,
            bool OneWay,
            IReadOnlyList<GodotVector2> FromPoints,
            IReadOnlyList<GodotVector2> ToPoints);

        public sealed record TileCollisionAltCommit(string LayerNodePath, int CellX, int CellY, int FromAlternative, int ToAlternative);

        public sealed record NodePositionCommit(string ScenePath, string NodePath, float FromX, float FromY, float ToX, float ToY);

        public sealed record CommitInfo(
            MapDefinition Map,
            CommitKind Kind,
            string DisplayKind,
            string Name,
            float FromX,
            float FromY,
            float ToX,
            float ToY,
            IReadOnlyList<TileCollisionCommit>? TileCollisions,
            NodePositionCommit? NodePosition,
            IReadOnlyList<TileCollisionAltCommit>? TileCollisionAlternatives = null);

        public Func<CommitInfo, bool>? CommitRequested { get; set; }
        public Action<CollisionToolMode>? ToolModeChanged { get; set; }
        public Action<string, Point>? ShowHoverHint { get; set; }
        public Action? HideHoverHint { get; set; }
        public Func<Portal, string>? GetPortalHoverText { get; set; }
        public Func<PlacedEntity, string>? GetEntityHoverText { get; set; }
        public Action<Portal>? PortalRightClick { get; set; }
        public Action<float, float>? AddPortalRequested { get; set; }

        private const int TileSize = 32;
        private const int Pad = 16;
        private const int MarkerRadius = 8;
        private const int CollisionHandleRadius = 6;
        private const int GizmoHandleSize = 10;
        private const float RotateHandleOffset = 26f;
        private const float RotateHandleRadius = 10f;
        private const float VertexSnapDistance = 6f;

        private MapDefinition? _map;
        private string? _godotRoot;
        private CanvasViewMode _viewMode = CanvasViewMode.Map;
        private CollisionToolMode _toolMode = CollisionToolMode.Select;
        private object? _dragItem;
        private float _dragFromX;
        private float _dragFromY;
        private bool _dragMoved;
        private object? _hoverItem;

        private readonly Dictionary<string, Image> _imageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GodotTileSet> _tileSetCache = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<TileCollisionSelection> _tileCollisionSelections = [];
        private TileCollisionDrag? _tileCollisionDrag;
        private TileCollisionTransformDrag? _tileCollisionTransformDrag;
        private TileCollisionMarquee? _tileCollisionMarquee;

        private enum PendingTileCollisionAction
        {
            None = 0,
            AddSquare = 1,
            Remove = 2
        }

        private PendingTileCollisionAction _pendingTileCollisionAction;

        private CollisionLayoutData? _layoutCollision;
        private bool _layoutCollisionDragging;
        private bool _layoutCollisionDragValue;
        private int _layoutCollisionLastIndex = -1;
        private int _layoutPolySelectedIndex = -1;
        private bool _layoutPolyDragging;
        private LayoutCollisionTransformDrag? _layoutCollisionTransformDrag;
        private CollisionLayoutData? _layoutUndoBefore;
        private string _layoutUndoName = "";
        private LayoutPolyDragKind _layoutPolyDragKind;
        private int _layoutPolyDragPolygonIndex = -1;
        private int _layoutPolyDragVertexIndex = -1;
        private (float x, float y) _layoutPolyDragStartWorld;
        private List<GodotVector2>? _layoutPolyDragStartPoints;
        private GodotVector2 _layoutPolyDragPivot;
        private GodotVector2 _layoutPolyDragStartPivotToMouse;

        private enum LayoutPolyDragKind
        {
            None = 0,
            Vertex = 1,
            Move = 2,
            Rotate = 3,
            Scale = 4
        }

        public Action? LayoutCollisionChanged { get; set; }
        public Action<string, CollisionLayoutData, CollisionLayoutData>? LayoutCollisionCommitted { get; set; }

        public MapCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9f);
            TabStop = true;
        }

        public CollisionToolMode GetToolMode() => _toolMode;

        public void BeginAddSquareTileCollision()
        {
            if (_viewMode != CanvasViewMode.TileSetCollision)
                return;
            _pendingTileCollisionAction = PendingTileCollisionAction.AddSquare;
            Cursor = Cursors.Cross;
            Invalidate();
        }

        public void BeginRemoveTileCollision()
        {
            if (_viewMode != CanvasViewMode.TileSetCollision)
                return;
            _pendingTileCollisionAction = PendingTileCollisionAction.Remove;
            Cursor = Cursors.Cross;
            Invalidate();
        }

        public void CancelPendingTileCollisionAction()
        {
            _pendingTileCollisionAction = PendingTileCollisionAction.None;
            Cursor = Cursors.Default;
            Invalidate();
        }

        public void SetMap(MapDefinition? map, string? godotRoot)
        {
            _map = map;
            _godotRoot = godotRoot;
            _dragItem = null;
            _dragMoved = false;
            _pendingTileCollisionAction = PendingTileCollisionAction.None;
            Cursor = Cursors.Default;
            _tileCollisionSelections.Clear();
            _tileCollisionDrag = null;
            _tileCollisionTransformDrag = null;
            _tileCollisionMarquee = null;
            Invalidate();
        }

        public void SetViewMode(CanvasViewMode mode)
        {
            if (_viewMode == mode)
                return;
            _viewMode = mode;
            _dragItem = null;
            _dragMoved = false;
            _pendingTileCollisionAction = PendingTileCollisionAction.None;
            Cursor = Cursors.Default;
            _tileCollisionSelections.Clear();
            _tileCollisionDrag = null;
            _tileCollisionTransformDrag = null;
            _tileCollisionMarquee = null;
            _layoutCollisionDragging = false;
            _layoutCollisionLastIndex = -1;
            _layoutUndoBefore = null;
            _layoutUndoName = "";
            if (_viewMode == CanvasViewMode.TileSetCollision)
                SetToolMode(CollisionToolMode.Select);
            Invalidate();
        }

        public void SetLayoutCollision(CollisionLayoutData? layout)
        {
            _layoutCollision = layout;
            _layoutCollisionDragging = false;
            _layoutCollisionLastIndex = -1;
            _layoutPolySelectedIndex = (layout != null && layout.Polygons.Count > 0) ? 0 : -1;
            _layoutPolyDragging = false;
            _layoutCollisionTransformDrag = null;
            _layoutUndoBefore = null;
            _layoutUndoName = "";
            _layoutPolyDragPolygonIndex = -1;
            _layoutPolyDragVertexIndex = -1;
            Invalidate();
        }

        public void SetToolMode(CollisionToolMode mode)
        {
            if (_toolMode == mode)
                return;
            _toolMode = mode;
            _tileCollisionDrag = null;
            _tileCollisionTransformDrag = null;
            _tileCollisionMarquee = null;
            ToolModeChanged?.Invoke(mode);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var img in _imageCache.Values)
                    img.Dispose();
                _imageCache.Clear();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (_map == null)
            {
                using var br = new SolidBrush(ForeColor);
                g.DrawString("从“文件 → 从 Godot 重新加载...”载入后，选择左侧地图进行编辑。", Font, br, new PointF(Pad, Pad));
                return;
            }

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            DrawRoom(g, roomW, roomH, scale, offset);
            var hasAnyTileData = _map.TileLayers.Any(l => l.Visible && l.TileSetPath.Length > 0 && l.Cells.Count > 0);
            var hasAnyTex = (_map.BackgroundTextureEnabled && (_map.BackgroundTexturePath.Length > 0 || _map.TemplateTexturePath.Length > 0))
                || (_map.ForegroundTextureEnabled && _map.ForegroundTexturePath.Length > 0);
            if (!hasAnyTileData && !hasAnyTex)
            {
                using var br = new SolidBrush(ForeColor);
                g.DrawString("提示：该场景没有可预览的 TileMap 数据（TileMapLayer 为空）且未设置背景/模板纹理。\n如果房间地形由脚本在运行时生成（例如 TemplateRoomMap），编辑器只能显示出入口/实体标记。", Font, br, new PointF(offset.X + Pad, offset.Y + Pad));
            }
            DrawSceneLikePreview(g, roomW, roomH, scale, offset);
            if (_viewMode == CanvasViewMode.TileSetCollision)
                DrawTileCollisionOverlays(g, roomW, roomH, scale, offset);
            else if (_viewMode == CanvasViewMode.LayoutCollision)
                DrawLayoutCollisionOverlays(g, roomW, roomH, scale, offset);
            DrawMarkers(g, scale, offset);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_viewMode != CanvasViewMode.TileSetCollision)
                return;

            if (e.KeyCode == Keys.Q)
            {
                SetToolMode(CollisionToolMode.Vertex);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.S)
            {
                SetToolMode(CollisionToolMode.Select);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.W)
            {
                SetToolMode(CollisionToolMode.Move);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.E)
            {
                SetToolMode(CollisionToolMode.Rotate);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.R)
            {
                SetToolMode(CollisionToolMode.Scale);
                e.Handled = true;
            }
        }

        private void DrawSceneLikePreview(Graphics g, int roomW, int roomH, float scale, PointF offset)
        {
            if (_map == null)
                return;
            if (string.IsNullOrWhiteSpace(_godotRoot))
                return;

            var clip = new RectangleF(offset.X, offset.Y, roomW * scale, roomH * scale);
            var prevClip = g.Clip;
            g.SetClip(clip);

            DrawBackgroundTexture(g, roomW, roomH, scale, offset);
            DrawForegroundTexture(g, roomW, roomH, scale, offset);
            DrawTileLayers(g, scale, offset);

            g.SetClip(prevClip, System.Drawing.Drawing2D.CombineMode.Replace);
        }

        private void DrawBackgroundTexture(Graphics g, int roomW, int roomH, float scale, PointF offset)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return;
            if (!_map.BackgroundTextureEnabled)
                return;

            var resPath = _map.BackgroundTexturePath.Length > 0 ? _map.BackgroundTexturePath : _map.TemplateTexturePath;
            if (resPath.Length == 0)
                return;

            var img = TryLoadImage(resPath);
            if (img == null)
                return;

            var upscale = Math.Max(0.0001f, _map.BackgroundTextureUpscale);
            var texW = img.Width * upscale;
            var texH = img.Height * upscale;
            var origin = ComputeAnchoredOrigin(roomW, roomH, texW, texH, _map.BackgroundTextureAnchor);
            var dest = new RectangleF(offset.X + origin.X * scale, offset.Y + origin.Y * scale, texW * scale, texH * scale);
            g.DrawImage(img, dest);
        }

        private void DrawForegroundTexture(Graphics g, int roomW, int roomH, float scale, PointF offset)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return;
            if (!_map.ForegroundTextureEnabled)
                return;
            if (_map.ForegroundTexturePath.Length == 0)
                return;

            var img = TryLoadImage(_map.ForegroundTexturePath);
            if (img == null)
                return;

            var upscale = Math.Max(0.0001f, _map.ForegroundTextureUpscale);
            var texW = img.Width * upscale;
            var texH = img.Height * upscale;
            var origin = ComputeAnchoredOrigin(roomW, roomH, texW, texH, _map.ForegroundTextureAnchor);
            var dest = new RectangleF(offset.X + origin.X * scale, offset.Y + origin.Y * scale, texW * scale, texH * scale);
            g.DrawImage(img, dest);
        }

        private static PointF ComputeAnchoredOrigin(float roomW, float roomH, float texW, float texH, TextureAnchor anchor)
        {
            return anchor switch
            {
                TextureAnchor.TopLeft => new PointF(0, 0),
                TextureAnchor.TopRight => new PointF(roomW - texW, 0),
                TextureAnchor.BottomLeft => new PointF(0, roomH - texH),
                TextureAnchor.BottomRight => new PointF(roomW - texW, roomH - texH),
                TextureAnchor.Center => new PointF((roomW - texW) / 2f, (roomH - texH) / 2f),
                _ => new PointF(0, 0)
            };
        }

        private void DrawTileLayers(Graphics g, float scale, PointF offset)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return;

            foreach (var layer in _map.TileLayers.OrderBy(l => l.ZIndex).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                if (layer.TileSetPath.Length == 0)
                    continue;

                var tileset = TryLoadTileSet(layer.TileSetPath);
                if (tileset == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    if (!tileset.Sources.TryGetValue(cell.SourceId, out var src))
                        continue;

                    var atlasImg = TryLoadImage(src.TextureResPath);
                    if (atlasImg == null)
                        continue;

                    var srcRect = new Rectangle(cell.AtlasX * src.RegionWidth, cell.AtlasY * src.RegionHeight, src.RegionWidth, src.RegionHeight);
                    var worldX = cell.X * TileSize;
                    var worldY = cell.Y * TileSize;
                    var destRect = new RectangleF(offset.X + worldX * scale, offset.Y + worldY * scale, TileSize * scale, TileSize * scale);

                    g.DrawImage(atlasImg, destRect, srcRect, GraphicsUnit.Pixel);
                }
            }
        }

        private Image? TryLoadImage(string resPath)
        {
            if (string.IsNullOrWhiteSpace(_godotRoot))
                return null;

            var abs = ToAbsoluteGodotPath(_godotRoot, resPath);
            if (_imageCache.TryGetValue(abs, out var cached))
                return cached;
            if (!File.Exists(abs))
                return null;

            var img = Image.FromFile(abs);
            _imageCache[abs] = img;
            return img;
        }

        public void EvictImageCacheUnderAbsoluteDir(string absDir)
        {
            if (string.IsNullOrWhiteSpace(absDir))
                return;
            absDir = absDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            var keys = _imageCache.Keys
                .Where(k => k.StartsWith(absDir, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var k in keys)
            {
                if (_imageCache.TryGetValue(k, out var img))
                {
                    try { img.Dispose(); } catch { }
                }
                _imageCache.Remove(k);
            }
        }

        private Image? TryLoadPortalPreviewImage(Portal portal)
        {
            if (portal == null)
                return null;
            if (string.IsNullOrWhiteSpace(_godotRoot))
                return null;

            var dirRes = (portal.AnimationFramesDir ?? "").Trim();
            if (dirRes.Length == 0 || !dirRes.StartsWith("res://", StringComparison.Ordinal))
                return null;

            var absDir = ToAbsoluteGodotPath(_godotRoot, dirRes);
            if (!Directory.Exists(absDir))
                return null;

            var abs = Path.Combine(absDir, "frame_000_Alpha.png");
            if (!File.Exists(abs))
                abs = Path.Combine(absDir, "frame_000.png");
            if (!File.Exists(abs))
            {
                var any = Directory.EnumerateFiles(absDir, "*_Alpha.png", SearchOption.TopDirectoryOnly)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                    ?? Directory.EnumerateFiles(absDir, "*.png", SearchOption.TopDirectoryOnly)
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(any))
                    return null;
                abs = any;
            }

            if (_imageCache.TryGetValue(abs, out var cached))
                return cached;
            if (!File.Exists(abs))
                return null;

            try
            {
                var bytes = File.ReadAllBytes(abs);
                using var ms = new MemoryStream(bytes);
                using var src = Image.FromStream(ms);
                var img = new Bitmap(src);
                _imageCache[abs] = img;
                return img;
            }
            catch
            {
                return null;
            }
        }

        private GodotTileSet? TryLoadTileSet(string resPath)
        {
            if (string.IsNullOrWhiteSpace(_godotRoot))
                return null;

            var abs = ToAbsoluteGodotPath(_godotRoot, resPath);
            if (_tileSetCache.TryGetValue(abs, out var cached))
                return cached;
            if (!File.Exists(abs))
                return null;

            var ts = GodotTileSetLoader.Load(abs);
            _tileSetCache[abs] = ts;
            return ts;
        }

        private void EvictTileSetCache(string resPath)
        {
            if (string.IsNullOrWhiteSpace(_godotRoot))
                return;
            var abs = ToAbsoluteGodotPath(_godotRoot, resPath);
            _tileSetCache.Remove(abs);
        }

        public void EvictTileSetCacheForResPath(string resPath)
        {
            EvictTileSetCache(resPath);
        }

        public void ClearCollisionSelection()
        {
            _tileCollisionSelections.Clear();
            _tileCollisionDrag = null;
            _tileCollisionTransformDrag = null;
            _tileCollisionMarquee = null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (_map == null)
                return;

            if (_viewMode == CanvasViewMode.Map && e.Button == MouseButtons.Right)
            {
                var loc = e.Location;
                var hitRight = HitTest(loc);
                if (hitRight?.item is Portal p)
                {
                    PortalRightClick?.Invoke(p);
                    return;
                }

                var menu = new ContextMenuStrip();
                menu.Closed += (_, _) => BeginInvoke(() => menu.Dispose());
                var addPortal = new ToolStripMenuItem("新增传送门...");
                addPortal.Enabled = AddPortalRequested != null;
                addPortal.Click += (_, _) =>
                {
                    var w = ScreenToWorld(loc);
                    AddPortalRequested?.Invoke(w.x, w.y);
                };
                menu.Items.Add(addPortal);
                menu.Show(this, loc);
                return;
            }

            ClearHover();
            if (_viewMode == CanvasViewMode.LayoutCollision)
            {
                if (_layoutCollision == null || _layoutCollision.RoomWidth <= 0 || _layoutCollision.RoomHeight <= 0)
                    return;

                if (_layoutCollision.Polygons.Count > 0)
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        if (_layoutCollisionTransformDrag == null && _toolMode != CollisionToolMode.Vertex && _layoutPolySelectedIndex >= 0 && _layoutPolySelectedIndex < _layoutCollision.Polygons.Count)
                        {
                            var gizmoHit = HitTestLayoutCollisionGizmo(e.Location);
                            if (gizmoHit != null)
                            {
                                _layoutCollisionTransformDrag = StartLayoutTransformDrag(gizmoHit.Value, e.Location);
                                if (_layoutCollisionTransformDrag != null)
                                {
                                    BeginLayoutUndo(gizmoHit.Value.Kind switch
                                    {
                                        CollisionTransformKind.Move => "移动",
                                        CollisionTransformKind.Rotate => "旋转",
                                        CollisionTransformKind.Scale => "缩放",
                                        _ => "编辑"
                                    });
                                    Capture = true;
                                    Invalidate();
                                    return;
                                }
                            }
                        }

                        var vh = HitTestLayoutPolygonVertexHandle(e.Location);
                        if (vh != null)
                        {
                            _layoutPolySelectedIndex = vh.Value.polyIndex;
                            _layoutCollisionTransformDrag = null;
                            if (_toolMode == CollisionToolMode.Vertex)
                            {
                                StartLayoutPolygonDrag(LayoutPolyDragKind.Vertex, vh.Value.polyIndex, vh.Value.vertexIndex, e.Location);
                                Invalidate();
                                return;
                            }
                            Invalidate();
                            return;
                        }

                        var ph = HitTestLayoutPolygon(e.Location);
                        if (ph != null)
                        {
                            _layoutPolySelectedIndex = ph.Value;
                            _layoutCollisionTransformDrag = null;
                            if (_toolMode == CollisionToolMode.Move)
                            {
                                _layoutCollisionTransformDrag = StartLayoutTransformDrag(new CollisionGizmoHit(CollisionTransformKind.Move, null), e.Location);
                                if (_layoutCollisionTransformDrag != null)
                                    BeginLayoutUndo("移动");
                                if (_layoutCollisionTransformDrag != null)
                                    Capture = true;
                                Invalidate();
                                return;
                            }
                            if (_toolMode == CollisionToolMode.Rotate)
                            {
                                _layoutCollisionTransformDrag = StartLayoutTransformDrag(new CollisionGizmoHit(CollisionTransformKind.Rotate, null), e.Location);
                                if (_layoutCollisionTransformDrag != null)
                                    BeginLayoutUndo("旋转");
                                if (_layoutCollisionTransformDrag != null)
                                    Capture = true;
                                Invalidate();
                                return;
                            }
                            if (_toolMode == CollisionToolMode.Scale)
                            {
                                var gizmoHit = HitTestLayoutCollisionGizmo(e.Location);
                                if (gizmoHit != null && gizmoHit.Value.Kind == CollisionTransformKind.Scale)
                                {
                                    _layoutCollisionTransformDrag = StartLayoutTransformDrag(gizmoHit.Value, e.Location);
                                    if (_layoutCollisionTransformDrag != null)
                                        BeginLayoutUndo("缩放");
                                    if (_layoutCollisionTransformDrag != null)
                                        Capture = true;
                                }
                                Invalidate();
                                return;
                            }
                            Invalidate();
                            return;
                        }

                        _layoutPolySelectedIndex = -1;
                        _layoutCollisionTransformDrag = null;
                        Invalidate();
                        return;
                    }

                    if (e.Button == MouseButtons.Right)
                    {
                        _layoutPolySelectedIndex = -1;
                        _layoutPolyDragging = false;
                        _layoutCollisionTransformDrag = null;
                        _layoutPolyDragKind = LayoutPolyDragKind.None;
                        _layoutPolyDragPolygonIndex = -1;
                        _layoutPolyDragVertexIndex = -1;
                        Capture = false;
                        Invalidate();
                        return;
                    }

                    return;
                }
                else
                {
                    if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right)
                        return;

                    _layoutCollisionDragging = true;
                    _layoutCollisionDragValue = e.Button == MouseButtons.Left;
                    _layoutCollisionLastIndex = -1;
                    BeginLayoutUndo(_layoutCollisionDragValue ? "绘制碰撞" : "擦除碰撞");
                    ApplyLayoutCollisionAtScreen(e.Location, _layoutCollisionDragValue);
                    Capture = true;
                    Invalidate();
                    return;
                }
            }

            if (_viewMode == CanvasViewMode.TileSetCollision)
            {
                if (e.Button == MouseButtons.Right)
                {
                    var poly = HitTestTileCollisionPolygon(e.Location);
                    if (poly != null)
                    {
                        SetSingleSelection(poly);
                        ShowTileCollisionContextMenu(e.Location, poly);
                        Invalidate();
                    }
                    return;
                }

                if (e.Button != MouseButtons.Left)
                    return;

                if (_pendingTileCollisionAction != PendingTileCollisionAction.None)
                {
                    if (_pendingTileCollisionAction == PendingTileCollisionAction.AddSquare)
                        TryAddSquareTileCollisionAtScreen(e.Location);
                    else if (_pendingTileCollisionAction == PendingTileCollisionAction.Remove)
                        TryRemoveTileCollisionAtScreen(e.Location);
                    return;
                }

                if (_toolMode == CollisionToolMode.Select)
                {
                    _tileCollisionMarquee = new TileCollisionMarquee(e.Location);
                    Capture = true;
                    Invalidate();
                    return;
                }

                if (_tileCollisionTransformDrag == null && _toolMode != CollisionToolMode.Vertex && _tileCollisionSelections.Count > 0)
                {
                    var gizmoHit = HitTestCollisionGizmo(e.Location);
                    if (gizmoHit != null)
                    {
                        _tileCollisionTransformDrag = StartTransformDrag(gizmoHit.Value, e.Location);
                        if (_tileCollisionTransformDrag != null)
                        {
                            Capture = true;
                            Invalidate();
                            return;
                        }
                    }
                }

                if (_toolMode == CollisionToolMode.Vertex)
                {
                    var handleHit = HitTestTileCollisionVertexHandle(e.Location);
                    if (handleHit != null)
                    {
                        SetSingleSelection(handleHit.Value.selection);
                        _tileCollisionDrag = new TileCollisionDrag(handleHit.Value.selection, handleHit.Value.vertexIndex);
                        Capture = true;
                        Invalidate();
                        return;
                    }
                }

                var polyHit = HitTestTileCollisionPolygon(e.Location);
                if (polyHit != null)
                {
                    var ctrl = ModifierKeys.HasFlag(Keys.Control);
                    if (_toolMode == CollisionToolMode.Vertex)
                        SetSingleSelection(polyHit);
                    else if (ctrl)
                        ToggleSelection(polyHit);
                    else
                        SetSingleSelection(polyHit);

                    _tileCollisionDrag = null;
                    _tileCollisionTransformDrag = null;
                    Invalidate();
                    if (_toolMode == CollisionToolMode.Move)
                    {
                        _tileCollisionTransformDrag = StartTransformDrag(new CollisionGizmoHit(CollisionTransformKind.Move, null), e.Location);
                        if (_tileCollisionTransformDrag != null)
                        {
                            Capture = true;
                            Invalidate();
                        }
                    }
                    else if (_toolMode == CollisionToolMode.Rotate)
                    {
                        _tileCollisionTransformDrag = StartTransformDrag(new CollisionGizmoHit(CollisionTransformKind.Rotate, null), e.Location);
                        if (_tileCollisionTransformDrag != null)
                        {
                            Capture = true;
                            Invalidate();
                        }
                    }
                    else if (_toolMode == CollisionToolMode.Scale)
                    {
                        var gizmoHit = HitTestCollisionGizmo(e.Location);
                        if (gizmoHit != null && gizmoHit.Value.Kind == CollisionTransformKind.Scale)
                        {
                            _tileCollisionTransformDrag = StartTransformDrag(gizmoHit.Value, e.Location);
                            if (_tileCollisionTransformDrag != null)
                            {
                                Capture = true;
                                Invalidate();
                            }
                        }
                    }
                    return;
                }

                if (!ModifierKeys.HasFlag(Keys.Control))
                {
                    ClearSelection();
                    _tileCollisionDrag = null;
                    _tileCollisionTransformDrag = null;
                    Invalidate();
                }
            }

            if (e.Button != MouseButtons.Left)
                return;

            var hit = HitTest(e.Location);
            if (hit == null)
                return;

            _dragItem = hit.Value.item;
            _dragFromX = hit.Value.x;
            _dragFromY = hit.Value.y;
            _dragMoved = false;
            Capture = true;
        }

        private void ShowTileCollisionContextMenu(Point location, TileCollisionSelection selection)
        {
            var menu = new ContextMenuStrip();
            menu.Closed += (_, _) => BeginInvoke(() => menu.Dispose());

            var canDrop = new ToolStripMenuItem("设为可下跳(绿色)") { Checked = selection.OneWay };
            var cannotDrop = new ToolStripMenuItem("设为不可下跳(红色)") { Checked = !selection.OneWay };
            canDrop.Click += (_, _) => RequestSetOneWayForSelection(oneWay: true);
            cannotDrop.Click += (_, _) => RequestSetOneWayForSelection(oneWay: false);
            menu.Items.AddRange([canDrop, cannotDrop]);

            menu.Show(this, location);
        }

        private void RequestSetOneWayForSelection(bool oneWay)
        {
            if (_map == null)
                return;
            if (_tileCollisionSelections.Count == 0)
                return;

            var edits = new List<TileCollisionCommit>();
            for (var i = 0; i < _tileCollisionSelections.Count; i++)
            {
                var sel = _tileCollisionSelections[i];
                if (sel.OneWay == oneWay)
                    continue;
                edits.Add(new TileCollisionCommit(
                    sel.TileSetResPath,
                    sel.LayerNodePath,
                    sel.SourceId,
                    sel.AtlasX,
                    sel.AtlasY,
                    sel.CellX,
                    sel.CellY,
                    oneWay,
                    sel.Points.ToArray(),
                    sel.Points.ToArray()));
            }

            if (edits.Count == 0)
                return;

            CommitRequested?.Invoke(new CommitInfo(
                _map,
                CommitKind.TileCollisionPolygon,
                oneWay ? "设为可下跳" : "设为不可下跳",
                edits.Count == 1 ? $"{edits[0].AtlasX}:{edits[0].AtlasY}" : $"{edits.Count} 个节点",
                0,
                0,
                0,
                0,
                edits,
                null));
        }

        private sealed record TileCellHit(TileLayer Layer, TileCell Cell);

        private TileCellHit? HitTestTileCellAtScreen(Point location)
        {
            if (_map == null)
                return null;

            var world = ScreenToWorld(location);
            var cellX = (int)MathF.Floor(world.x / TileSize);
            var cellY = (int)MathF.Floor(world.y / TileSize);

            if (cellX < 0 || cellY < 0 || cellX >= Math.Max(1, _map.RoomWidth) || cellY >= Math.Max(1, _map.RoomHeight))
                return null;

            foreach (var layer in _map.TileLayers.OrderByDescending(l => l.ZIndex).ThenByDescending(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                var cell = layer.Cells.FirstOrDefault(c => c.X == cellX && c.Y == cellY);
                if (cell != null)
                    return new TileCellHit(layer, cell);
            }

            return null;
        }

        private void TryAddSquareTileCollisionAtScreen(Point location)
        {
            if (_map == null)
                return;

            var hit = HitTestTileCellAtScreen(location);
            if (hit == null)
            {
                ShowHoverHint?.Invoke("这里没有 Tile。", location);
                return;
            }

            var layer = hit.Layer;
            var cell = hit.Cell;
            if (string.IsNullOrWhiteSpace(layer.TileSetPath))
            {
                ShowHoverHint?.Invoke("该 TileMapLayer 未绑定 TileSet。", location);
                return;
            }

            var h = TileSize / 2f;
            var square = new GodotVector2[]
            {
                new(-h, -h),
                new(h, -h),
                new(h, h),
                new(-h, h)
            };
            for (var i = 0; i < square.Length; i++)
                square[i] = SnapLocalPointToNearbyVertex(cell.X, cell.Y, square[i], null, -1);

            var edit = new TileCollisionCommit(
                layer.TileSetPath,
                layer.NodePath,
                cell.SourceId,
                cell.AtlasX,
                cell.AtlasY,
                cell.X,
                cell.Y,
                OneWay: false,
                FromPoints: Array.Empty<GodotVector2>(),
                ToPoints: square);

            CommitRequested?.Invoke(new CommitInfo(
                _map,
                CommitKind.TileCollisionPolygon,
                "新增方形",
                $"{cell.X},{cell.Y}",
                0,
                0,
                0,
                0,
                [edit],
                null));
        }

        private void TryRemoveTileCollisionAtScreen(Point location)
        {
            if (_map == null)
                return;

            var hit = HitTestTileCellAtScreen(location);
            if (hit == null)
            {
                ShowHoverHint?.Invoke("这里没有 Tile。", location);
                return;
            }

            var layer = hit.Layer;
            var cell = hit.Cell;
            var edit = new TileCollisionAltCommit(layer.NodePath, cell.X, cell.Y, cell.Alternative, 0);
            CommitRequested?.Invoke(new CommitInfo(
                _map,
                CommitKind.TileCollisionAlternative,
                "移除碰撞",
                $"{cell.X},{cell.Y}",
                0,
                0,
                0,
                0,
                null,
                null,
                [edit]));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_map == null)
                return;

            if (_viewMode == CanvasViewMode.Map && _dragItem == null && e.Button == MouseButtons.None)
                UpdateHover(e.Location);

            if (_viewMode == CanvasViewMode.LayoutCollision && _layoutCollisionDragging && (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right))
            {
                ApplyLayoutCollisionAtScreen(e.Location, _layoutCollisionDragValue);
                Invalidate();
                return;
            }

            if (_viewMode == CanvasViewMode.LayoutCollision && _layoutCollisionTransformDrag != null && e.Button == MouseButtons.Left)
            {
                if (_layoutCollision == null)
                    return;
                var drag = _layoutCollisionTransformDrag;
                if (drag.PolygonIndex < 0 || drag.PolygonIndex >= _layoutCollision.Polygons.Count)
                    return;
                var pts = _layoutCollision.Polygons[drag.PolygonIndex];
                if (pts == null || pts.Count != drag.StartPoints.Count)
                    return;

                var mw = ScreenToWorld(e.Location);
                var mouseWorld = new GodotVector2(mw.x, mw.y);

                if (drag.Kind == CollisionTransformKind.Move)
                {
                    var delta = new GodotVector2(mouseWorld.X - drag.StartMouseWorld.X, mouseWorld.Y - drag.StartMouseWorld.Y);
                    ApplyMove(pts, drag.StartPoints, delta);
                    drag.Moved = MathF.Abs(delta.X) > 0.01f || MathF.Abs(delta.Y) > 0.01f;
                }
                else if (drag.Kind == CollisionTransformKind.Rotate)
                {
                    var a0 = MathF.Atan2(drag.StartMouseWorld.Y - drag.PivotWorld.Y, drag.StartMouseWorld.X - drag.PivotWorld.X);
                    var a1 = MathF.Atan2(mouseWorld.Y - drag.PivotWorld.Y, mouseWorld.X - drag.PivotWorld.X);
                    var da = a1 - a0;
                    ApplyRotate(pts, drag.StartPoints, drag.PivotWorld, da);
                    drag.Moved = MathF.Abs(da) > 0.0008f;
                }
                else if (drag.Kind == CollisionTransformKind.Scale && drag.ScaleHandle != null)
                {
                    var sx = 1f;
                    var sy = 1f;

                    if (drag.ScaleHandle.Value.AffectsX)
                    {
                        var denom = (drag.StartHandleWorld.X - drag.PivotWorld.X);
                        if (MathF.Abs(denom) > 0.0001f)
                            sx = (mouseWorld.X - drag.PivotWorld.X) / denom;
                    }
                    if (drag.ScaleHandle.Value.AffectsY)
                    {
                        var denom = (drag.StartHandleWorld.Y - drag.PivotWorld.Y);
                        if (MathF.Abs(denom) > 0.0001f)
                            sy = (mouseWorld.Y - drag.PivotWorld.Y) / denom;
                    }

                    if (ModifierKeys.HasFlag(Keys.Shift))
                    {
                        var s = (MathF.Abs(sx) + MathF.Abs(sy)) / 2f;
                        var sign = (sx < 0 || sy < 0) ? -1f : 1f;
                        s = MathF.Max(0.01f, s) * sign;
                        sx = drag.ScaleHandle.Value.AffectsX ? s : 1f;
                        sy = drag.ScaleHandle.Value.AffectsY ? s : 1f;
                    }

                    sx = Math.Clamp(sx, -100f, 100f);
                    sy = Math.Clamp(sy, -100f, 100f);
                    ApplyScale(pts, drag.StartPoints, drag.PivotWorld, sx, sy);
                    drag.Moved = MathF.Abs(sx - 1f) > 0.001f || MathF.Abs(sy - 1f) > 0.001f;
                }

                for (var i = 0; i < pts.Count; i++)
                {
                    var (cx, cy) = ClampToRoom(pts[i].X, pts[i].Y);
                    pts[i] = new GodotVector2(cx, cy);
                }

                _layoutCollisionTransformDrag = drag;
                LayoutCollisionChanged?.Invoke();
                Invalidate();
                return;
            }

            if (_viewMode == CanvasViewMode.LayoutCollision && _layoutPolyDragging && e.Button == MouseButtons.Left)
            {
                ApplyLayoutPolygonDrag(e.Location);
                Invalidate();
                return;
            }

            if (_tileCollisionMarquee != null && e.Button == MouseButtons.Left)
            {
                _tileCollisionMarquee.End = e.Location;
                Invalidate();
                return;
            }

            if (_tileCollisionDrag != null && e.Button == MouseButtons.Left)
            {
                var drag = _tileCollisionDrag;
                var sel = drag.Selection;
                var world = ScreenToWorld(e.Location);

                var centerX = sel.CellX * TileSize + TileSize / 2f;
                var centerY = sel.CellY * TileSize + TileSize / 2f;
                var local = new GodotVector2(world.x - centerX, world.y - centerY);
                var idx = drag.VertexIndex;
                if (idx >= 0 && idx < sel.Points.Count)
                {
                    sel.Points[idx] = SnapLocalPointToNearbyVertex(sel.CellX, sel.CellY, local, sel, idx);
                    drag.Moved = true;
                    _tileCollisionDrag = drag;
                    Invalidate();
                }
                return;
            }

            if (_tileCollisionTransformDrag != null && e.Button == MouseButtons.Left)
            {
                var drag = _tileCollisionTransformDrag;
                var mw = ScreenToWorld(e.Location);
                var mouseWorld = new GodotVector2(mw.x, mw.y);

                if (drag.Kind == CollisionTransformKind.Move)
                {
                    var delta = new GodotVector2(mouseWorld.X - drag.StartMouseWorld.X, mouseWorld.Y - drag.StartMouseWorld.Y);
                    for (var s = 0; s < drag.Selections.Count; s++)
                    {
                        var sel = drag.Selections[s];
                        var start = drag.StartPoints[s];
                        var center = new GodotVector2(sel.CellX * TileSize + TileSize / 2f, sel.CellY * TileSize + TileSize / 2f);
                        sel.Points.Clear();
                        for (var i = 0; i < start.Count; i++)
                        {
                            var w = new GodotVector2(center.X + start[i].X, center.Y + start[i].Y);
                            var w2 = new GodotVector2(w.X + delta.X, w.Y + delta.Y);
                            sel.Points.Add(new GodotVector2(w2.X - center.X, w2.Y - center.Y));
                        }
                    }
                    drag.Moved = MathF.Abs(delta.X) > 0.01f || MathF.Abs(delta.Y) > 0.01f;
                }
                else if (drag.Kind == CollisionTransformKind.Rotate)
                {
                    var a0 = MathF.Atan2(drag.StartMouseWorld.Y - drag.PivotWorld.Y, drag.StartMouseWorld.X - drag.PivotWorld.X);
                    var a1 = MathF.Atan2(mouseWorld.Y - drag.PivotWorld.Y, mouseWorld.X - drag.PivotWorld.X);
                    var da = a1 - a0;
                    var c = MathF.Cos(da);
                    var s = MathF.Sin(da);
                    for (var si = 0; si < drag.Selections.Count; si++)
                    {
                        var sel = drag.Selections[si];
                        var start = drag.StartPoints[si];
                        var center = new GodotVector2(sel.CellX * TileSize + TileSize / 2f, sel.CellY * TileSize + TileSize / 2f);
                        sel.Points.Clear();
                        for (var i = 0; i < start.Count; i++)
                        {
                            var w = new GodotVector2(center.X + start[i].X, center.Y + start[i].Y);
                            var x = w.X - drag.PivotWorld.X;
                            var y = w.Y - drag.PivotWorld.Y;
                            var rx = x * c - y * s;
                            var ry = x * s + y * c;
                            var w2 = new GodotVector2(rx + drag.PivotWorld.X, ry + drag.PivotWorld.Y);
                            sel.Points.Add(new GodotVector2(w2.X - center.X, w2.Y - center.Y));
                        }
                    }
                    drag.Moved = MathF.Abs(da) > 0.0008f;
                }
                else if (drag.Kind == CollisionTransformKind.Scale && drag.ScaleHandle != null)
                {
                    var sx = 1f;
                    var sy = 1f;

                    if (drag.ScaleHandle.Value.AffectsX)
                    {
                        var denom = (drag.StartHandleWorld.X - drag.PivotWorld.X);
                        if (MathF.Abs(denom) > 0.0001f)
                            sx = (mouseWorld.X - drag.PivotWorld.X) / denom;
                    }
                    if (drag.ScaleHandle.Value.AffectsY)
                    {
                        var denom = (drag.StartHandleWorld.Y - drag.PivotWorld.Y);
                        if (MathF.Abs(denom) > 0.0001f)
                            sy = (mouseWorld.Y - drag.PivotWorld.Y) / denom;
                    }

                    if (ModifierKeys.HasFlag(Keys.Shift))
                    {
                        var s = (MathF.Abs(sx) + MathF.Abs(sy)) / 2f;
                        var sign = (sx < 0 || sy < 0) ? -1f : 1f;
                        s = MathF.Max(0.01f, s) * sign;
                        sx = drag.ScaleHandle.Value.AffectsX ? s : 1f;
                        sy = drag.ScaleHandle.Value.AffectsY ? s : 1f;
                    }

                    sx = Math.Clamp(sx, -100f, 100f);
                    sy = Math.Clamp(sy, -100f, 100f);
                    for (var si = 0; si < drag.Selections.Count; si++)
                    {
                        var sel = drag.Selections[si];
                        var start = drag.StartPoints[si];
                        var center = new GodotVector2(sel.CellX * TileSize + TileSize / 2f, sel.CellY * TileSize + TileSize / 2f);
                        sel.Points.Clear();
                        for (var i = 0; i < start.Count; i++)
                        {
                            var w = new GodotVector2(center.X + start[i].X, center.Y + start[i].Y);
                            var dx = w.X - drag.PivotWorld.X;
                            var dy = w.Y - drag.PivotWorld.Y;
                            var w2 = new GodotVector2(drag.PivotWorld.X + dx * sx, drag.PivotWorld.Y + dy * sy);
                            sel.Points.Add(new GodotVector2(w2.X - center.X, w2.Y - center.Y));
                        }
                    }
                    drag.Moved = MathF.Abs(sx - 1f) > 0.001f || MathF.Abs(sy - 1f) > 0.001f;
                }

                _tileCollisionTransformDrag = drag;
                Invalidate();
                return;
            }

            if (_map == null || _dragItem == null || e.Button != MouseButtons.Left)
                return;

            var worldPos = ScreenToWorld(e.Location);
            var clamped = ClampToRoom(worldPos.x, worldPos.y);
            if (_dragItem is Portal p)
            {
                if (p.X != clamped.x || p.Y != clamped.y)
                {
                    p.X = clamped.x;
                    p.Y = clamped.y;
                    _dragMoved = true;
                    Invalidate();
                }
            }
            else if (_dragItem is PlacedEntity ent)
            {
                if (ent.X != clamped.x || ent.Y != clamped.y)
                {
                    ent.X = clamped.x;
                    ent.Y = clamped.y;
                    _dragMoved = true;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ClearHover();
        }

        private void UpdateHover(Point p)
        {
            var hit = HitTest(p);
            var item = hit?.item;
            if (ReferenceEquals(item, _hoverItem))
                return;

            ClearHover();
            if (item == null)
                return;

            _hoverItem = item;
            var text = "";
            if (item is Portal portal)
                text = GetPortalHoverText?.Invoke(portal) ?? $"Portal: {portal.Name}";
            else if (item is PlacedEntity ent)
                text = GetEntityHoverText?.Invoke(ent) ?? (ent.Type.Length > 0 ? ent.Type : "Entity");

            if (text.Length > 0)
                ShowHoverHint?.Invoke(text, p);

            Cursor = item is Portal ? Cursors.Hand : Cursors.Default;
        }

        private void ClearHover()
        {
            _hoverItem = null;
            HideHoverHint?.Invoke();
            Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_map == null)
                return;

            Capture = false;

            if (_viewMode == CanvasViewMode.LayoutCollision)
            {
                CommitLayoutUndoIfNeeded();
                _layoutCollisionDragging = false;
                _layoutCollisionLastIndex = -1;
                _layoutPolyDragging = false;
                _layoutCollisionTransformDrag = null;
                _layoutPolyDragKind = LayoutPolyDragKind.None;
                _layoutPolyDragPolygonIndex = -1;
                _layoutPolyDragVertexIndex = -1;
                _layoutPolyDragStartPoints = null;
                Invalidate();
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            if (_tileCollisionMarquee != null)
            {
                var marquee = _tileCollisionMarquee;
                _tileCollisionMarquee = null;

                var ctrl = ModifierKeys.HasFlag(Keys.Control);
                if (marquee.IsDrag)
                {
                    var hits = HitTestTileCollisionsInRect(marquee.GetRect());
                    if (!ctrl)
                        ClearSelection();
                    foreach (var h in hits)
                        AddSelection(h);
                }
                else
                {
                    var polyHit = HitTestTileCollisionPolygon(e.Location);
                    if (polyHit != null)
                    {
                        if (ctrl)
                            ToggleSelection(polyHit);
                        else
                            SetSingleSelection(polyHit);
                    }
                    else
                    {
                        if (!ctrl)
                            ClearSelection();
                    }
                }

                Invalidate();
                return;
            }

            if (_tileCollisionDrag != null)
            {
                var drag = _tileCollisionDrag;
                _tileCollisionDrag = null;

                if (drag.Moved)
                {
                    var sel = drag.Selection;
                    var idx = drag.VertexIndex;
                    var from = idx >= 0 && idx < drag.OriginalPoints.Count ? drag.OriginalPoints[idx] : new GodotVector2(0, 0);
                    var to = idx >= 0 && idx < sel.Points.Count ? sel.Points[idx] : new GodotVector2(0, 0);

                    var ok = CommitRequested?.Invoke(new CommitInfo(
                        _map,
                        CommitKind.TileCollisionPolygon,
                        sel.OneWay ? "落地判定" : "碰撞判定",
                        $"{Path.GetFileName(sel.TileSetResPath)} source:{sel.SourceId} atlas:({sel.AtlasX},{sel.AtlasY}) cell:({sel.CellX},{sel.CellY})",
                        from.X,
                        from.Y,
                        to.X,
                        to.Y,
                        [new TileCollisionCommit(sel.TileSetResPath, sel.LayerNodePath, sel.SourceId, sel.AtlasX, sel.AtlasY, sel.CellX, sel.CellY, sel.OneWay, drag.OriginalPoints, sel.Points.ToArray())],
                        null
                    )) ?? true;

                    if (!ok)
                    {
                        sel.Points.Clear();
                        sel.Points.AddRange(drag.OriginalPoints);
                    }
                    else
                    {
                        EvictTileSetCache(sel.TileSetResPath);
                    }
                }

                Invalidate();
                return;
            }

            if (_tileCollisionTransformDrag != null)
            {
                var drag = _tileCollisionTransformDrag;
                _tileCollisionTransformDrag = null;

                if (drag.Moved)
                {
                    var world = ScreenToWorld(e.Location);
                    var from = drag.StartMouseWorld;
                    var to = new GodotVector2(world.x, world.y);

                    var kind = drag.Kind switch
                    {
                        CollisionTransformKind.Move => "移动",
                        CollisionTransformKind.Rotate => "旋转",
                        CollisionTransformKind.Scale => "拉伸",
                        _ => "编辑"
                    };

                    var commits = new List<TileCollisionCommit>(drag.Selections.Count);
                    for (var i = 0; i < drag.Selections.Count; i++)
                    {
                        var sel = drag.Selections[i];
                        commits.Add(new TileCollisionCommit(sel.TileSetResPath, sel.LayerNodePath, sel.SourceId, sel.AtlasX, sel.AtlasY, sel.CellX, sel.CellY, sel.OneWay, drag.StartPoints[i], sel.Points.ToArray()));
                    }

                    var ok = CommitRequested?.Invoke(new CommitInfo(
                        _map,
                        CommitKind.TileCollisionPolygon,
                        $"{kind}碰撞",
                        $"{commits.Count} 个节点",
                        from.X,
                        from.Y,
                        to.X,
                        to.Y,
                        commits,
                        null
                    )) ?? true;

                    if (!ok)
                    {
                        for (var i = 0; i < drag.Selections.Count; i++)
                        {
                            var sel = drag.Selections[i];
                            sel.Points.Clear();
                            sel.Points.AddRange(drag.StartPoints[i]);
                        }
                    }
                    else
                    {
                        foreach (var s in drag.Selections.Select(s => s.TileSetResPath).Distinct(StringComparer.OrdinalIgnoreCase))
                            EvictTileSetCache(s);
                    }
                }

                Invalidate();
                return;
            }

            if (_dragItem != null && _dragMoved)
            {
                var (kind, name, toX, toY) = DescribeDragTarget(_dragItem);
                var nodePath = _dragItem is Portal p ? p.NodePath : _dragItem is PlacedEntity ent ? ent.NodePath : "";
                var nodeCommit = new NodePositionCommit(_map.ScenePath, nodePath, _dragFromX, _dragFromY, toX, toY);
                var ok = CommitRequested?.Invoke(new CommitInfo(_map, CommitKind.NodePosition, kind, name, _dragFromX, _dragFromY, toX, toY, null, nodeCommit)) ?? true;
                if (!ok)
                    RevertDrag(_dragItem, _dragFromX, _dragFromY);
            }

            _dragItem = null;
            _dragMoved = false;
            Invalidate();
        }

        private (string kind, string name, float toX, float toY) DescribeDragTarget(object item)
        {
            if (item is Portal p)
                return ("Portal", p.Name, p.X, p.Y);
            if (item is PlacedEntity e)
                return (e.Type.Length > 0 ? e.Type : "Entity", e.Prefab.Length > 0 ? Path.GetFileNameWithoutExtension(e.Prefab) : "Entity", e.X, e.Y);
            return ("对象", item.GetType().Name, 0, 0);
        }

        private void RevertDrag(object item, float x, float y)
        {
            if (item is Portal p)
            {
                p.X = x;
                p.Y = y;
                return;
            }
            if (item is PlacedEntity e)
            {
                e.X = x;
                e.Y = y;
            }
        }

        private void DrawRoom(Graphics g, int roomW, int roomH, float scale, PointF offset)
        {
            var rect = new RectangleF(offset.X, offset.Y, roomW * scale, roomH * scale);
            using var borderPen = new Pen(Color.FromArgb(160, 200, 200, 200), 2f);
            using var gridPen = new Pen(Color.FromArgb(28, 220, 220, 220), 1f);

            for (var x = 0; x <= roomW; x += TileSize)
            {
                var sx = offset.X + x * scale;
                g.DrawLine(gridPen, sx, offset.Y, sx, offset.Y + roomH * scale);
            }
            for (var y = 0; y <= roomH; y += TileSize)
            {
                var sy = offset.Y + y * scale;
                g.DrawLine(gridPen, offset.X, sy, offset.X + roomW * scale, sy);
            }

            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private void DrawMarkers(Graphics g, float scale, PointF offset)
        {
            if (_map == null)
                return;

            using var portalBrush = new SolidBrush(Color.FromArgb(220, 74, 144, 226));
            using var entityBrush = new SolidBrush(Color.FromArgb(220, 245, 165, 66));
            using var textBrush = new SolidBrush(Color.FromArgb(220, 230, 230, 230));
            using var selPen = new Pen(Color.FromArgb(230, 255, 255, 255), 2f);

            foreach (var p in _map.Portals)
            {
                var s = WorldToScreen(p.X, p.Y, scale, offset);
                var preview = TryLoadPortalPreviewImage(p);
                if (preview != null)
                {
                    var u = MathF.Max(0.001f, p.Upscale);
                    var w = preview.Width * scale * u;
                    var h = preview.Height * scale * u;
                    var dest = new RectangleF(s.X - w / 2f, s.Y - h / 2f, w, h);
                    var prevInterp = g.InterpolationMode;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.DrawImage(preview, dest);
                    g.InterpolationMode = prevInterp;
                }
                var r = MarkerRadius;
                g.FillEllipse(portalBrush, s.X - r, s.Y - r, r * 2, r * 2);
                if (ReferenceEquals(_dragItem, p))
                    g.DrawEllipse(selPen, s.X - r - 2, s.Y - r - 2, (r + 2) * 2, (r + 2) * 2);
                g.DrawString(p.Name, Font, textBrush, s.X + r + 2, s.Y - r - 2);
            }

            foreach (var ent in _map.Entities)
            {
                var s = WorldToScreen(ent.X, ent.Y, scale, offset);
                var r = MarkerRadius;
                g.FillRectangle(entityBrush, s.X - r, s.Y - r, r * 2, r * 2);
                if (ReferenceEquals(_dragItem, ent))
                    g.DrawRectangle(selPen, s.X - r - 2, s.Y - r - 2, (r + 2) * 2, (r + 2) * 2);
                var label = ent.Type.Length > 0 ? ent.Type : "Entity";
                g.DrawString(label, Font, textBrush, s.X + r + 2, s.Y - r - 2);
            }
        }

        private (object item, float x, float y)? HitTest(Point p)
        {
            if (_map == null)
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var r = MarkerRadius + 3;
            var r2 = r * r;

            foreach (var portal in _map.Portals)
            {
                var s = WorldToScreen(portal.X, portal.Y, scale, offset);
                var dx = p.X - s.X;
                var dy = p.Y - s.Y;
                if (dx * dx + dy * dy <= r2)
                    return (portal, portal.X, portal.Y);
            }
            foreach (var ent in _map.Entities)
            {
                var s = WorldToScreen(ent.X, ent.Y, scale, offset);
                var dx = p.X - s.X;
                var dy = p.Y - s.Y;
                if (dx * dx + dy * dy <= r2)
                    return (ent, ent.X, ent.Y);
            }
            return null;
        }

        private (float x, float y) ScreenToWorld(Point p)
        {
            if (_map == null)
                return (0, 0);
            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);
            var x = (p.X - offset.X) / scale;
            var y = (p.Y - offset.Y) / scale;
            return (x, y);
        }

        private (float x, float y) ClampToRoom(float x, float y)
        {
            if (_map == null)
                return (x, y);
            var maxX = Math.Max(1, _map.RoomWidth) * TileSize;
            var maxY = Math.Max(1, _map.RoomHeight) * TileSize;
            x = Math.Clamp(x, 0, maxX);
            y = Math.Clamp(y, 0, maxY);
            return ((float)Math.Round(x), (float)Math.Round(y));
        }

        private static PointF WorldToScreen(float x, float y, float scale, PointF offset)
        {
            return new PointF(offset.X + x * scale, offset.Y + y * scale);
        }

        private float ComputeScale(int roomW, int roomH)
        {
            var w = Math.Max(1, ClientSize.Width - Pad * 2);
            var h = Math.Max(1, ClientSize.Height - Pad * 2);
            var sx = w / (float)Math.Max(1, roomW);
            var sy = h / (float)Math.Max(1, roomH);
            return MathF.Max(0.1f, MathF.Min(sx, sy));
        }

        private PointF ComputeOffset(int roomW, int roomH, float scale)
        {
            var drawW = roomW * scale;
            var drawH = roomH * scale;
            var x = (ClientSize.Width - drawW) / 2f;
            var y = (ClientSize.Height - drawH) / 2f;
            return new PointF(MathF.Max(Pad, x), MathF.Max(Pad, y));
        }

        private sealed class TileCollisionSelection
        {
            public required string TileSetResPath { get; init; }
            public required string LayerNodePath { get; init; }
            public required int SourceId { get; init; }
            public required int AtlasX { get; init; }
            public required int AtlasY { get; init; }
            public required int CellX { get; init; }
            public required int CellY { get; init; }
            public required int Alternative { get; init; }
            public required bool OneWay { get; init; }
            public required List<GodotVector2> Points { get; init; }
        }

        private sealed class TileCollisionDrag
        {
            public TileCollisionSelection Selection { get; }
            public int VertexIndex { get; }
            public bool Moved { get; set; }
            public List<GodotVector2> OriginalPoints { get; }

            public TileCollisionDrag(TileCollisionSelection selection, int vertexIndex)
            {
                Selection = selection;
                VertexIndex = vertexIndex;
                OriginalPoints = selection.Points.ToList();
            }
        }

        private sealed class TileCollisionMarquee
        {
            public Point Start { get; }
            public Point End { get; set; }

            public TileCollisionMarquee(Point start)
            {
                Start = start;
                End = start;
            }

            public Rectangle GetRect()
            {
                var x1 = Math.Min(Start.X, End.X);
                var y1 = Math.Min(Start.Y, End.Y);
                var x2 = Math.Max(Start.X, End.X);
                var y2 = Math.Max(Start.Y, End.Y);
                return Rectangle.FromLTRB(x1, y1, x2, y2);
            }

            public bool IsDrag => Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y) >= 6;
        }

        private TileCollisionSelection? FindSelection(string layerNodePath, int cellX, int cellY)
        {
            for (var i = 0; i < _tileCollisionSelections.Count; i++)
            {
                var s = _tileCollisionSelections[i];
                if (s.CellX == cellX && s.CellY == cellY && string.Equals(s.LayerNodePath, layerNodePath, StringComparison.Ordinal))
                    return s;
            }
            return null;
        }

        private TileCollisionSelection? GetPrimarySelection()
        {
            return _tileCollisionSelections.Count > 0 ? _tileCollisionSelections[0] : null;
        }

        private void SetSingleSelection(TileCollisionSelection sel)
        {
            _tileCollisionSelections.Clear();
            _tileCollisionSelections.Add(sel);
        }

        private void ToggleSelection(TileCollisionSelection sel)
        {
            for (var i = 0; i < _tileCollisionSelections.Count; i++)
            {
                var s = _tileCollisionSelections[i];
                if (s.CellX == sel.CellX && s.CellY == sel.CellY && string.Equals(s.LayerNodePath, sel.LayerNodePath, StringComparison.Ordinal))
                {
                    _tileCollisionSelections.RemoveAt(i);
                    return;
                }
            }
            _tileCollisionSelections.Add(sel);
        }

        private void AddSelection(TileCollisionSelection sel)
        {
            if (FindSelection(sel.LayerNodePath, sel.CellX, sel.CellY) != null)
                return;
            _tileCollisionSelections.Add(sel);
        }

        private void ClearSelection()
        {
            _tileCollisionSelections.Clear();
        }

        private void DrawTileCollisionOverlays(Graphics g, int roomW, int roomH, float scale, PointF offset)
        {
            if (_map == null)
                return;
            if (string.IsNullOrWhiteSpace(_godotRoot))
                return;

            var clip = new RectangleF(offset.X, offset.Y, roomW * scale, roomH * scale);
            using var dim = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
            g.FillRectangle(dim, clip);

            using var solidPen = new Pen(Color.FromArgb(220, 235, 90, 90), 2f);
            using var oneWayPen = new Pen(Color.FromArgb(220, 90, 235, 120), 2f);
            using var solidSelPen = new Pen(Color.FromArgb(240, 255, 140, 140), 3f);
            using var oneWaySelPen = new Pen(Color.FromArgb(240, 140, 255, 170), 3f);
            using var handleBrush = new SolidBrush(Color.FromArgb(240, 245, 245, 245));
            using var handleBorder = new Pen(Color.FromArgb(200, 30, 30, 30), 1f);

            foreach (var layer in _map.TileLayers.OrderBy(l => l.ZIndex).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                if (layer.TileSetPath.Length == 0)
                    continue;

                var tileset = TryLoadTileSet(layer.TileSetPath);
                if (tileset == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    if (!tileset.Sources.TryGetValue(cell.SourceId, out var src))
                        continue;
                    if (!src.PhysicsPolygons.TryGetValue((cell.AtlasX, cell.AtlasY, cell.Alternative), out var poly))
                        continue;
                    if (poly.Points.Count < 3)
                        continue;

                    var selected = FindSelection(layer.NodePath, cell.X, cell.Y);
                    var isSelected = selected != null;
                    var drawPoints = isSelected ? selected!.Points : poly.Points;
                    var screenPts = new PointF[drawPoints.Count];
                    for (var i = 0; i < drawPoints.Count; i++)
                    {
                        var wp = ToTileWorld(cell.X, cell.Y, drawPoints[i]);
                        screenPts[i] = WorldToScreen(wp.x, wp.y, scale, offset);
                    }

                    var pen = poly.OneWay
                        ? (isSelected ? oneWaySelPen : oneWayPen)
                        : (isSelected ? solidSelPen : solidPen);

                    g.DrawPolygon(pen, screenPts);
                }
            }

            if (_tileCollisionMarquee != null)
            {
                var rect = _tileCollisionMarquee.GetRect();
                using var fill = new SolidBrush(Color.FromArgb(45, 180, 220, 255));
                using var border = new Pen(Color.FromArgb(200, 180, 220, 255), 1.5f);
                g.FillRectangle(fill, rect);
                g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            }

            var primary = GetPrimarySelection();
            if (_toolMode == CollisionToolMode.Vertex && primary != null)
            {
                var pts = primary.Points;
                for (var i = 0; i < pts.Count; i++)
                {
                    var wp = ToTileWorld(primary.CellX, primary.CellY, pts[i]);
                    var sp = WorldToScreen(wp.x, wp.y, scale, offset);
                    var r = CollisionHandleRadius;
                    g.FillEllipse(handleBrush, sp.X - r, sp.Y - r, r * 2, r * 2);
                    g.DrawEllipse(handleBorder, sp.X - r, sp.Y - r, r * 2, r * 2);
                }
            }
            else if ((_toolMode == CollisionToolMode.Move || _toolMode == CollisionToolMode.Rotate || _toolMode == CollisionToolMode.Scale) && _tileCollisionSelections.Count > 0)
            {
                DrawGroupCollisionGizmo(g, scale, offset);
            }
        }

        private void DrawLayoutCollisionOverlays(Graphics g, int roomW, int roomH, float scale, PointF offset)
        {
            if (_map == null || _layoutCollision == null)
                return;

            var clip = new RectangleF(offset.X, offset.Y, roomW * scale, roomH * scale);
            using var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            g.FillRectangle(dim, clip);

            using var borderPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);

            if (_layoutCollision.Polygons.Count > 0)
            {
                using var polyFill = new SolidBrush(Color.FromArgb(170, 235, 90, 90));
                using var polyFillSel = new SolidBrush(Color.FromArgb(190, 120, 200, 255));
                using var polyPen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f);
                using var polyPenSel = new Pen(Color.FromArgb(240, 180, 220, 255), 2f);
                using var handleBrush = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
                using var handleBorder = new Pen(Color.FromArgb(220, 30, 30, 30), 1f);

                for (var pi = 0; pi < _layoutCollision.Polygons.Count; pi++)
                {
                    var pts = _layoutCollision.Polygons[pi];
                    if (pts == null || pts.Count < 3)
                        continue;
                    var screenPts = new PointF[pts.Count];
                    for (var i = 0; i < pts.Count; i++)
                        screenPts[i] = WorldToScreen(pts[i].X, pts[i].Y, scale, offset);

                    var selected = pi == _layoutPolySelectedIndex;
                    g.FillPolygon(selected ? polyFillSel : polyFill, screenPts);
                    g.DrawPolygon(selected ? polyPenSel : polyPen, screenPts);
                }

                if (_layoutPolySelectedIndex >= 0 && _layoutPolySelectedIndex < _layoutCollision.Polygons.Count)
                {
                    var selPts = _layoutCollision.Polygons[_layoutPolySelectedIndex];
                    if (selPts != null)
                    {
                        var r = CollisionHandleRadius;
                        for (var i = 0; i < selPts.Count; i++)
                        {
                            var sp = WorldToScreen(selPts[i].X, selPts[i].Y, scale, offset);
                            g.FillEllipse(handleBrush, sp.X - r, sp.Y - r, r * 2, r * 2);
                            g.DrawEllipse(handleBorder, sp.X - r, sp.Y - r, r * 2, r * 2);
                        }
                    }
                }

                if ((_toolMode == CollisionToolMode.Move || _toolMode == CollisionToolMode.Rotate || _toolMode == CollisionToolMode.Scale) && _layoutPolySelectedIndex >= 0 && _layoutPolySelectedIndex < _layoutCollision.Polygons.Count)
                    DrawLayoutCollisionGizmo(g, scale, offset);
            }
            else
            {
                using var solidBrush = new SolidBrush(Color.FromArgb(190, 235, 90, 90));
                var w = Math.Max(1, _layoutCollision.RoomWidth);
                var h = Math.Max(1, _layoutCollision.RoomHeight);
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var idx = y * w + x;
                        if (idx < 0 || idx >= _layoutCollision.Solid.Length)
                            continue;
                        if (!_layoutCollision.Solid[idx])
                            continue;

                        var rx = offset.X + x * TileSize * scale;
                        var ry = offset.Y + y * TileSize * scale;
                        var rect = new RectangleF(rx, ry, TileSize * scale, TileSize * scale);
                        g.FillRectangle(solidBrush, rect);
                    }
                }
            }

            g.DrawRectangle(borderPen, clip.X, clip.Y, clip.Width, clip.Height);
        }

        private void DrawLayoutCollisionGizmo(Graphics g, float scale, PointF offset)
        {
            var bounds = ComputeLayoutSelectionBoundsScreen(scale, offset);
            if (bounds.width <= 0 || bounds.height <= 0)
                return;

            using var boxPen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f);
            using var handleFill = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
            using var handleBorder = new Pen(Color.FromArgb(210, 20, 20, 20), 1f);
            using var rotatePen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f);

            var rect = new RectangleF(bounds.minX, bounds.minY, bounds.width, bounds.height);
            g.DrawRectangle(boxPen, rect.X, rect.Y, rect.Width, rect.Height);

            var handles = GetScaleHandleRects(rect);
            foreach (var h in handles)
            {
                g.FillRectangle(handleFill, h);
                g.DrawRectangle(handleBorder, h.X, h.Y, h.Width, h.Height);
            }

            var rotateCenter = new PointF(rect.X + rect.Width / 2f, rect.Y - RotateHandleOffset * scale);
            g.DrawLine(rotatePen, rect.X + rect.Width / 2f, rect.Y, rotateCenter.X, rotateCenter.Y);
            var rr = RotateHandleRadius * scale;
            g.DrawEllipse(rotatePen, rotateCenter.X - rr, rotateCenter.Y - rr, rr * 2, rr * 2);
        }

        private bool TryGetLayoutCellIndex(Point screen, out int cellIndex)
        {
            cellIndex = -1;
            if (_map == null || _layoutCollision == null)
                return false;
            if (_layoutCollision.Polygons.Count > 0)
                return false;

            var world = ScreenToWorld(screen);
            var cellX = (int)MathF.Floor(world.x / TileSize);
            var cellY = (int)MathF.Floor(world.y / TileSize);

            var w = _layoutCollision.RoomWidth;
            var h = _layoutCollision.RoomHeight;
            if (cellX < 0 || cellY < 0 || cellX >= w || cellY >= h)
                return false;

            var idx = cellY * w + cellX;
            if (idx < 0 || idx >= _layoutCollision.Solid.Length)
                return false;

            cellIndex = idx;
            return true;
        }

        private void ApplyLayoutCollisionAtScreen(Point screen, bool solid)
        {
            if (_layoutCollision == null)
                return;
            if (_layoutCollision.Polygons.Count > 0)
                return;
            if (!TryGetLayoutCellIndex(screen, out var idx))
                return;
            if (idx == _layoutCollisionLastIndex)
                return;
            _layoutCollisionLastIndex = idx;
            if (_layoutCollision.Solid[idx] == solid)
                return;
            _layoutCollision.Solid[idx] = solid;
            LayoutCollisionChanged?.Invoke();
        }

        private void StartLayoutPolygonDrag(LayoutPolyDragKind kind, int polyIndex, int vertexIndex, Point mouseScreen)
        {
            if (_layoutCollision == null)
                return;
            if (polyIndex < 0 || polyIndex >= _layoutCollision.Polygons.Count)
                return;
            var pts = _layoutCollision.Polygons[polyIndex];
            if (pts == null || pts.Count < 3)
                return;

            BeginLayoutUndo(kind switch
            {
                LayoutPolyDragKind.Vertex => "移动顶点",
                LayoutPolyDragKind.Move => "移动",
                LayoutPolyDragKind.Rotate => "旋转",
                LayoutPolyDragKind.Scale => "缩放",
                _ => "编辑"
            });
            _layoutPolyDragging = true;
            _layoutPolyDragKind = kind;
            _layoutPolyDragPolygonIndex = polyIndex;
            _layoutPolyDragVertexIndex = vertexIndex;

            var world = ScreenToWorld(mouseScreen);
            _layoutPolyDragStartWorld = (world.x, world.y);
            _layoutPolyDragStartPoints = pts.Select(p => new GodotVector2(p.X, p.Y)).ToList();
            _layoutPolyDragPivot = ComputePolygonCentroid(_layoutPolyDragStartPoints);
            _layoutPolyDragStartPivotToMouse = new GodotVector2(world.x - _layoutPolyDragPivot.X, world.y - _layoutPolyDragPivot.Y);
            Capture = true;
        }

        private void ApplyLayoutPolygonDrag(Point mouseScreen)
        {
            if (_layoutCollision == null)
                return;
            if (_layoutPolyDragStartPoints == null)
                return;
            var polyIndex = _layoutPolyDragPolygonIndex;
            if (polyIndex < 0 || polyIndex >= _layoutCollision.Polygons.Count)
                return;
            var pts = _layoutCollision.Polygons[polyIndex];
            if (pts == null || pts.Count != _layoutPolyDragStartPoints.Count)
                return;

            var world = ScreenToWorld(mouseScreen);
            var dx = world.x - _layoutPolyDragStartWorld.x;
            var dy = world.y - _layoutPolyDragStartWorld.y;

            if (_layoutPolyDragKind == LayoutPolyDragKind.Move)
            {
                for (var i = 0; i < pts.Count; i++)
                {
                    var start = _layoutPolyDragStartPoints[i];
                    var (cx, cy) = ClampToRoom(start.X + dx, start.Y + dy);
                    pts[i] = new GodotVector2(cx, cy);
                }
            }
            else if (_layoutPolyDragKind == LayoutPolyDragKind.Vertex)
            {
                var vi = _layoutPolyDragVertexIndex;
                if (vi < 0 || vi >= pts.Count)
                    return;
                var start = _layoutPolyDragStartPoints[vi];
                var (cx, cy) = ClampToRoom(start.X + dx, start.Y + dy);
                pts[vi] = new GodotVector2(cx, cy);
            }
            else if (_layoutPolyDragKind == LayoutPolyDragKind.Rotate)
            {
                var pv = _layoutPolyDragPivot;
                var v0 = _layoutPolyDragStartPivotToMouse;
                var v1 = new GodotVector2(world.x - pv.X, world.y - pv.Y);
                var a0 = MathF.Atan2(v0.Y, v0.X);
                var a1 = MathF.Atan2(v1.Y, v1.X);
                var da = a1 - a0;
                var c = MathF.Cos(da);
                var s = MathF.Sin(da);
                for (var i = 0; i < pts.Count; i++)
                {
                    var start = _layoutPolyDragStartPoints[i];
                    var x = start.X - pv.X;
                    var y = start.Y - pv.Y;
                    var rx = x * c - y * s;
                    var ry = x * s + y * c;
                    var (cx, cy) = ClampToRoom(pv.X + rx, pv.Y + ry);
                    pts[i] = new GodotVector2(cx, cy);
                }
            }
            else if (_layoutPolyDragKind == LayoutPolyDragKind.Scale)
            {
                var pv = _layoutPolyDragPivot;
                var v0 = _layoutPolyDragStartPivotToMouse;
                var v1 = new GodotVector2(world.x - pv.X, world.y - pv.Y);
                var sx = MathF.Abs(v0.X) < 0.001f ? 1f : v1.X / v0.X;
                var sy = MathF.Abs(v0.Y) < 0.001f ? 1f : v1.Y / v0.Y;
                sx = Math.Clamp(sx, 0.05f, 20f);
                sy = Math.Clamp(sy, 0.05f, 20f);
                for (var i = 0; i < pts.Count; i++)
                {
                    var start = _layoutPolyDragStartPoints[i];
                    var x = start.X - pv.X;
                    var y = start.Y - pv.Y;
                    var (cx, cy) = ClampToRoom(pv.X + x * sx, pv.Y + y * sy);
                    pts[i] = new GodotVector2(cx, cy);
                }
            }

            LayoutCollisionChanged?.Invoke();
        }

        private void BeginLayoutUndo(string name)
        {
            if (_layoutUndoBefore != null)
                return;
            if (_layoutCollision == null)
                return;
            _layoutUndoBefore = CloneCollisionLayoutData(_layoutCollision);
            _layoutUndoName = name ?? "";
        }

        private void CommitLayoutUndoIfNeeded()
        {
            if (_layoutUndoBefore == null)
                return;
            if (_layoutCollision == null)
            {
                _layoutUndoBefore = null;
                _layoutUndoName = "";
                return;
            }

            var before = _layoutUndoBefore;
            var after = CloneCollisionLayoutData(_layoutCollision);
            _layoutUndoBefore = null;
            var name = _layoutUndoName;
            _layoutUndoName = "";

            if (CollisionLayoutDataEquals(before, after))
                return;

            LayoutCollisionCommitted?.Invoke(string.IsNullOrWhiteSpace(name) ? "编辑" : name, before, after);
        }

        private static GodotVector2 ComputePolygonCentroid(List<GodotVector2> pts)
        {
            if (pts.Count == 0)
                return new GodotVector2(0, 0);
            float sx = 0;
            float sy = 0;
            for (var i = 0; i < pts.Count; i++)
            {
                sx += pts[i].X;
                sy += pts[i].Y;
            }
            return new GodotVector2(sx / pts.Count, sy / pts.Count);
        }

        private (int polyIndex, int vertexIndex)? HitTestLayoutPolygonVertexHandle(Point mouse)
        {
            if (_map == null || _layoutCollision == null)
                return null;
            if (_layoutCollision.Polygons.Count == 0)
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var r = CollisionHandleRadius + 2;
            var r2 = r * r;

            if (_layoutPolySelectedIndex >= 0 && _layoutPolySelectedIndex < _layoutCollision.Polygons.Count)
            {
                var pts = _layoutCollision.Polygons[_layoutPolySelectedIndex];
                if (pts != null)
                {
                    for (var i = 0; i < pts.Count; i++)
                    {
                        var sp = WorldToScreen(pts[i].X, pts[i].Y, scale, offset);
                        var dx = mouse.X - sp.X;
                        var dy = mouse.Y - sp.Y;
                        if (dx * dx + dy * dy <= r2)
                            return (_layoutPolySelectedIndex, i);
                    }
                }
            }

            for (var pi = _layoutCollision.Polygons.Count - 1; pi >= 0; pi--)
            {
                var pts = _layoutCollision.Polygons[pi];
                if (pts == null)
                    continue;
                for (var i = 0; i < pts.Count; i++)
                {
                    var sp = WorldToScreen(pts[i].X, pts[i].Y, scale, offset);
                    var dx = mouse.X - sp.X;
                    var dy = mouse.Y - sp.Y;
                    if (dx * dx + dy * dy <= r2)
                        return (pi, i);
                }
            }

            return null;
        }

        private int? HitTestLayoutPolygon(Point mouse)
        {
            if (_map == null || _layoutCollision == null)
                return null;
            if (_layoutCollision.Polygons.Count == 0)
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            for (var pi = _layoutCollision.Polygons.Count - 1; pi >= 0; pi--)
            {
                var pts = _layoutCollision.Polygons[pi];
                if (pts == null || pts.Count < 3)
                    continue;
                var screenPts = new PointF[pts.Count];
                for (var i = 0; i < pts.Count; i++)
                    screenPts[i] = WorldToScreen(pts[i].X, pts[i].Y, scale, offset);
                if (PointInPolygon(screenPts, mouse))
                    return pi;
            }

            return null;
        }

        private void DrawGroupCollisionGizmo(Graphics g, float scale, PointF offset)
        {
            var bounds = ComputeSelectionBoundsScreen(scale, offset);
            if (bounds.width <= 0 || bounds.height <= 0)
                return;

            using var boxPen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f);
            using var handleFill = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
            using var handleBorder = new Pen(Color.FromArgb(210, 20, 20, 20), 1f);
            using var rotatePen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f);

            var rect = new RectangleF(bounds.minX, bounds.minY, bounds.width, bounds.height);
            g.DrawRectangle(boxPen, rect.X, rect.Y, rect.Width, rect.Height);

            var handles = GetScaleHandleRects(rect);
            foreach (var h in handles)
            {
                g.FillRectangle(handleFill, h);
                g.DrawRectangle(handleBorder, h.X, h.Y, h.Width, h.Height);
            }

            var rotateCenter = new PointF(rect.X + rect.Width / 2f, rect.Y - RotateHandleOffset * scale);
            g.DrawLine(rotatePen, rect.X + rect.Width / 2f, rect.Y, rotateCenter.X, rotateCenter.Y);
            var rr = RotateHandleRadius * scale;
            g.DrawEllipse(rotatePen, rotateCenter.X - rr, rotateCenter.Y - rr, rr * 2, rr * 2);
        }

        private (float minX, float minY, float width, float height) ComputeSelectionBoundsScreen(float scale, PointF offset)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;

            for (var s = 0; s < _tileCollisionSelections.Count; s++)
            {
                var sel = _tileCollisionSelections[s];
                for (var i = 0; i < sel.Points.Count; i++)
                {
                    var wp = ToTileWorld(sel.CellX, sel.CellY, sel.Points[i]);
                    var sp = WorldToScreen(wp.x, wp.y, scale, offset);
                    minX = MathF.Min(minX, sp.X);
                    minY = MathF.Min(minY, sp.Y);
                    maxX = MathF.Max(maxX, sp.X);
                    maxY = MathF.Max(maxY, sp.Y);
                }
            }

            if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
                return (0, 0, 0, 0);
            return (minX, minY, MathF.Max(0, maxX - minX), MathF.Max(0, maxY - minY));
        }

        private (float minX, float minY, float width, float height) ComputeLayoutSelectionBoundsScreen(float scale, PointF offset)
        {
            if (_layoutCollision == null)
                return (0, 0, 0, 0);
            if (_layoutPolySelectedIndex < 0 || _layoutPolySelectedIndex >= _layoutCollision.Polygons.Count)
                return (0, 0, 0, 0);
            var pts = _layoutCollision.Polygons[_layoutPolySelectedIndex];
            if (pts == null || pts.Count < 3)
                return (0, 0, 0, 0);

            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < pts.Count; i++)
            {
                var sp = WorldToScreen(pts[i].X, pts[i].Y, scale, offset);
                minX = MathF.Min(minX, sp.X);
                minY = MathF.Min(minY, sp.Y);
                maxX = MathF.Max(maxX, sp.X);
                maxY = MathF.Max(maxY, sp.Y);
            }

            if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
                return (0, 0, 0, 0);
            return (minX, minY, MathF.Max(0, maxX - minX), MathF.Max(0, maxY - minY));
        }

        private (float minX, float minY, float width, float height) ComputeBoundsScreen(int cellX, int cellY, List<GodotVector2> pts, float scale, PointF offset)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var p in pts)
            {
                var wp = ToTileWorld(cellX, cellY, p);
                var sp = WorldToScreen(wp.x, wp.y, scale, offset);
                minX = MathF.Min(minX, sp.X);
                minY = MathF.Min(minY, sp.Y);
                maxX = MathF.Max(maxX, sp.X);
                maxY = MathF.Max(maxY, sp.Y);
            }
            if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
                return (0, 0, 0, 0);
            return (minX, minY, MathF.Max(0, maxX - minX), MathF.Max(0, maxY - minY));
        }

        private List<RectangleF> GetScaleHandleRects(RectangleF bounds)
        {
            var s = GizmoHandleSize;
            var hs = s / 2f;
            var cx = bounds.X + bounds.Width / 2f;
            var cy = bounds.Y + bounds.Height / 2f;
            var left = bounds.X;
            var right = bounds.X + bounds.Width;
            var top = bounds.Y;
            var bottom = bounds.Y + bounds.Height;

            return
            [
                new RectangleF(left - hs, top - hs, s, s),
                new RectangleF(cx - hs, top - hs, s, s),
                new RectangleF(right - hs, top - hs, s, s),
                new RectangleF(right - hs, cy - hs, s, s),
                new RectangleF(right - hs, bottom - hs, s, s),
                new RectangleF(cx - hs, bottom - hs, s, s),
                new RectangleF(left - hs, bottom - hs, s, s),
                new RectangleF(left - hs, cy - hs, s, s)
            ];
        }

        private (float x, float y) ToTileWorld(int cellX, int cellY, GodotVector2 local)
        {
            var centerX = cellX * TileSize + TileSize / 2f;
            var centerY = cellY * TileSize + TileSize / 2f;
            return (centerX + local.X, centerY + local.Y);
        }

        private IEnumerable<(float x, float y)> EnumerateCollisionWorldVertices(TileCollisionSelection? excludeSelection, int excludeVertexIndex)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                yield break;

            for (var si = 0; si < _tileCollisionSelections.Count; si++)
            {
                var sel = _tileCollisionSelections[si];
                for (var i = 0; i < sel.Points.Count; i++)
                {
                    if (ReferenceEquals(sel, excludeSelection) && i == excludeVertexIndex)
                        continue;
                    var wp = ToTileWorld(sel.CellX, sel.CellY, sel.Points[i]);
                    yield return (wp.x, wp.y);
                }
            }

            foreach (var layer in _map.TileLayers.OrderBy(l => l.ZIndex).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                if (layer.TileSetPath.Length == 0)
                    continue;

                var tileset = TryLoadTileSet(layer.TileSetPath);
                if (tileset == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    if (FindSelection(layer.NodePath, cell.X, cell.Y) != null)
                        continue;
                    if (!tileset.Sources.TryGetValue(cell.SourceId, out var src))
                        continue;
                    if (!src.PhysicsPolygons.TryGetValue((cell.AtlasX, cell.AtlasY, cell.Alternative), out var poly))
                        continue;
                    if (poly.Points.Count < 1)
                        continue;

                    for (var i = 0; i < poly.Points.Count; i++)
                    {
                        var wp = ToTileWorld(cell.X, cell.Y, poly.Points[i]);
                        yield return (wp.x, wp.y);
                    }
                }
            }
        }

        private GodotVector2 SnapLocalPointToNearbyVertex(int cellX, int cellY, GodotVector2 local, TileCollisionSelection? excludeSelection, int excludeVertexIndex)
        {
            var centerX = cellX * TileSize + TileSize / 2f;
            var centerY = cellY * TileSize + TileSize / 2f;
            var worldX = centerX + local.X;
            var worldY = centerY + local.Y;

            var bestX = worldX;
            var bestY = worldY;
            var bestDist2 = VertexSnapDistance * VertexSnapDistance;
            var found = false;

            foreach (var v in EnumerateCollisionWorldVertices(excludeSelection, excludeVertexIndex))
            {
                var dx = v.x - worldX;
                var dy = v.y - worldY;
                var d2 = dx * dx + dy * dy;
                if (d2 <= bestDist2)
                {
                    bestDist2 = d2;
                    bestX = v.x;
                    bestY = v.y;
                    found = true;
                }
            }

            if (!found)
                return local;
            return new GodotVector2(bestX - centerX, bestY - centerY);
        }

        private (TileCollisionSelection selection, int vertexIndex)? HitTestTileCollisionVertexHandle(Point mouse)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var r = CollisionHandleRadius + 2;
            var r2 = r * r;

            var primary = GetPrimarySelection();
            if (primary != null)
            {
                var sel = primary;
                for (var i = 0; i < sel.Points.Count; i++)
                {
                    var wp = ToTileWorld(sel.CellX, sel.CellY, sel.Points[i]);
                    var sp = WorldToScreen(wp.x, wp.y, scale, offset);
                    var dx = mouse.X - sp.X;
                    var dy = mouse.Y - sp.Y;
                    if (dx * dx + dy * dy <= r2)
                        return (sel, i);
                }
            }

            foreach (var layer in _map.TileLayers.OrderByDescending(l => l.ZIndex).ThenByDescending(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                if (layer.TileSetPath.Length == 0)
                    continue;
                var tileset = TryLoadTileSet(layer.TileSetPath);
                if (tileset == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    if (!tileset.Sources.TryGetValue(cell.SourceId, out var src))
                        continue;
                    if (!src.PhysicsPolygons.TryGetValue((cell.AtlasX, cell.AtlasY, cell.Alternative), out var poly))
                        continue;
                    if (poly.Points.Count < 3)
                        continue;

                    for (var i = 0; i < poly.Points.Count; i++)
                    {
                        var wp = ToTileWorld(cell.X, cell.Y, poly.Points[i]);
                        var sp = WorldToScreen(wp.x, wp.y, scale, offset);
                        var dx = mouse.X - sp.X;
                        var dy = mouse.Y - sp.Y;
                        if (dx * dx + dy * dy <= r2)
                        {
                            var selected = FindSelection(layer.NodePath, cell.X, cell.Y);
                            if (selected != null)
                                return (selected, i);
                            var sel = new TileCollisionSelection
                            {
                                TileSetResPath = layer.TileSetPath,
                                LayerNodePath = layer.NodePath,
                                SourceId = cell.SourceId,
                                AtlasX = cell.AtlasX,
                                AtlasY = cell.AtlasY,
                                CellX = cell.X,
                                CellY = cell.Y,
                                Alternative = cell.Alternative,
                                OneWay = poly.OneWay,
                                Points = poly.Points.ToList()
                            };
                            return (sel, i);
                        }
                    }
                }
            }

            return null;
        }

        private TileCollisionSelection? HitTestTileCollisionPolygon(Point mouse)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            for (var s = _tileCollisionSelections.Count - 1; s >= 0; s--)
            {
                var sel = _tileCollisionSelections[s];
                var screenPts = new PointF[sel.Points.Count];
                for (var i = 0; i < sel.Points.Count; i++)
                {
                    var wp = ToTileWorld(sel.CellX, sel.CellY, sel.Points[i]);
                    screenPts[i] = WorldToScreen(wp.x, wp.y, scale, offset);
                }
                if (PointInPolygon(screenPts, mouse))
                    return sel;
            }

            foreach (var layer in _map.TileLayers.OrderByDescending(l => l.ZIndex).ThenByDescending(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                if (layer.TileSetPath.Length == 0)
                    continue;
                var tileset = TryLoadTileSet(layer.TileSetPath);
                if (tileset == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    if (!tileset.Sources.TryGetValue(cell.SourceId, out var src))
                        continue;
                    if (!src.PhysicsPolygons.TryGetValue((cell.AtlasX, cell.AtlasY, cell.Alternative), out var poly))
                        continue;
                    if (poly.Points.Count < 3)
                        continue;

                    var screenPts = new PointF[poly.Points.Count];
                    for (var i = 0; i < poly.Points.Count; i++)
                    {
                        var wp = ToTileWorld(cell.X, cell.Y, poly.Points[i]);
                        screenPts[i] = WorldToScreen(wp.x, wp.y, scale, offset);
                    }

                    if (PointInPolygon(screenPts, mouse))
                    {
                        var selected = FindSelection(layer.NodePath, cell.X, cell.Y);
                        if (selected != null)
                            return selected;
                        return new TileCollisionSelection
                        {
                            TileSetResPath = layer.TileSetPath,
                            LayerNodePath = layer.NodePath,
                            SourceId = cell.SourceId,
                            AtlasX = cell.AtlasX,
                            AtlasY = cell.AtlasY,
                            CellX = cell.X,
                            CellY = cell.Y,
                            Alternative = cell.Alternative,
                            OneWay = poly.OneWay,
                            Points = poly.Points.ToList()
                        };
                    }
                }
            }

            return null;
        }

        private List<TileCollisionSelection> HitTestTileCollisionsInRect(Rectangle rect)
        {
            if (_map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return [];

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var result = new List<TileCollisionSelection>();
            foreach (var layer in _map.TileLayers.OrderByDescending(l => l.ZIndex).ThenByDescending(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!layer.Visible)
                    continue;
                if (layer.Cells.Count == 0)
                    continue;
                if (layer.TileSetPath.Length == 0)
                    continue;

                var tileset = TryLoadTileSet(layer.TileSetPath);
                if (tileset == null)
                    continue;

                foreach (var cell in layer.Cells)
                {
                    if (!tileset.Sources.TryGetValue(cell.SourceId, out var src))
                        continue;
                    if (!src.PhysicsPolygons.TryGetValue((cell.AtlasX, cell.AtlasY, cell.Alternative), out var poly))
                        continue;
                    if (poly.Points.Count < 3)
                        continue;

                    var selExisting = FindSelection(layer.NodePath, cell.X, cell.Y);
                    var pts = selExisting?.Points ?? poly.Points;

                    var minX = float.PositiveInfinity;
                    var minY = float.PositiveInfinity;
                    var maxX = float.NegativeInfinity;
                    var maxY = float.NegativeInfinity;
                    for (var i = 0; i < pts.Count; i++)
                    {
                        var wp = ToTileWorld(cell.X, cell.Y, pts[i]);
                        var sp = WorldToScreen(wp.x, wp.y, scale, offset);
                        minX = MathF.Min(minX, sp.X);
                        minY = MathF.Min(minY, sp.Y);
                        maxX = MathF.Max(maxX, sp.X);
                        maxY = MathF.Max(maxY, sp.Y);
                    }
                    if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
                        continue;

                    var b = Rectangle.FromLTRB((int)MathF.Floor(minX), (int)MathF.Floor(minY), (int)MathF.Ceiling(maxX), (int)MathF.Ceiling(maxY));
                    if (!rect.IntersectsWith(b))
                        continue;

                    if (selExisting != null)
                    {
                        result.Add(selExisting);
                        continue;
                    }

                    result.Add(new TileCollisionSelection
                    {
                        TileSetResPath = layer.TileSetPath,
                        LayerNodePath = layer.NodePath,
                        SourceId = cell.SourceId,
                        AtlasX = cell.AtlasX,
                        AtlasY = cell.AtlasY,
                        CellX = cell.X,
                        CellY = cell.Y,
                        Alternative = cell.Alternative,
                        OneWay = poly.OneWay,
                        Points = poly.Points.ToList()
                    });
                }
            }

            return result;
        }

        private static bool PointInPolygon(PointF[] poly, Point p)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var xi = poly[i].X;
                var yi = poly[i].Y;
                var xj = poly[j].X;
                var yj = poly[j].Y;

                var intersect = ((yi > p.Y) != (yj > p.Y)) &&
                                (p.X < (xj - xi) * (p.Y - yi) / Math.Max(0.0001f, (yj - yi)) + xi);
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        private enum CollisionTransformKind
        {
            Move = 0,
            Rotate = 1,
            Scale = 2
        }

        private readonly record struct CollisionScaleHandle(CollisionScaleHandleKind Kind, bool AffectsX, bool AffectsY);

        private enum CollisionScaleHandleKind
        {
            TopLeft = 0,
            Top = 1,
            TopRight = 2,
            Right = 3,
            BottomRight = 4,
            Bottom = 5,
            BottomLeft = 6,
            Left = 7
        }

        private readonly record struct CollisionGizmoHit(CollisionTransformKind Kind, CollisionScaleHandle? ScaleHandle);

        private sealed class TileCollisionTransformDrag
        {
            public required CollisionTransformKind Kind { get; init; }
            public required List<TileCollisionSelection> Selections { get; init; }
            public required List<List<GodotVector2>> StartPoints { get; init; }
            public required GodotVector2 PivotWorld { get; init; }
            public required GodotVector2 StartMouseWorld { get; init; }
            public bool Moved { get; set; }
            public CollisionScaleHandle? ScaleHandle { get; init; }
            public GodotVector2 StartHandleWorld { get; init; }
        }

        private sealed class LayoutCollisionTransformDrag
        {
            public required CollisionTransformKind Kind { get; init; }
            public required int PolygonIndex { get; init; }
            public required List<GodotVector2> StartPoints { get; init; }
            public required GodotVector2 PivotWorld { get; init; }
            public required GodotVector2 StartMouseWorld { get; init; }
            public bool Moved { get; set; }
            public CollisionScaleHandle? ScaleHandle { get; init; }
            public GodotVector2 StartHandleWorld { get; init; }
        }

        private TileCollisionTransformDrag? StartTransformDrag(CollisionGizmoHit hit, Point mouseScreen)
        {
            if (_tileCollisionSelections.Count == 0 || _map == null)
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var bounds = ComputeSelectionBoundsScreen(scale, offset);
            var rect = new RectangleF(bounds.minX, bounds.minY, bounds.width, bounds.height);
            var pivotScreen = new Point((int)MathF.Round(rect.X + rect.Width / 2f), (int)MathF.Round(rect.Y + rect.Height / 2f));
            var pivotWorld2 = ScreenToWorld(pivotScreen);
            var pivotWorld = new GodotVector2(pivotWorld2.x, pivotWorld2.y);

            var mouseWorld2 = ScreenToWorld(mouseScreen);
            var startMouseWorld = new GodotVector2(mouseWorld2.x, mouseWorld2.y);

            var startHandleWorld = startMouseWorld;
            if (hit.Kind == CollisionTransformKind.Scale && hit.ScaleHandle != null)
            {
                var hs = GetScaleHandleRects(rect)[(int)hit.ScaleHandle.Value.Kind];
                var center = new Point((int)MathF.Round(hs.X + hs.Width / 2f), (int)MathF.Round(hs.Y + hs.Height / 2f));
                var w = ScreenToWorld(center);
                startHandleWorld = new GodotVector2(w.x, w.y);
            }
            else if (hit.Kind == CollisionTransformKind.Rotate)
            {
                var rotateCenter = new Point((int)MathF.Round(rect.X + rect.Width / 2f), (int)MathF.Round(rect.Y - RotateHandleOffset * scale));
                var w = ScreenToWorld(rotateCenter);
                startHandleWorld = new GodotVector2(w.x, w.y);
            }

            return new TileCollisionTransformDrag
            {
                Kind = hit.Kind,
                Selections = _tileCollisionSelections.ToList(),
                StartPoints = _tileCollisionSelections.Select(s => s.Points.ToList()).ToList(),
                PivotWorld = pivotWorld,
                StartMouseWorld = startMouseWorld,
                ScaleHandle = hit.ScaleHandle,
                StartHandleWorld = startHandleWorld
            };
        }

        private LayoutCollisionTransformDrag? StartLayoutTransformDrag(CollisionGizmoHit hit, Point mouseScreen)
        {
            if (_layoutCollision == null || _map == null)
                return null;
            if (_layoutCollision.Polygons.Count == 0)
                return null;
            if (_layoutPolySelectedIndex < 0 || _layoutPolySelectedIndex >= _layoutCollision.Polygons.Count)
                return null;
            var pts = _layoutCollision.Polygons[_layoutPolySelectedIndex];
            if (pts == null || pts.Count < 3)
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var bounds = ComputeLayoutSelectionBoundsScreen(scale, offset);
            var rect = new RectangleF(bounds.minX, bounds.minY, bounds.width, bounds.height);
            var pivotScreen = new Point((int)MathF.Round(rect.X + rect.Width / 2f), (int)MathF.Round(rect.Y + rect.Height / 2f));
            var pivotWorld2 = ScreenToWorld(pivotScreen);
            var pivotWorld = new GodotVector2(pivotWorld2.x, pivotWorld2.y);

            var mouseWorld2 = ScreenToWorld(mouseScreen);
            var startMouseWorld = new GodotVector2(mouseWorld2.x, mouseWorld2.y);

            var startHandleWorld = startMouseWorld;
            if (hit.Kind == CollisionTransformKind.Scale && hit.ScaleHandle != null)
            {
                var hs = GetScaleHandleRects(rect)[(int)hit.ScaleHandle.Value.Kind];
                var center = new Point((int)MathF.Round(hs.X + hs.Width / 2f), (int)MathF.Round(hs.Y + hs.Height / 2f));
                var w = ScreenToWorld(center);
                startHandleWorld = new GodotVector2(w.x, w.y);
            }
            else if (hit.Kind == CollisionTransformKind.Rotate)
            {
                var rotateCenter = new Point((int)MathF.Round(rect.X + rect.Width / 2f), (int)MathF.Round(rect.Y - RotateHandleOffset * scale));
                var w = ScreenToWorld(rotateCenter);
                startHandleWorld = new GodotVector2(w.x, w.y);
            }

            return new LayoutCollisionTransformDrag
            {
                Kind = hit.Kind,
                PolygonIndex = _layoutPolySelectedIndex,
                StartPoints = pts.Select(p => new GodotVector2(p.X, p.Y)).ToList(),
                PivotWorld = pivotWorld,
                StartMouseWorld = startMouseWorld,
                ScaleHandle = hit.ScaleHandle,
                StartHandleWorld = startHandleWorld
            };
        }

        private CollisionGizmoHit? HitTestCollisionGizmo(Point mouse)
        {
            if (_tileCollisionSelections.Count == 0 || _map == null || string.IsNullOrWhiteSpace(_godotRoot))
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var bounds = ComputeSelectionBoundsScreen(scale, offset);
            var rect = new RectangleF(bounds.minX, bounds.minY, bounds.width, bounds.height);

            if (_toolMode == CollisionToolMode.Move)
            {
                for (var s = _tileCollisionSelections.Count - 1; s >= 0; s--)
                {
                    var sel = _tileCollisionSelections[s];
                    var poly = new PointF[sel.Points.Count];
                    for (var i = 0; i < sel.Points.Count; i++)
                    {
                        var wp = ToTileWorld(sel.CellX, sel.CellY, sel.Points[i]);
                        poly[i] = WorldToScreen(wp.x, wp.y, scale, offset);
                    }
                    if (PointInPolygon(poly, mouse))
                        return new CollisionGizmoHit(CollisionTransformKind.Move, null);
                }
                return null;
            }

            if (_toolMode == CollisionToolMode.Scale)
            {
                var handles = GetScaleHandleRects(rect);
                for (var i = 0; i < handles.Count; i++)
                {
                    if (!handles[i].Contains(mouse))
                        continue;
                    var kind = (CollisionScaleHandleKind)i;
                    var affectsX = kind is CollisionScaleHandleKind.Left or CollisionScaleHandleKind.Right or CollisionScaleHandleKind.TopLeft or CollisionScaleHandleKind.TopRight or CollisionScaleHandleKind.BottomLeft or CollisionScaleHandleKind.BottomRight;
                    var affectsY = kind is CollisionScaleHandleKind.Top or CollisionScaleHandleKind.Bottom or CollisionScaleHandleKind.TopLeft or CollisionScaleHandleKind.TopRight or CollisionScaleHandleKind.BottomLeft or CollisionScaleHandleKind.BottomRight;
                    return new CollisionGizmoHit(CollisionTransformKind.Scale, new CollisionScaleHandle(kind, affectsX, affectsY));
                }
                return null;
            }

            if (_toolMode == CollisionToolMode.Rotate)
            {
                var rotateCenter = new PointF(rect.X + rect.Width / 2f, rect.Y - RotateHandleOffset * scale);
                var rr = RotateHandleRadius * scale;
                var dx = mouse.X - rotateCenter.X;
                var dy = mouse.Y - rotateCenter.Y;
                if (dx * dx + dy * dy <= rr * rr)
                    return new CollisionGizmoHit(CollisionTransformKind.Rotate, null);
                return null;
            }

            return null;
        }

        private CollisionGizmoHit? HitTestLayoutCollisionGizmo(Point mouse)
        {
            if (_layoutCollision == null || _map == null)
                return null;
            if (_layoutCollision.Polygons.Count == 0)
                return null;
            if (_layoutPolySelectedIndex < 0 || _layoutPolySelectedIndex >= _layoutCollision.Polygons.Count)
                return null;

            var roomW = Math.Max(1, _map.RoomWidth) * TileSize;
            var roomH = Math.Max(1, _map.RoomHeight) * TileSize;
            var scale = ComputeScale(roomW, roomH);
            var offset = ComputeOffset(roomW, roomH, scale);

            var bounds = ComputeLayoutSelectionBoundsScreen(scale, offset);
            var rect = new RectangleF(bounds.minX, bounds.minY, bounds.width, bounds.height);

            var pts = _layoutCollision.Polygons[_layoutPolySelectedIndex];
            if (pts == null || pts.Count < 3)
                return null;

            if (_toolMode == CollisionToolMode.Move)
            {
                var poly = new PointF[pts.Count];
                for (var i = 0; i < pts.Count; i++)
                    poly[i] = WorldToScreen(pts[i].X, pts[i].Y, scale, offset);
                return PointInPolygon(poly, mouse) ? new CollisionGizmoHit(CollisionTransformKind.Move, null) : null;
            }

            if (_toolMode == CollisionToolMode.Scale)
            {
                var handles = GetScaleHandleRects(rect);
                for (var i = 0; i < handles.Count; i++)
                {
                    if (!handles[i].Contains(mouse))
                        continue;
                    var kind = (CollisionScaleHandleKind)i;
                    var affectsX = kind is CollisionScaleHandleKind.Left or CollisionScaleHandleKind.Right or CollisionScaleHandleKind.TopLeft or CollisionScaleHandleKind.TopRight or CollisionScaleHandleKind.BottomLeft or CollisionScaleHandleKind.BottomRight;
                    var affectsY = kind is CollisionScaleHandleKind.Top or CollisionScaleHandleKind.Bottom or CollisionScaleHandleKind.TopLeft or CollisionScaleHandleKind.TopRight or CollisionScaleHandleKind.BottomLeft or CollisionScaleHandleKind.BottomRight;
                    return new CollisionGizmoHit(CollisionTransformKind.Scale, new CollisionScaleHandle(kind, affectsX, affectsY));
                }
                return null;
            }

            if (_toolMode == CollisionToolMode.Rotate)
            {
                var rotateCenter = new PointF(rect.X + rect.Width / 2f, rect.Y - RotateHandleOffset * scale);
                var rr = RotateHandleRadius * scale;
                var dx = mouse.X - rotateCenter.X;
                var dy = mouse.Y - rotateCenter.Y;
                return dx * dx + dy * dy <= rr * rr ? new CollisionGizmoHit(CollisionTransformKind.Rotate, null) : null;
            }

            return null;
        }

        private static (GodotVector2 pivot, (float minX, float minY, float maxX, float maxY) bounds) ComputeBoundsLocal(List<GodotVector2> pts)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var p in pts)
            {
                minX = MathF.Min(minX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxX = MathF.Max(maxX, p.X);
                maxY = MathF.Max(maxY, p.Y);
            }
            if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
                return (new GodotVector2(0, 0), (0, 0, 0, 0));
            var pivot = new GodotVector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            return (pivot, (minX, minY, maxX, maxY));
        }

        private static GodotVector2 GetHandleLocalFromBounds((float minX, float minY, float maxX, float maxY) b, CollisionScaleHandleKind kind)
        {
            return kind switch
            {
                CollisionScaleHandleKind.TopLeft => new GodotVector2(b.minX, b.minY),
                CollisionScaleHandleKind.Top => new GodotVector2((b.minX + b.maxX) / 2f, b.minY),
                CollisionScaleHandleKind.TopRight => new GodotVector2(b.maxX, b.minY),
                CollisionScaleHandleKind.Right => new GodotVector2(b.maxX, (b.minY + b.maxY) / 2f),
                CollisionScaleHandleKind.BottomRight => new GodotVector2(b.maxX, b.maxY),
                CollisionScaleHandleKind.Bottom => new GodotVector2((b.minX + b.maxX) / 2f, b.maxY),
                CollisionScaleHandleKind.BottomLeft => new GodotVector2(b.minX, b.maxY),
                CollisionScaleHandleKind.Left => new GodotVector2(b.minX, (b.minY + b.maxY) / 2f),
                _ => new GodotVector2(b.maxX, b.maxY)
            };
        }

        private static GodotVector2 GetRotateHandleLocalFromBounds((float minX, float minY, float maxX, float maxY) b)
        {
            return new GodotVector2((b.minX + b.maxX) / 2f, b.minY - RotateHandleOffset);
        }

        private static void ApplyMove(List<GodotVector2> target, List<GodotVector2> start, GodotVector2 delta)
        {
            target.Clear();
            for (var i = 0; i < start.Count; i++)
                target.Add(new GodotVector2(start[i].X + delta.X, start[i].Y + delta.Y));
        }

        private static void ApplyRotate(List<GodotVector2> target, List<GodotVector2> start, GodotVector2 pivot, float radians)
        {
            var c = MathF.Cos(radians);
            var s = MathF.Sin(radians);
            target.Clear();
            for (var i = 0; i < start.Count; i++)
            {
                var x = start[i].X - pivot.X;
                var y = start[i].Y - pivot.Y;
                var rx = x * c - y * s;
                var ry = x * s + y * c;
                target.Add(new GodotVector2(rx + pivot.X, ry + pivot.Y));
            }
        }

        private static void ApplyScale(List<GodotVector2> target, List<GodotVector2> start, GodotVector2 pivot, float sx, float sy)
        {
            target.Clear();
            for (var i = 0; i < start.Count; i++)
            {
                var x = start[i].X - pivot.X;
                var y = start[i].Y - pivot.Y;
                target.Add(new GodotVector2(x * sx + pivot.X, y * sy + pivot.Y));
            }
        }
    }

}
