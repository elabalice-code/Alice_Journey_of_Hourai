using System;
using System.Collections.Generic;
using System.Windows.Forms;
using MapEditorTool.Executor;
using MapEditorTool.ViewModel;

namespace MapEditorTool.UI
{
    public partial class Form1 : Form
    {
        private readonly DeveloperCommentExecutor _developerCommentExecutor;
        private readonly MapEditorShellViewModel _viewModel;
        private bool _isDeveloperCommentBoxOpen;
        private bool _isApplyingSnapshot;

        public Form1()
        {
            InitializeComponent();
            _viewModel = MapEditorShellViewModel.CreateShellDefaults();

            _developerCommentExecutor = new DeveloperCommentExecutor(AppDomain.CurrentDomain.BaseDirectory);

            BuildMapEditorShell();
            BuildDeveloperCommentModeToggle();
            WireDeveloperInteractionHandlers(this);
            WireDeveloperInteractionHandlers(mainMenu);
            WireDeveloperInteractionHandlers(mapTools);
            WireDeveloperInteractionHandlers(mapListContextMenu);

            ApplySnapshotToUi();
        }

        private void BuildMapEditorShell()
        {
            Text = "MapEditorTool - UI Prototype Shell";
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            BuildMenu();
            BuildMapTools();
            BuildMapList();
            BuildTabs();
        }

        private void BuildDeveloperCommentModeToggle()
        {
            developerCommentModeCheckBox.Checked = _viewModel.Snapshot.DeveloperCommentModeEnabled;
            developerCommentModeCheckBox.CheckedChanged += DeveloperCommentModeCheckBoxCheckedChanged;
        }

        private void BuildMenu()
        {
            var file = CreateMenuItem("文件", "menu.file");
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("New Project", "menu.file.newProject", Keys.Control | Keys.N),
                CreateMenuItem("Open Project...", "menu.file.openProject", Keys.Control | Keys.O),
                CreateMenuItem("Save Project", "menu.file.saveProject", Keys.Control | Keys.S),
                CreateMenuItem("Save Project As...", "menu.file.saveProjectAs", Keys.Control | Keys.Shift | Keys.S),
                new ToolStripSeparator(),
                CreateMenuItem("从 Godot 重新加载...", "menu.file.importFromGodot"),
                new ToolStripSeparator(),
                CreateMenuItem("退出", "menu.file.exit")
            });

            var edit = CreateMenuItem("编辑", "menu.edit");
            edit.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("撤回", "menu.edit.undo", Keys.Control | Keys.Z),
                CreateMenuItem("重做", "menu.edit.redo", Keys.Control | Keys.Y)
            });

            var view = CreateMenuItem("视图", "menu.view");
            view.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("地图视图", "menu.view.map"),
                CreateMenuItem("碰撞视图", "menu.view.collision"),
                CreateMenuItem("连接视图", "menu.view.links")
            });

            var review = CreateMenuItem("开发者反馈", "menu.developer");
            review.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("查看评论日志", "menu.developer.openLog"),
                CreateMenuItem("清空状态提示", "menu.developer.clearStatus")
            });

            mainMenu.Items.AddRange(new ToolStripItem[] { file, edit, view, review });
        }

        private void BuildMapTools()
        {
            var viewMode = new ToolStripComboBox
            {
                Name = "viewModeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };
            viewMode.Items.AddRange(new object[] { "地图", "碰撞" });
            viewMode.SelectedIndex = 0;

            var collisionEditMode = new ToolStripComboBox
            {
                Name = "collisionEditModeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140
            };
            collisionEditMode.Items.AddRange(new object[] { "TileSet碰撞", "碰撞布局" });
            collisionEditMode.SelectedIndex = 1;

            var collisionMode = new ToolStripComboBox
            {
                Name = "collisionModeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140
            };
            collisionMode.Items.AddRange(new object[] { "Tile前景", "前景纹理" });
            collisionMode.SelectedIndex = 0;

            var collisionTarget = new ToolStripComboBox
            {
                Name = "collisionTargetCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160
            };
            collisionTarget.Items.AddRange(new object[] { "Tile碰撞文件", "前景纹理碰撞文件" });
            collisionTarget.SelectedIndex = 0;

            mapTools.Items.Add(new ToolStripLabel("视图") { Name = "viewModeLabel" });
            mapTools.Items.Add(viewMode);
            mapTools.Items.Add(new ToolStripSeparator());
            mapTools.Items.Add(new ToolStripLabel("编辑") { Name = "collisionEditModeLabel" });
            mapTools.Items.Add(collisionEditMode);
            mapTools.Items.Add(new ToolStripLabel("生效") { Name = "collisionModeLabel" });
            mapTools.Items.Add(collisionMode);
            mapTools.Items.Add(new ToolStripSeparator());
            mapTools.Items.Add(new ToolStripLabel("工具") { Name = "toolModeLabel" });
            mapTools.Items.Add(CreateToolButton("选择(S)", "tool.select", true));
            mapTools.Items.Add(CreateToolButton("顶点(Q)", "tool.vertex", true));
            mapTools.Items.Add(CreateToolButton("移动(W)", "tool.move", true));
            mapTools.Items.Add(CreateToolButton("旋转(E)", "tool.rotate", true));
            mapTools.Items.Add(CreateToolButton("拉伸(R)", "tool.scale", true));
            mapTools.Items.Add(new ToolStripSeparator());
            mapTools.Items.Add(CreateToolButton("添加方形(A)", "tool.addSquareCollision", true));
            mapTools.Items.Add(CreateToolButton("移除碰撞(D)", "tool.removeCollision", true));
            mapTools.Items.Add(new ToolStripLabel("碰撞编辑") { Name = "collisionTargetLabel" });
            mapTools.Items.Add(collisionTarget);
            mapTools.Items.Add(CreateToolButton("初始化", "tool.collisionInitialize", false));
            mapTools.Items.Add(CreateToolButton("加载", "tool.collisionLoad", false));
            mapTools.Items.Add(CreateToolButton("保存", "tool.collisionSave", false));
        }

        private void BuildMapList()
        {
            mapListContextMenu.Items.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("新增地图", "context.maps.add"),
                CreateMenuItem("删除地图", "context.maps.delete"),
                new ToolStripSeparator(),
                CreateMenuItem("设为置顶", "context.maps.pin")
            });

            mapsList.ContextMenuStrip = mapListContextMenu;
        }

        private void BuildTabs()
        {
            mapPlaceholder.Text =
                "Map canvas placeholder\r\n\r\n" +
                "This shell keeps the old MapEditor layout visible while MapEditorTool is rebuilt.";

            linksPlaceholder.Text =
                "Links graph placeholder\r\n\r\n" +
                "Click any item, menu, tab, toolbar button, or panel to submit developer feedback.";
        }

        private ToolStripMenuItem CreateMenuItem(string text, string name)
        {
            return CreateMenuItem(text, name, Keys.None);
        }

        private ToolStripMenuItem CreateMenuItem(string text, string name, Keys shortcut)
        {
            return new ToolStripMenuItem(text)
            {
                Name = name,
                ShortcutKeys = shortcut,
                ToolTipText = "Prototype shell action: " + text
            };
        }

        private ToolStripButton CreateToolButton(string text, string name, bool checkOnClick)
        {
            return new ToolStripButton(text)
            {
                Name = name,
                CheckOnClick = checkOnClick,
                ToolTipText = "Prototype shell tool: " + text
            };
        }

        private void WireDeveloperInteractionHandlers(Control control)
        {
            control.Click -= HandleDeveloperInteraction;
            control.Click += HandleDeveloperInteraction;

            for (var i = 0; i < control.Controls.Count; i++)
                WireDeveloperInteractionHandlers(control.Controls[i]);
        }

        private void WireDeveloperInteractionHandlers(ToolStrip toolStrip)
        {
            for (var i = 0; i < toolStrip.Items.Count; i++)
                WireDeveloperInteractionHandlers(toolStrip.Items[i]);
        }

        private void WireDeveloperInteractionHandlers(ToolStripItem item)
        {
            if (!(item is ToolStripSeparator))
            {
                item.Click -= HandleDeveloperInteraction;
                item.Click += HandleDeveloperInteraction;
            }

            var menuItem = item as ToolStripMenuItem;
            if (menuItem == null)
                return;

            for (var i = 0; i < menuItem.DropDownItems.Count; i++)
                WireDeveloperInteractionHandlers(menuItem.DropDownItems[i]);
        }

        private void HandleDeveloperInteraction(object sender, EventArgs e)
        {
            if (_isDeveloperCommentBoxOpen)
                return;

            if (ReferenceEquals(sender, developerCommentModeCheckBox) || ReferenceEquals(sender, developerCommentPanel))
                return;

            var source = DescribeSource(sender);
            var sourceSignal = UiSignalFactory.Click(source, e == null ? "Click" : e.GetType().Name);
            _viewModel.SubmitDeveloperCommentClick(sourceSignal);
            ApplySnapshotToUi();

            if (!_viewModel.Snapshot.DeveloperCommentOpenRequested)
                return;

            var commentSource = _viewModel.Snapshot.DeveloperCommentRequestSource;
            _viewModel.ConsumeDeveloperCommentOpenRequest();
            ApplySnapshotToUi();

            using (var box = new DeveloperCommentBox(commentSource))
            {
                _isDeveloperCommentBoxOpen = true;
                try
                {
                    if (box.ShowDialog(this) != DialogResult.OK)
                    {
                        statusText.Text = "Developer comment canceled: " + commentSource;
                        return;
                    }
                }
                finally
                {
                    _isDeveloperCommentBoxOpen = false;
                }

                _developerCommentExecutor.WriteComment(commentSource, box.CommentText);
                statusText.Text = "Developer comment logged: " + commentSource;
            }
        }

        private void DeveloperCommentModeCheckBoxCheckedChanged(object sender, EventArgs e)
        {
            if (_isApplyingSnapshot)
                return;

            var source = DescribeSource(sender);
            _viewModel.SetDeveloperCommentMode(
                developerCommentModeCheckBox.Checked,
                UiSignalFactory.CheckedChanged(source));
            ApplySnapshotToUi();
        }

        private void ApplySnapshotToUi()
        {
            var snapshot = _viewModel.Snapshot;
            _isApplyingSnapshot = true;
            try
            {
                if (developerCommentModeCheckBox.Checked != snapshot.DeveloperCommentModeEnabled)
                    developerCommentModeCheckBox.Checked = snapshot.DeveloperCommentModeEnabled;

                ReplaceItems(mapsList, snapshot.MapNames);
                ReplaceItems(linksList, snapshot.LinkNames);
                mapPropertyGrid.SelectedObject = snapshot.MapState;
                linkPropertyGrid.SelectedObject = snapshot.LinkState;
                statusText.Text = snapshot.StatusText;
            }
            finally
            {
                _isApplyingSnapshot = false;
            }
        }

        private static void ReplaceItems(ListBox listBox, string[] items)
        {
            var selectedIndex = listBox.SelectedIndex;
            listBox.BeginUpdate();
            try
            {
                listBox.Items.Clear();
                if (items != null && items.Length > 0)
                    listBox.Items.AddRange(items);

                if (selectedIndex >= 0 && selectedIndex < listBox.Items.Count)
                    listBox.SelectedIndex = selectedIndex;
            }
            finally
            {
                listBox.EndUpdate();
            }
        }

        private string DescribeSource(object sender)
        {
            var item = sender as ToolStripItem;
            if (item != null)
                return DescribeToolStripItem(item);

            var control = sender as Control;
            if (control != null)
                return DescribeControl(control);

            return sender == null ? "(unknown sender)" : sender.GetType().FullName;
        }

        private string DescribeToolStripItem(ToolStripItem item)
        {
            var parts = new List<string>();
            var current = item;

            while (current != null)
            {
                var ownerMenu = current.OwnerItem as ToolStripMenuItem;
                var index = GetToolStripItemIndex(current);
                parts.Insert(0, FormatToolStripItem(current, index));
                current = ownerMenu;
            }

            var ownerName = item.Owner == null ? "no-owner" : item.Owner.Name;
            return "ToolStripItem owner=" + ownerName + " depth=" + (parts.Count - 1) + " path=" + string.Join(" > ", parts.ToArray());
        }

        private int GetToolStripItemIndex(ToolStripItem item)
        {
            if (item.Owner == null)
                return -1;

            for (var i = 0; i < item.Owner.Items.Count; i++)
            {
                if (ReferenceEquals(item.Owner.Items[i], item))
                    return i;
            }

            return -1;
        }

        private string FormatToolStripItem(ToolStripItem item, int index)
        {
            var text = string.IsNullOrWhiteSpace(item.Text) ? "(no text)" : item.Text;
            var name = string.IsNullOrWhiteSpace(item.Name) ? "(no name)" : item.Name;
            return string.Format("[{0}] {1} \"{2}\" ({3})", index, item.GetType().Name, text, name);
        }

        private string DescribeControl(Control control)
        {
            var parts = new List<string>();
            var current = control;

            while (current != null)
            {
                var index = current.Parent == null ? -1 : current.Parent.Controls.GetChildIndex(current);
                parts.Insert(0, FormatControl(current, index));
                current = current.Parent;
            }

            return "Control path=" + string.Join(" > ", parts.ToArray());
        }

        private string FormatControl(Control control, int index)
        {
            var text = string.IsNullOrWhiteSpace(control.Text) ? "(no text)" : control.Text;
            var name = string.IsNullOrWhiteSpace(control.Name) ? "(no name)" : control.Name;
            return string.Format("[{0}] {1} \"{2}\" ({3})", index, control.GetType().Name, text, name);
        }
    }
}
