using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using MapEditorTool.Executor;
using MapEditorTool.Executor.CollisionLayout;
using MapEditorTool.Executor.ForegroundTextureCollision;
using MapEditorTool.Executor.GameSettings;
using MapEditorTool.Executor.MapApply;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Executor.MapDeletion;
using MapEditorTool.Executor.MapImport;
using MapEditorTool.Executor.MapReport;
using MapEditorTool.Executor.PortalAnimation;
using MapEditorTool.Executor.PortalEditing;
using MapEditorTool.Executor.ProjectFile;
using MapEditorTool.Executor.ResourcePath;
using MapEditorTool.Executor.RuntimeVerify;
using MapEditorTool.Models;
using MapEditorTool.ViewModel;

namespace MapEditorTool.UI
{
    public partial class Form1 : Form
    {
        private readonly DeveloperCommentExecutor _developerCommentExecutor;
        private readonly CollisionLayoutExecutor _collisionLayoutExecutor;
        private readonly ForegroundTextureCollisionExecutor _foregroundTextureCollisionExecutor;
        private readonly GameSettingsExecutor _gameSettingsExecutor;
        private readonly MapApplyExecutor _mapApplyExecutor;
        private readonly MapCreationExecutor _mapCreationExecutor;
        private readonly MapDeletionExecutor _mapDeletionExecutor;
        private readonly MapImportExecutor _mapImportExecutor;
        private readonly MapReportExecutor _mapReportExecutor;
        private readonly PortalAnimationExecutor _portalAnimationExecutor;
        private readonly PortalEditingExecutor _portalEditingExecutor;
        private readonly ProjectFileExecutor _projectFileExecutor;
        private readonly ResourcePathExecutor _resourcePathExecutor;
        private readonly RuntimeVerificationExecutor _runtimeVerificationExecutor;
        private readonly MapEditorShellViewModel _viewModel;
        private readonly MapPreviewCanvas _mapPreviewCanvas;
        private readonly LinksPreviewCanvas _linksPreviewCanvas;
        private MapEditorTool.Executor.MapCreation.CollisionLayoutData _currentCollisionOverlay;
        private CollisionLayoutTarget _currentCollisionOverlayTarget;
        private bool _showCollisionOverlay;
        private bool _isDeveloperCommentBoxOpen;
        private bool _isApplyingSnapshot;

        public Form1()
        {
            InitializeComponent();
            _viewModel = MapEditorShellViewModel.CreateShellDefaults();
            _mapPreviewCanvas = new MapPreviewCanvas();
            _linksPreviewCanvas = new LinksPreviewCanvas();

            _developerCommentExecutor = new DeveloperCommentExecutor(AppDomain.CurrentDomain.BaseDirectory);
            _collisionLayoutExecutor = new CollisionLayoutExecutor();
            _foregroundTextureCollisionExecutor = new ForegroundTextureCollisionExecutor(_collisionLayoutExecutor);
            _gameSettingsExecutor = new GameSettingsExecutor();
            _mapApplyExecutor = new MapApplyExecutor();
            _mapCreationExecutor = new MapCreationExecutor();
            _mapDeletionExecutor = new MapDeletionExecutor();
            _mapImportExecutor = new MapImportExecutor();
            _mapReportExecutor = new MapReportExecutor(_mapImportExecutor);
            _portalAnimationExecutor = new PortalAnimationExecutor();
            _portalEditingExecutor = new PortalEditingExecutor(_mapCreationExecutor, new Executor.ScenePatch.ScenePatchExecutor(), _portalAnimationExecutor);
            _projectFileExecutor = new ProjectFileExecutor();
            _resourcePathExecutor = new ResourcePathExecutor();
            _runtimeVerificationExecutor = new RuntimeVerificationExecutor(_mapImportExecutor);

            BuildMapEditorShell();
            RegisterPropertyGridEditors();
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
            NormalizePrototypeLabels();
            BuildTabs();
        }

        private void RegisterPropertyGridEditors()
        {
            RegisterResourcePathEditorProvider(typeof(MapDefinition));
            RegisterResourcePathEditorProvider(typeof(PlacedEntity));
            RegisterResourcePathEditorProvider(typeof(Obstacle));
            RegisterResourcePathEditorProvider(typeof(TileSkin));
            RegisterResourcePathEditorProvider(typeof(TileLayer));
            RegisterResourcePathEditorProvider(typeof(Portal));
            RegisterPortalEditorProviders();

            mapPropertyGrid.PropertyValueChanged += MapPropertyGridPropertyValueChanged;
        }

        private void RegisterPortalEditorProviders()
        {
            var context = new PortalEditorContext
            {
                ResolveGodotRoot = ResolveGodotRootForEditor,
                ResolveProject = delegate { return _viewModel.CurrentProject; },
                ResolveSelectedMap = delegate { return _viewModel.SelectedMap; },
                ReportStatus = delegate(string status)
                {
                    _viewModel.SetStatusText(status);
                    if (!_isApplyingSnapshot)
                        statusText.Text = status;
                },
                PortalEditingExecutor = _portalEditingExecutor
            };

            TypeDescriptor.AddProvider(new PortalEditorTypeDescriptionProvider(typeof(MapDefinition), context), typeof(MapDefinition));
            TypeDescriptor.AddProvider(new PortalEditorTypeDescriptionProvider(typeof(Portal), context), typeof(Portal));
        }

        private void RegisterResourcePathEditorProvider(Type type)
        {
            TypeDescriptor.AddProvider(
                new AutoResPathEditorTypeDescriptionProvider(
                    type,
                    delegate
                    {
                        return new GodotResPathEditor(
                            ResolveGodotRootForEditor,
                            delegate { return _viewModel.SelectedMap; },
                            _resourcePathExecutor);
                    }),
                type);
        }

        private void BuildDeveloperCommentModeToggle()
        {
            developerCommentModeCheckBox.Checked = _viewModel.Snapshot.DeveloperCommentModeEnabled;
            developerCommentModeCheckBox.CheckedChanged += DeveloperCommentModeCheckBoxCheckedChanged;
        }

        private void BuildMenu()
        {
            var file = CreateMenuItem("File", "menu.file");
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("New Project", "menu.file.newProject", Keys.Control | Keys.N),
                CreateMenuItem("Open Project...", "menu.file.openProject", Keys.Control | Keys.O),
                CreateMenuItem("Save Project", "menu.file.saveProject", Keys.Control | Keys.S),
                CreateMenuItem("Save Project As...", "menu.file.saveProjectAs", Keys.Control | Keys.Shift | Keys.S),
                CreateMenuItem("Apply Selected Map to Godot", "menu.file.applySelectedMapToGodot"),
                new ToolStripSeparator(),
                CreateMenuItem("Reload From Godot...", "menu.file.importFromGodot"),
                new ToolStripSeparator(),
                CreateMenuItem("Exit", "menu.file.exit")
            });

            var edit = CreateMenuItem("Edit", "menu.edit");
            edit.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("Undo", "menu.edit.undo", Keys.Control | Keys.Z),
                CreateMenuItem("Redo", "menu.edit.redo", Keys.Control | Keys.Y)
            });

            var view = CreateMenuItem("View", "menu.view");
            view.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("Map View", "menu.view.map"),
                CreateMenuItem("Collision View", "menu.view.collision"),
                CreateMenuItem("Links View", "menu.view.links")
            });

            var review = CreateMenuItem("Developer", "menu.developer");
            review.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("Open Comment Log", "menu.developer.openLog"),
                CreateMenuItem("Clear Status", "menu.developer.clearStatus")
            });

            review.DropDownItems.Insert(0, new ToolStripSeparator());
            review.DropDownItems.Insert(0, CreateMenuItem("Validate Current Import", "menu.developer.validateCurrentImport"));
            review.DropDownItems.Insert(0, CreateMenuItem("Runtime Verification Report", "menu.developer.runtimeVerify"));
            review.DropDownItems.Insert(0, CreateMenuItem("Portal Review Report", "menu.developer.portalReview"));
            review.DropDownItems.Insert(0, CreateMenuItem("Map Status Report", "menu.developer.mapStatus"));

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
            viewMode.Items.AddRange(new object[] { "Map", "Collision" });
            viewMode.SelectedIndex = 0;

            var collisionEditMode = new ToolStripComboBox
            {
                Name = "collisionEditModeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140
            };
            collisionEditMode.Items.AddRange(new object[] { "TileSet Collision", "Collision Layout" });
            collisionEditMode.SelectedIndex = 1;

            var collisionMode = new ToolStripComboBox
            {
                Name = "collisionModeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140
            };
            collisionMode.Items.AddRange(new object[] { "Tile Foreground", "Foreground Texture" });
            collisionMode.SelectedIndex = 0;

            var collisionTarget = new ToolStripComboBox
            {
                Name = "collisionTargetCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160
            };
            collisionTarget.Items.AddRange(new object[] { "Tile Collision File", "Foreground Texture Collision File" });
            collisionTarget.SelectedIndex = 0;
            viewMode.SelectedIndexChanged += MapToolSelectionChanged;
            collisionEditMode.SelectedIndexChanged += MapToolSelectionChanged;
            collisionMode.SelectedIndexChanged += MapToolSelectionChanged;
            collisionTarget.SelectedIndexChanged += MapToolSelectionChanged;

            mapTools.Items.Add(new ToolStripLabel("View") { Name = "viewModeLabel" });
            mapTools.Items.Add(viewMode);
            mapTools.Items.Add(new ToolStripSeparator());
            mapTools.Items.Add(new ToolStripLabel("Edit") { Name = "collisionEditModeLabel" });
            mapTools.Items.Add(collisionEditMode);
            mapTools.Items.Add(new ToolStripLabel("Active") { Name = "collisionModeLabel" });
            mapTools.Items.Add(collisionMode);
            mapTools.Items.Add(new ToolStripSeparator());
            mapTools.Items.Add(new ToolStripLabel("Tool") { Name = "toolModeLabel" });
            mapTools.Items.Add(CreateToolButton("Select(S)", "tool.select", true));
            mapTools.Items.Add(CreateToolButton("Vertex(Q)", "tool.vertex", true));
            mapTools.Items.Add(CreateToolButton("Move(W)", "tool.move", true));
            mapTools.Items.Add(CreateToolButton("Rotate(E)", "tool.rotate", true));
            mapTools.Items.Add(CreateToolButton("Scale(R)", "tool.scale", true));
            mapTools.Items.Add(new ToolStripSeparator());
            mapTools.Items.Add(CreateToolButton("Add Box(A)", "tool.addSquareCollision", true));
            mapTools.Items.Add(CreateToolButton("Remove Collision(D)", "tool.removeCollision", true));
            mapTools.Items.Add(new ToolStripLabel("Collision Target") { Name = "collisionTargetLabel" });
            mapTools.Items.Add(collisionTarget);
            mapTools.Items.Add(CreateToolButton("Initialize", "tool.collisionInitialize", false));
            mapTools.Items.Add(CreateToolButton("Load", "tool.collisionLoad", false));
            mapTools.Items.Add(CreateToolButton("Save", "tool.collisionSave", false));
        }

        private void BuildMapList()
        {
            mapListContextMenu.Items.AddRange(new ToolStripItem[]
            {
                CreateMenuItem("Add Map", "context.maps.add"),
                CreateMenuItem("Delete Map", "context.maps.delete"),
                new ToolStripSeparator(),
                CreateMenuItem("Pin Map", "context.maps.pin")
            });

            mapsList.ContextMenuStrip = mapListContextMenu;
            mapsList.SelectedIndexChanged += MapsListSelectedIndexChanged;
            linksList.SelectedIndexChanged += LinksListSelectedIndexChanged;
        }

        private void BuildTabs()
        {
            mapPlaceholder.Visible = false;
            mapCanvasHost.Controls.Add(_mapPreviewCanvas);
            _mapPreviewCanvas.Dock = DockStyle.Fill;
            _mapPreviewCanvas.BringToFront();

            linksPlaceholder.Text =
                string.Empty;
            linksPlaceholder.Visible = false;
            linksTabSplit.Panel1.Controls.Add(_linksPreviewCanvas);
            _linksPreviewCanvas.Dock = DockStyle.Fill;
            _linksPreviewCanvas.BringToFront();
        }

        private void NormalizePrototypeLabels()
        {
            Text = "MapEditorTool";
            mapTab.Text = "Map";
            linksTab.Text = "Links";

            SetToolStripItemText(mainMenu, "menu.file", "File");
            SetToolStripItemText(mainMenu, "menu.file.newProject", "New Project");
            SetToolStripItemText(mainMenu, "menu.file.openProject", "Open Project...");
            SetToolStripItemText(mainMenu, "menu.file.saveProject", "Save Project");
            SetToolStripItemText(mainMenu, "menu.file.saveProjectAs", "Save Project As...");
            SetToolStripItemText(mainMenu, "menu.file.applySelectedMapToGodot", "Apply Selected Map to Godot");
            SetToolStripItemText(mainMenu, "menu.file.importFromGodot", "Reload From Godot...");
            SetToolStripItemText(mainMenu, "menu.file.exit", "Exit");
            SetToolStripItemText(mainMenu, "menu.edit", "Edit");
            SetToolStripItemText(mainMenu, "menu.edit.undo", "Undo");
            SetToolStripItemText(mainMenu, "menu.edit.redo", "Redo");
            SetToolStripItemText(mainMenu, "menu.view", "View");
            SetToolStripItemText(mainMenu, "menu.view.map", "Map View");
            SetToolStripItemText(mainMenu, "menu.view.collision", "Collision View");
            SetToolStripItemText(mainMenu, "menu.view.links", "Links View");
            SetToolStripItemText(mainMenu, "menu.developer", "Developer");
            SetToolStripItemText(mainMenu, "menu.developer.runtimeVerify", "Runtime Verification Report");
            SetToolStripItemText(mainMenu, "menu.developer.validateCurrentImport", "Validate Current Import");
            SetToolStripItemText(mainMenu, "menu.developer.openLog", "Open Comment Log");
            SetToolStripItemText(mainMenu, "menu.developer.clearStatus", "Clear Status");

            SetToolStripComboItems(mapTools, "viewModeCombo", new object[] { "Map", "Collision" }, 0);
            SetToolStripComboItems(mapTools, "collisionEditModeCombo", new object[] { "TileSet Collision", "Collision Layout" }, 1);
            SetToolStripComboItems(mapTools, "collisionModeCombo", new object[] { "Tile Foreground", "Foreground Texture" }, 0);
            SetToolStripComboItems(mapTools, "collisionTargetCombo", new object[] { "Tile Collision File", "Foreground Texture Collision File" }, 0);

            SetToolStripItemText(mapTools, "viewModeLabel", "View");
            SetToolStripItemText(mapTools, "collisionEditModeLabel", "Edit");
            SetToolStripItemText(mapTools, "collisionModeLabel", "Active");
            SetToolStripItemText(mapTools, "toolModeLabel", "Tool");
            SetToolStripItemText(mapTools, "tool.select", "Select(S)");
            SetToolStripItemText(mapTools, "tool.vertex", "Vertex(Q)");
            SetToolStripItemText(mapTools, "tool.move", "Move(W)");
            SetToolStripItemText(mapTools, "tool.rotate", "Rotate(E)");
            SetToolStripItemText(mapTools, "tool.scale", "Scale(R)");
            SetToolStripItemText(mapTools, "tool.addSquareCollision", "Add Box(A)");
            SetToolStripItemText(mapTools, "tool.removeCollision", "Remove Collision(D)");
            SetToolStripItemText(mapTools, "collisionTargetLabel", "Collision Target");
            SetToolStripItemText(mapTools, "tool.collisionInitialize", "Initialize");
            SetToolStripItemText(mapTools, "tool.collisionLoad", "Load");
            SetToolStripItemText(mapTools, "tool.collisionSave", "Save");

            SetToolStripItemText(mapListContextMenu, "context.maps.add", "Add Map");
            SetToolStripItemText(mapListContextMenu, "context.maps.delete", "Delete Map");
            SetToolStripItemText(mapListContextMenu, "context.maps.pin", "Pin Map");
        }

        private static void SetToolStripComboItems(ToolStrip toolStrip, string itemName, object[] items, int selectedIndex)
        {
            var combo = FindToolStripItem(toolStrip.Items, itemName) as ToolStripComboBox;
            if (combo == null)
                return;

            combo.Items.Clear();
            combo.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < combo.Items.Count)
                combo.SelectedIndex = selectedIndex;
        }

        private static void SetToolStripItemText(ToolStrip toolStrip, string itemName, string text)
        {
            SetToolStripItemText(toolStrip.Items, itemName, text);
        }

        private static void SetToolStripItemText(ToolStripItemCollection items, string itemName, string text)
        {
            var item = FindToolStripItem(items, itemName);
            if (item != null)
                item.Text = text;
        }

        private static ToolStripItem FindToolStripItem(ToolStripItemCollection items, string itemName)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (string.Equals(item.Name, itemName, StringComparison.Ordinal))
                    return item;

                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null)
                    continue;

                var found = FindToolStripItem(menuItem.DropDownItems, itemName);
                if (found != null)
                    return found;
            }

            return null;
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
            ExecuteTerminalUiAction(sender, source);
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

        private void ExecuteTerminalUiAction(object sender, string sourceDescription)
        {
            var item = sender as ToolStripItem;
            if (item == null)
                return;

            if (string.Equals(item.Name, "menu.file.importFromGodot", StringComparison.Ordinal))
            {
                ImportFromGodot(sourceDescription);
            }
            else if (string.Equals(item.Name, "menu.file.applySelectedMapToGodot", StringComparison.Ordinal))
            {
                ApplySelectedMapToGodot();
            }
            else if (string.Equals(item.Name, "menu.file.newProject", StringComparison.Ordinal))
            {
                NewProject();
            }
            else if (string.Equals(item.Name, "menu.file.openProject", StringComparison.Ordinal))
            {
                OpenProject();
            }
            else if (string.Equals(item.Name, "menu.file.saveProject", StringComparison.Ordinal))
            {
                SaveProject();
            }
            else if (string.Equals(item.Name, "menu.file.saveProjectAs", StringComparison.Ordinal))
            {
                SaveProjectAs();
            }
            else if (string.Equals(item.Name, "menu.file.exit", StringComparison.Ordinal))
            {
                Close();
            }
            else if (string.Equals(item.Name, "menu.view.map", StringComparison.Ordinal))
            {
                SetViewModeComboIndex(0);
            }
            else if (string.Equals(item.Name, "menu.view.collision", StringComparison.Ordinal))
            {
                SetViewModeComboIndex(1);
            }
            else if (string.Equals(item.Name, "menu.view.links", StringComparison.Ordinal))
            {
                tabs.SelectedTab = linksTab;
            }
            else if (string.Equals(item.Name, "menu.developer.clearStatus", StringComparison.Ordinal))
            {
                _viewModel.SetStatusText("Status cleared.");
            }
            else if (string.Equals(item.Name, "menu.developer.openLog", StringComparison.Ordinal))
            {
                OpenDeveloperCommentLog();
            }
            else if (string.Equals(item.Name, "menu.developer.mapStatus", StringComparison.Ordinal))
            {
                ShowMapStatusReport();
            }
            else if (string.Equals(item.Name, "menu.developer.portalReview", StringComparison.Ordinal))
            {
                ShowPortalReviewReport();
            }
            else if (string.Equals(item.Name, "menu.developer.validateCurrentImport", StringComparison.Ordinal))
            {
                ShowValidateCurrentImportReport();
            }
            else if (string.Equals(item.Name, "menu.developer.runtimeVerify", StringComparison.Ordinal))
            {
                ShowRuntimeVerificationReport();
            }
            else if (string.Equals(item.Name, "context.maps.add", StringComparison.Ordinal))
            {
                AddMap();
            }
            else if (string.Equals(item.Name, "context.maps.delete", StringComparison.Ordinal))
            {
                DeleteSelectedMap();
            }
            else if (string.Equals(item.Name, "context.maps.pin", StringComparison.Ordinal))
            {
                PinSelectedMapAsStartingMap();
            }
            else if (string.Equals(item.Name, "tool.collisionInitialize", StringComparison.Ordinal))
            {
                InitializeSelectedMapCollision();
            }
            else if (string.Equals(item.Name, "tool.collisionLoad", StringComparison.Ordinal))
            {
                LoadSelectedMapCollision();
            }
            else if (string.Equals(item.Name, "tool.collisionSave", StringComparison.Ordinal))
            {
                SaveSelectedMapCollision();
            }
        }

        private void ImportFromGodot(string sourceDescription)
        {
            try
            {
                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var project = _mapImportExecutor.ImportFromGodotRoot(godotRoot);
                _viewModel.LoadImportedProject(project, godotRoot);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Import failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Import from Godot failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenDeveloperCommentLog()
        {
            try
            {
                var path = _developerCommentExecutor.OpenCommentLog();
                _viewModel.SetStatusText("Developer comment log opened: " + path);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Open comment log failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Open comment log failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void ApplySelectedMapToGodot()
        {
            try
            {
                var selected = _viewModel.SelectedMap;
                if (selected == null)
                {
                    _viewModel.SetStatusText("Apply skipped: no map is selected.");
                    MessageBox.Show(this, "Import or open a project, then select a map first.", "Apply Selected Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var result = _mapApplyExecutor.ApplyMapToGodot(godotRoot, selected);
                _viewModel.SetStatusText("Applied selected map to Godot: " + result.Summary);
                ShowReportDialog("Apply Selected Map to Godot", string.Join(Environment.NewLine, result.Steps.ToArray()));
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Apply selected map failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Apply selected map failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void AddMap()
        {
            try
            {
                var displayName = PromptText(this, "Add Map", "Map display name:", "NewMap");
                if (string.IsNullOrWhiteSpace(displayName))
                    return;

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var result = _mapCreationExecutor.CreateMap(godotRoot, displayName, _viewModel.CurrentProject.Maps);
                _viewModel.AddMap(result.CreatedMap);
                _viewModel.SetStatusText("Added map: " + result.Summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Add map failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Add map failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void DeleteSelectedMap()
        {
            try
            {
                var selected = _viewModel.SelectedMap;
                if (selected == null)
                {
                    _viewModel.SetStatusText("Delete skipped: no map is selected.");
                    MessageBox.Show(this, "Select a map first.", "Delete Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    this,
                    "Delete selected map resources and remove it from the project?\r\n\r\n" + selected.DisplayName + "\r\n" + selected.ScenePath,
                    "Delete Map",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.OK)
                    return;

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                if (_gameSettingsExecutor.IsStartingMap(godotRoot, selected.ScenePath))
                    _gameSettingsExecutor.WriteStartingMap(godotRoot, string.Empty);

                var result = _mapDeletionExecutor.DeleteMapResources(godotRoot, selected, true);
                _viewModel.RemoveSelectedMap();
                _viewModel.SetStatusText("Deleted map: " + result.Summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Delete map failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Delete map failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void PinSelectedMapAsStartingMap()
        {
            try
            {
                var selected = _viewModel.SelectedMap;
                if (selected == null)
                {
                    _viewModel.SetStatusText("Pin skipped: no map is selected.");
                    MessageBox.Show(this, "Select a map first.", "Pin Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (string.IsNullOrWhiteSpace(selected.ScenePath))
                {
                    _viewModel.SetStatusText("Pin skipped: selected map has no scene path.");
                    MessageBox.Show(this, "The selected map has no scene path.", "Pin Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var result = _gameSettingsExecutor.WriteStartingMap(godotRoot, selected.ScenePath);
                _viewModel.SetStatusText("Pinned starting map: " + result.Summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Pin map failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Pin map failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void InitializeSelectedMapCollision()
        {
            try
            {
                var selected = RequireSelectedMap("Initialize collision");
                if (selected == null)
                    return;

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var target = GetSelectedCollisionLayoutTarget();
                if (target == CollisionLayoutTarget.ForegroundTexture)
                {
                    var foreground = _foregroundTextureCollisionExecutor.BuildAndWriteLayout(godotRoot, selected);
                    SetCollisionOverlay(foreground.Layout, target, true);
                    _viewModel.SetStatusText("Initialized foreground texture collision: " + foreground.Summary);
                }
                else
                {
                    var layout = MapEditorTool.Executor.MapCreation.CollisionLayoutData.Create(selected.RoomWidth, selected.RoomHeight);
                    var saved = _collisionLayoutExecutor.SaveLayout(godotRoot, selected, target, layout);
                    SetCollisionOverlay(saved.Layout, target, true);
                    _viewModel.SetStatusText("Initialized tile collision: " + saved.Summary);
                }
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Initialize collision failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Initialize collision failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void LoadSelectedMapCollision()
        {
            try
            {
                var selected = RequireSelectedMap("Load collision");
                if (selected == null)
                    return;

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var target = GetSelectedCollisionLayoutTarget();
                var loaded = _collisionLayoutExecutor.LoadLayout(godotRoot, selected, target, false);
                SetCollisionOverlay(loaded.Layout, target, true);
                _viewModel.SetStatusText("Loaded collision layout: " + loaded.Summary);
                ShowReportDialog(
                    "Load Collision Layout",
                    "Target: " + target + Environment.NewLine +
                    "Path: " + loaded.CollisionResPath + Environment.NewLine +
                    "File: " + loaded.CollisionFilePath + Environment.NewLine +
                    "Room: " + loaded.Layout.RoomWidth + " x " + loaded.Layout.RoomHeight + Environment.NewLine +
                    "Solid cells: " + CountSolidCells(loaded.Layout.Solid) + Environment.NewLine +
                    "Polygons: " + (loaded.Layout.Polygons == null ? 0 : loaded.Layout.Polygons.Count) + Environment.NewLine +
                    loaded.Summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Load collision failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Load collision failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void SaveSelectedMapCollision()
        {
            try
            {
                var selected = RequireSelectedMap("Save collision");
                if (selected == null)
                    return;

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var target = GetSelectedCollisionLayoutTarget();
                var loaded = _collisionLayoutExecutor.LoadLayout(godotRoot, selected, target, true);
                var saved = _collisionLayoutExecutor.SaveLayout(godotRoot, selected, target, loaded.Layout);
                SetCollisionOverlay(saved.Layout, target, true);
                _viewModel.SetStatusText("Saved collision layout: " + saved.Summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Save collision failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Save collision failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void MapToolSelectionChanged(object sender, EventArgs e)
        {
            if (_isApplyingSnapshot)
                return;

            ApplyCollisionModeSelectionToMap();
            RefreshCollisionOverlayFromToolbar(false);
            ApplySnapshotToUi();
        }

        private void ApplyCollisionModeSelectionToMap()
        {
            var selected = _viewModel.SelectedMap;
            if (selected == null)
                return;

            var combo = FindToolStripItem(mapTools.Items, "collisionModeCombo") as ToolStripComboBox;
            if (combo == null)
                return;

            var mode = combo.SelectedIndex == 1 ? CollisionMode.ForegroundTexture : CollisionMode.TileForeground;
            if (selected.CollisionUsed == mode)
                return;

            selected.CollisionUsed = mode;
            _viewModel.MarkSelectedMapEdited("CollisionUsed");
        }

        private void RefreshCollisionOverlayFromToolbar(bool ensureDefaultPath)
        {
            if (!IsCollisionViewSelected())
            {
                ClearCollisionOverlay();
                return;
            }

            var selected = _viewModel.SelectedMap;
            if (selected == null)
            {
                ClearCollisionOverlay();
                return;
            }

            try
            {
                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var target = GetSelectedCollisionLayoutTarget();
                var loaded = _collisionLayoutExecutor.LoadLayout(godotRoot, selected, target, ensureDefaultPath);
                SetCollisionOverlay(loaded.Layout, target, true);
            }
            catch
            {
                ClearCollisionOverlay();
            }
        }

        private void SetViewModeComboIndex(int index)
        {
            var combo = FindToolStripItem(mapTools.Items, "viewModeCombo") as ToolStripComboBox;
            if (combo == null)
                return;
            if (index < 0 || index >= combo.Items.Count)
                return;

            combo.SelectedIndex = index;
            tabs.SelectedTab = mapTab;
            RefreshCollisionOverlayFromToolbar(false);
        }

        private bool IsCollisionViewSelected()
        {
            var combo = FindToolStripItem(mapTools.Items, "viewModeCombo") as ToolStripComboBox;
            return combo != null && combo.SelectedIndex == 1;
        }

        private void SetCollisionOverlay(MapEditorTool.Executor.MapCreation.CollisionLayoutData layout, CollisionLayoutTarget target, bool visible)
        {
            _currentCollisionOverlay = layout;
            _currentCollisionOverlayTarget = target;
            _showCollisionOverlay = visible;
        }

        private void ClearCollisionOverlay()
        {
            _currentCollisionOverlay = null;
            _showCollisionOverlay = false;
        }

        private MapEditorTool.Models.MapDefinition RequireSelectedMap(string actionName)
        {
            var selected = _viewModel.SelectedMap;
            if (selected != null)
                return selected;

            _viewModel.SetStatusText(actionName + " skipped: no map is selected.");
            MessageBox.Show(this, "Select a map first.", actionName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        private CollisionLayoutTarget GetSelectedCollisionLayoutTarget()
        {
            var combo = FindToolStripItem(mapTools.Items, "collisionTargetCombo") as ToolStripComboBox;
            if (combo != null && combo.SelectedIndex == 1)
                return CollisionLayoutTarget.ForegroundTexture;

            return CollisionLayoutTarget.Tile;
        }

        private static int CountSolidCells(bool[] solid)
        {
            if (solid == null)
                return 0;

            var count = 0;
            for (var i = 0; i < solid.Length; i++)
            {
                if (solid[i])
                    count++;
            }

            return count;
        }

        private void NewProject()
        {
            _viewModel.CreateNewProject();
        }

        private void OpenProject()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "MapEditor project (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Open MapEditorTool Project";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    var project = _projectFileExecutor.LoadProject(dialog.FileName);
                    _viewModel.LoadProjectFile(project, dialog.FileName);
                }
                catch (Exception ex)
                {
                    _viewModel.SetStatusText("Open project failed: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "Open project failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveProject()
        {
            if (string.IsNullOrWhiteSpace(_viewModel.Snapshot.CurrentProjectPath))
            {
                SaveProjectAs();
                return;
            }

            SaveProjectToPath(_viewModel.Snapshot.CurrentProjectPath);
        }

        private void SaveProjectAs()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "MapEditor project (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Save MapEditorTool Project";
                dialog.FileName = string.IsNullOrWhiteSpace(_viewModel.Snapshot.CurrentProjectPath)
                    ? "map_project.json"
                    : Path.GetFileName(_viewModel.Snapshot.CurrentProjectPath);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                SaveProjectToPath(dialog.FileName);
            }
        }

        private void SaveProjectToPath(string filePath)
        {
            try
            {
                _projectFileExecutor.SaveProject(filePath, _viewModel.CurrentProject);
                _viewModel.MarkProjectSaved(filePath);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Save project failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Save project failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowMapStatusReport()
        {
            try
            {
                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var status = _mapReportExecutor.BuildStatus(godotRoot);
                var summary = _mapReportExecutor.FormatStatusSummary(status);
                _viewModel.SetReportSummary("Map status report", summary);
                ShowReportDialog("Map Status Report", summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Map status report failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Map status report failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowPortalReviewReport()
        {
            try
            {
                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var review = _mapReportExecutor.BuildPortalReview(godotRoot);
                var summary = _mapReportExecutor.FormatPortalReviewSummary(review);
                _viewModel.SetReportSummary("Portal review report", summary);
                ShowReportDialog("Portal Review Report", summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Portal review report failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Portal review report failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowValidateCurrentImportReport()
        {
            try
            {
                if (!_viewModel.HasCurrentProject)
                {
                    _viewModel.SetStatusText("Validate skipped: import from Godot first.");
                    MessageBox.Show(this, "Import from Godot first, then run validation.", "Validate Current Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var report = _mapReportExecutor.ValidateProjectAgainstGodot(godotRoot, "Current ViewModel import", _viewModel.CurrentProject);
                var summary = _mapReportExecutor.FormatValidationSummary(report);
                _viewModel.SetReportSummary("Validation report", summary);
                ShowReportDialog("Validate Current Import", summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Validation report failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Validation report failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowRuntimeVerificationReport()
        {
            try
            {
                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var report = _runtimeVerificationExecutor.BuildRuntimeVerificationReport(godotRoot);
                var summary = _runtimeVerificationExecutor.FormatRuntimeVerificationSummary(report);
                _viewModel.SetReportSummary("Runtime verification report", summary);
                ShowReportDialog("Runtime Verification Report", summary);
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Runtime verification failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Runtime verification failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowReportDialog(string title, string summary)
        {
            using (var dialog = new Form())
            using (var textBox = new TextBox())
            using (var closeButton = new Button())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 820;
                dialog.Height = 600;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = true;
                dialog.ShowIcon = false;

                textBox.Multiline = true;
                textBox.ReadOnly = true;
                textBox.ScrollBars = ScrollBars.Both;
                textBox.WordWrap = false;
                textBox.Dock = DockStyle.Fill;
                textBox.Text = summary ?? string.Empty;

                closeButton.Text = "Close";
                closeButton.Dock = DockStyle.Bottom;
                closeButton.Height = 36;
                closeButton.DialogResult = DialogResult.OK;

                dialog.Controls.Add(textBox);
                dialog.Controls.Add(closeButton);
                dialog.AcceptButton = closeButton;
                dialog.ShowDialog(this);
            }
        }

        private static string PromptText(IWin32Window owner, string title, string label, string defaultValue)
        {
            using (var form = new Form())
            using (var labelControl = new Label())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowIcon = false;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.Width = 420;
                form.Height = 160;

                labelControl.Left = 12;
                labelControl.Top = 12;
                labelControl.Width = 380;
                labelControl.Text = label;

                textBox.Left = 12;
                textBox.Top = 38;
                textBox.Width = 380;
                textBox.Text = defaultValue ?? string.Empty;

                okButton.Text = "OK";
                okButton.Left = 232;
                okButton.Top = 74;
                okButton.Width = 80;
                okButton.DialogResult = DialogResult.OK;

                cancelButton.Text = "Cancel";
                cancelButton.Left = 312;
                cancelButton.Top = 74;
                cancelButton.Width = 80;
                cancelButton.DialogResult = DialogResult.Cancel;

                form.Controls.Add(labelControl);
                form.Controls.Add(textBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                form.Shown += delegate
                {
                    textBox.SelectionStart = textBox.TextLength;
                    textBox.Focus();
                };

                return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text.Trim() : string.Empty;
            }
        }

        private static string GetGodotSearchStartDirectory()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "project.godot")) || Directory.Exists(Path.Combine(dir.FullName, ".godot")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        private string ResolveGodotRootForEditor()
        {
            return GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
        }

        private string TryResolveGodotRootForPreview()
        {
            try
            {
                return ResolveGodotRootForEditor();
            }
            catch
            {
                return string.Empty;
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

                ReplaceItems(mapsList, snapshot.MapNames, snapshot.SelectedMapIndex);
                ReplaceItems(linksList, snapshot.LinkNames, snapshot.SelectedLinkIndex);
                mapPropertyGrid.SelectedObject = _viewModel.SelectedMap ?? (object)snapshot.MapState;
                linkPropertyGrid.SelectedObject = snapshot.LinkState;
                _mapPreviewCanvas.SetData(_viewModel.SelectedMap, TryResolveGodotRootForPreview());
                _mapPreviewCanvas.SetCollisionOverlay(_currentCollisionOverlay, _currentCollisionOverlayTarget, _showCollisionOverlay);
                _linksPreviewCanvas.SetData(_viewModel.CurrentProject, _viewModel.SelectedMap, _viewModel.SelectedLink);
                statusText.Text = snapshot.StatusText;
            }
            finally
            {
                _isApplyingSnapshot = false;
            }
        }

        private static void ReplaceItems(ListBox listBox, string[] items)
        {
            ReplaceItems(listBox, items, listBox.SelectedIndex);
        }

        private static void ReplaceItems(ListBox listBox, string[] items, int selectedIndex)
        {
            listBox.BeginUpdate();
            try
            {
                listBox.Items.Clear();
                if (items != null && items.Length > 0)
                    listBox.Items.AddRange(items);

                if (selectedIndex >= 0 && selectedIndex < listBox.Items.Count)
                    listBox.SelectedIndex = selectedIndex;
                else if (listBox.Items.Count == 0)
                    listBox.SelectedIndex = -1;
            }
            finally
            {
                listBox.EndUpdate();
            }
        }

        private void MapsListSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isApplyingSnapshot)
                return;

            _viewModel.SelectMapByIndex(mapsList.SelectedIndex);
            ClearCollisionOverlay();
            RefreshCollisionOverlayFromToolbar(false);
            ApplySnapshotToUi();
        }

        private void LinksListSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isApplyingSnapshot)
                return;

            _viewModel.SelectLinkByIndex(linksList.SelectedIndex);
            ApplySnapshotToUi();
        }

        private void MapPropertyGridPropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            if (_isApplyingSnapshot)
                return;

            var propertyName = e == null || e.ChangedItem == null || e.ChangedItem.PropertyDescriptor == null
                ? string.Empty
                : e.ChangedItem.PropertyDescriptor.Name;
            _viewModel.MarkSelectedMapEdited(propertyName);
            ApplySnapshotToUi();
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
