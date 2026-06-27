using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
using MapEditorTool.Executor.TileCollision;
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
        private readonly TileCollisionExecutor _tileCollisionExecutor;
        private readonly UndoManager _undoManager;
        private readonly MapEditorShellViewModel _viewModel;
        private readonly MapPreviewCanvas _mapPreviewCanvas;
        private readonly LinksPreviewCanvas _linksPreviewCanvas;
        private MapEditorTool.Executor.MapCreation.CollisionLayoutData _currentCollisionOverlay;
        private CollisionLayoutTarget _currentCollisionOverlayTarget;
        private bool _showCollisionOverlay;
        private bool _isDeveloperCommentBoxOpen;
        private bool _isApplyingSnapshot;
        private bool _isReplayingUndo;

        public Form1()
        {
            InitializeComponent();
            _viewModel = MapEditorShellViewModel.CreateShellDefaults();
            _undoManager = new UndoManager();
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
            _tileCollisionExecutor = new TileCollisionExecutor();

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
            var selectTool = CreateToolButton("Select(S)", "tool.select", true);
            _isApplyingSnapshot = true;
            try
            {
                selectTool.Checked = true;
            }
            finally
            {
                _isApplyingSnapshot = false;
            }
            mapTools.Items.Add(selectTool);
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
            _mapPreviewCanvas.PortalMoveCommitted += MapPreviewCanvasPortalMoveCommitted;
            _mapPreviewCanvas.PortalAddRequested += MapPreviewCanvasPortalAddRequested;
            _mapPreviewCanvas.PortalContextRequested += MapPreviewCanvasPortalContextRequested;
            _mapPreviewCanvas.CollisionLayoutEdited += MapPreviewCanvasCollisionLayoutEdited;
            _mapPreviewCanvas.CollisionLayoutPolygonSelected += MapPreviewCanvasCollisionLayoutPolygonSelected;
            _mapPreviewCanvas.CollisionLayoutPolygonEdited += MapPreviewCanvasCollisionLayoutPolygonEdited;
            _mapPreviewCanvas.TileCollisionSelected += MapPreviewCanvasTileCollisionSelected;
            _mapPreviewCanvas.TileCollisionEditCommitted += MapPreviewCanvasTileCollisionEditCommitted;
            _mapPreviewCanvas.TileCollisionAddBoxRequested += MapPreviewCanvasTileCollisionAddBoxRequested;
            _mapPreviewCanvas.TileCollisionRemoveRequested += MapPreviewCanvasTileCollisionRemoveRequested;
            _mapPreviewCanvas.BringToFront();

            linksPlaceholder.Text =
                string.Empty;
            linksPlaceholder.Visible = false;
            linksTabSplit.Panel1.Controls.Add(_linksPreviewCanvas);
            _linksPreviewCanvas.Dock = DockStyle.Fill;
            _linksPreviewCanvas.MapSelected += LinksPreviewCanvasMapSelected;
            _linksPreviewCanvas.LinkSelected += LinksPreviewCanvasLinkSelected;
            _linksPreviewCanvas.PortalSelected += LinksPreviewCanvasPortalSelected;
            _linksPreviewCanvas.PortalTargetRequested += LinksPreviewCanvasPortalTargetRequested;
            _linksPreviewCanvas.BringToFront();
        }

        private void MapPreviewCanvasPortalMoveCommitted(object sender, PortalMoveCommittedEventArgs e)
        {
            if (e == null || e.Portal == null)
                return;

            var selectedMap = _viewModel.SelectedMap;
            if (selectedMap == null)
                return;

            try
            {
                var godotRoot = ResolveGodotRootForEditor();
                _portalEditingExecutor.ApplyPortalPropertyChange(godotRoot, selectedMap, e.Portal, "X");
                e.Accepted = true;
                _viewModel.MarkSelectedMapEdited("Portal position");
                _viewModel.SetStatusText("Portal moved: " + FormatPortalName(e.Portal));
                mapPropertyGrid.Refresh();
            }
            catch (Exception ex)
            {
                e.Accepted = false;
                _viewModel.SetStatusText("Move portal failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Move portal failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void MapPreviewCanvasPortalAddRequested(object sender, PortalAddRequestedEventArgs e)
        {
            if (e == null)
                return;

            var selectedMap = _viewModel.SelectedMap;
            if (selectedMap == null)
                return;

            var canvas = sender as Control;
            var menu = new ContextMenuStrip();
            menu.Closed += delegate { menu.Dispose(); };
            var addItem = new ToolStripMenuItem("Add Portal Here");
            addItem.Click += delegate { AddPortalAtWorld(selectedMap, e.X, e.Y); };
            menu.Items.Add(addItem);

            if (canvas == null)
                menu.Show(this, PointToClient(Cursor.Position));
            else
                menu.Show(canvas, canvas.PointToClient(Cursor.Position));
        }

        private void MapPreviewCanvasPortalContextRequested(object sender, PortalContextRequestedEventArgs e)
        {
            if (e == null || e.Portal == null)
                return;

            var selectedMap = _viewModel.SelectedMap;
            if (selectedMap == null)
                return;

            var canvas = sender as Control;
            var menu = new ContextMenuStrip();
            menu.Closed += delegate { menu.Dispose(); };
            menu.Items.Add(new ToolStripMenuItem(FormatPortalName(e.Portal)) { Enabled = false });

            var openLink = new ToolStripMenuItem("Open Link");
            openLink.Click += delegate { OpenPortalLink(selectedMap, e.Portal); };
            menu.Items.Add(openLink);

            var jumpToTarget = new ToolStripMenuItem("Jump to Target");
            jumpToTarget.Enabled = !string.IsNullOrWhiteSpace(e.Portal.TargetMapId);
            jumpToTarget.Click += delegate { JumpToPortalTarget(e.Portal); };
            menu.Items.Add(jumpToTarget);

            if (canvas == null)
                menu.Show(this, PointToClient(Cursor.Position));
            else
                menu.Show(canvas, canvas.PointToClient(Cursor.Position));
        }

        private void AddPortalAtWorld(MapDefinition selectedMap, float x, float y)
        {
            try
            {
                var godotRoot = ResolveGodotRootForEditor();
                var result = _portalEditingExecutor.CreatePortal(godotRoot, _viewModel.CurrentProject, selectedMap, x, y);
                if (selectedMap.Portals == null)
                    selectedMap.Portals = new List<Portal>();

                selectedMap.Portals.Add(result.Portal);
                _viewModel.MarkSelectedMapEdited("Portals");
                _viewModel.SetStatusText("Portal created: " + FormatPortalName(result.Portal));
                mapPropertyGrid.Refresh();
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusText("Create portal failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Create portal failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
        }

        private void OpenPortalLink(MapDefinition map, Portal portal)
        {
            if (map == null || portal == null)
                return;

            var link = FindOrCreateLinkForPortal(NormalizeMapId(map), portal.Id);
            if (link == null)
                return;

            _viewModel.SelectLink(link);
            tabs.SelectedTab = linksTab;
            ApplySnapshotToUi();
        }

        private void JumpToPortalTarget(Portal portal)
        {
            if (portal == null || string.IsNullOrWhiteSpace(portal.TargetMapId))
                return;

            _viewModel.SelectMapById(portal.TargetMapId);
            tabs.SelectedTab = mapTab;
            ClearCollisionOverlay();
            RefreshCollisionOverlayFromToolbar(false);
            ApplySnapshotToUi();
        }

        private void MapPreviewCanvasCollisionLayoutEdited(object sender, CollisionLayoutEditedEventArgs e)
        {
            if (e == null)
                return;

            _currentCollisionOverlay = e.Layout;
            _currentCollisionOverlayTarget = e.Target;
            _showCollisionOverlay = true;
            PushCollisionLayoutUndo("Collision cell edit", e.Target, e.BeforeLayout, e.AfterLayout);
            _viewModel.MarkSelectedMapEdited("Collision layout");
            _viewModel.SetStatusText(
                "Collision cell " + (e.Solid ? "painted" : "cleared") +
                ": " + e.CellX + ", " + e.CellY +
                ". Use Save to write the collision file.");
            statusText.Text = _viewModel.Snapshot.StatusText;
        }

        private void MapPreviewCanvasCollisionLayoutPolygonSelected(object sender, CollisionLayoutPolygonSelectedEventArgs e)
        {
            if (e == null)
                return;

            _currentCollisionOverlay = e.Layout;
            _currentCollisionOverlayTarget = e.Target;
            _showCollisionOverlay = true;
            _viewModel.SetStatusText(e.PolygonIndex >= 0
                ? "Collision polygon selected: " + e.PolygonIndex
                : "Collision polygon selection cleared.");
            statusText.Text = _viewModel.Snapshot.StatusText;
        }

        private void MapPreviewCanvasCollisionLayoutPolygonEdited(object sender, CollisionLayoutPolygonEditedEventArgs e)
        {
            if (e == null)
                return;

            _currentCollisionOverlay = e.Layout;
            _currentCollisionOverlayTarget = e.Target;
            _showCollisionOverlay = true;
            PushCollisionLayoutUndo(e.EditName, e.Target, e.BeforeLayout, e.AfterLayout);
            _viewModel.MarkSelectedMapEdited("Collision polygon");
            _viewModel.SetStatusText(
                e.EditName +
                ": polygon " + e.PolygonIndex +
                ". Use Save to write the collision file.");
            statusText.Text = _viewModel.Snapshot.StatusText;
        }

        private void MapPreviewCanvasTileCollisionSelected(object sender, TileCollisionSelectedEventArgs e)
        {
            if (e == null || e.Selection == null)
                _viewModel.SetStatusText("Tile collision selection cleared.");
            else
                _viewModel.SetStatusText("Tile collision selected: " + e.Selection.FormatSummary());

            statusText.Text = _viewModel.Snapshot.StatusText;
        }

        private void MapPreviewCanvasTileCollisionEditCommitted(object sender, TileCollisionEditCommittedEventArgs e)
        {
            if (e == null || e.Selection == null)
                return;

            var selectedMap = _viewModel.SelectedMap;
            if (selectedMap == null)
            {
                e.Accepted = false;
                e.ErrorMessage = "No map is selected.";
                _viewModel.SetStatusText("Tile collision edit failed: " + e.ErrorMessage);
                statusText.Text = _viewModel.Snapshot.StatusText;
                return;
            }

            try
            {
                var selection = e.Selection;
                var commit = new TileCollisionCommit
                {
                    TileSetResPath = selection.TileSetResPath,
                    LayerNodePath = selection.LayerNodePath,
                    SourceId = selection.SourceId,
                    AtlasX = selection.AtlasX,
                    AtlasY = selection.AtlasY,
                    CellX = selection.CellX,
                    CellY = selection.CellY,
                    OneWay = selection.OneWay,
                    FromPoints = CloneGodotVectorPoints(e.FromPoints),
                    ToPoints = CloneGodotVectorPoints(e.ToPoints)
                };

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var result = _tileCollisionExecutor.ApplyTileCollisionEdits(godotRoot, selectedMap, new List<TileCollisionCommit> { commit });
                _mapPreviewCanvas.EvictTileSetCacheForResPath(selection.TileSetResPath);
                _mapPreviewCanvas.ClearTileCollisionSelection();
                e.Accepted = true;
                _viewModel.MarkSelectedMapEdited("Tile collision");
                _viewModel.SetStatusText("Tile collision vertex saved: " + result.Summary);
            }
            catch (Exception ex)
            {
                e.Accepted = false;
                e.ErrorMessage = ex.Message;
                _viewModel.SetStatusText("Tile collision edit failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Tile collision edit failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusText.Text = _viewModel.Snapshot.StatusText;
                ApplySnapshotToUi();
            }
        }

        private void MapPreviewCanvasTileCollisionAddBoxRequested(object sender, TileCollisionAddBoxRequestedEventArgs e)
        {
            if (e == null || e.Cell == null)
                return;

            var selectedMap = RequireSelectedMapForTileCollisionEvent("add");
            if (selectedMap == null)
                return;

            try
            {
                var cell = e.Cell;
                var commit = new TileCollisionCommit
                {
                    TileSetResPath = cell.TileSetResPath,
                    LayerNodePath = cell.LayerNodePath,
                    SourceId = cell.SourceId,
                    AtlasX = cell.AtlasX,
                    AtlasY = cell.AtlasY,
                    CellX = cell.CellX,
                    CellY = cell.CellY,
                    OneWay = false,
                    FromPoints = new List<GodotVector2>(),
                    ToPoints = CloneGodotVectorPoints(e.Points)
                };

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var result = _tileCollisionExecutor.ApplyTileCollisionEdits(godotRoot, selectedMap, new List<TileCollisionCommit> { commit });
                _mapPreviewCanvas.EvictTileSetCacheForResPath(cell.TileSetResPath);
                _mapPreviewCanvas.ClearTileCollisionSelection();
                e.Accepted = true;
                _viewModel.MarkSelectedMapEdited("Tile collision");
                _viewModel.SetStatusText("Tile collision box added: " + result.Summary);
            }
            catch (Exception ex)
            {
                e.Accepted = false;
                _viewModel.SetStatusText("Add tile collision failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Add tile collision failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusText.Text = _viewModel.Snapshot.StatusText;
                ApplySnapshotToUi();
            }
        }

        private void MapPreviewCanvasTileCollisionRemoveRequested(object sender, TileCollisionRemoveRequestedEventArgs e)
        {
            if (e == null || e.Cell == null)
                return;

            var selectedMap = RequireSelectedMapForTileCollisionEvent("remove");
            if (selectedMap == null)
                return;

            try
            {
                var cell = e.Cell;
                var commit = new TileCollisionAlternativeCommit
                {
                    LayerNodePath = cell.LayerNodePath,
                    CellX = cell.CellX,
                    CellY = cell.CellY,
                    FromAlternative = cell.Alternative,
                    ToAlternative = 0
                };

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var result = _tileCollisionExecutor.ApplyTileCollisionAlternativeEdits(godotRoot, selectedMap, new List<TileCollisionAlternativeCommit> { commit });
                _mapPreviewCanvas.EvictTileSetCacheForResPath(cell.TileSetResPath);
                _mapPreviewCanvas.ClearTileCollisionSelection();
                e.Accepted = true;
                _viewModel.MarkSelectedMapEdited("Tile collision");
                _viewModel.SetStatusText("Tile collision removed: " + result.Summary);
            }
            catch (Exception ex)
            {
                e.Accepted = false;
                _viewModel.SetStatusText("Remove tile collision failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Remove tile collision failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusText.Text = _viewModel.Snapshot.StatusText;
                ApplySnapshotToUi();
            }
        }

        private MapDefinition RequireSelectedMapForTileCollisionEvent(string operationName)
        {
            var selectedMap = _viewModel.SelectedMap;
            if (selectedMap != null)
                return selectedMap;

            _viewModel.SetStatusText("Tile collision " + operationName + " failed: no map is selected.");
            statusText.Text = _viewModel.Snapshot.StatusText;
            return null;
        }

        private void LinksPreviewCanvasMapSelected(object sender, LinkMapSelectedEventArgs e)
        {
            if (e == null)
                return;

            _viewModel.SelectMapById(e.MapId);
            tabs.SelectedTab = mapTab;
            ClearCollisionOverlay();
            RefreshCollisionOverlayFromToolbar(false);
            ApplySnapshotToUi();
        }

        private void LinksPreviewCanvasLinkSelected(object sender, LinkSelectedEventArgs e)
        {
            if (e == null || e.Link == null)
                return;

            _viewModel.SelectLink(e.Link);
            tabs.SelectedTab = linksTab;
            ApplySnapshotToUi();
        }

        private void LinksPreviewCanvasPortalSelected(object sender, PortalSelectedEventArgs e)
        {
            if (e == null)
                return;

            var link = FindOrCreateLinkForPortal(e.MapId, e.PortalId);
            if (link != null)
            {
                _viewModel.SelectLink(link);
                tabs.SelectedTab = linksTab;
                ApplySnapshotToUi();
            }
        }

        private void LinksPreviewCanvasPortalTargetRequested(object sender, PortalTargetRequestedEventArgs e)
        {
            if (e == null)
                return;

            SetPortalLinkTarget(e.FromMapId, e.FromPortalId, e.TargetMapId, e.TargetPortalId);
        }

        private MapLink FindOrCreateLinkForPortal(string fromMapId, string fromPortalId)
        {
            var map = FindMapById(fromMapId);
            if (map == null || map.Portals == null)
                return null;

            var portal = FindPortalById(map, fromPortalId);
            if (portal == null)
                return null;

            var link = FindLinkForPortal(NormalizeMapId(map), fromPortalId);
            if (link != null)
                return link;

            var targetMapId = (portal.TargetMapId ?? string.Empty).Trim();
            if (targetMapId.Length == 0)
            {
                var fallback = _viewModel.CurrentProject.Maps.FirstOrDefault(candidate =>
                    !string.Equals(NormalizeMapId(candidate), NormalizeMapId(map), StringComparison.Ordinal));
                targetMapId = fallback == null ? NormalizeMapId(map) : NormalizeMapId(fallback);
            }

            link = new MapLink
            {
                From = new LinkEndpoint { MapId = NormalizeMapId(map), PortalId = (portal.Id ?? string.Empty).Trim() },
                To = new LinkEndpoint { MapId = targetMapId, PortalId = (portal.TargetPortalId ?? string.Empty).Trim() }
            };
            _viewModel.CurrentProject.Links.Add(link);
            _viewModel.MarkSelectedMapEdited("Links");
            _viewModel.SetStatusText("Link selected: " + link.DisplayName);
            return link;
        }

        private void SetPortalLinkTarget(string fromMapId, string fromPortalId, string targetMapId, string targetPortalId)
        {
            var map = FindMapById(fromMapId);
            if (map == null || map.Portals == null)
                return;

            var portal = FindPortalById(map, fromPortalId);
            if (portal == null)
                return;

            var oldTargetMapId = (portal.TargetMapId ?? string.Empty).Trim();
            var oldTargetPortalId = (portal.TargetPortalId ?? string.Empty).Trim();
            var existingLink = FindLinkForPortal(NormalizeMapId(map), fromPortalId);
            var oldLinkToMapId = existingLink == null || existingLink.To == null ? string.Empty : (existingLink.To.MapId ?? string.Empty).Trim();
            var oldLinkToPortalId = existingLink == null || existingLink.To == null ? string.Empty : (existingLink.To.PortalId ?? string.Empty).Trim();
            var hadExistingLink = existingLink != null;

            try
            {
                targetMapId = (targetMapId ?? string.Empty).Trim();
                targetPortalId = (targetPortalId ?? string.Empty).Trim();
                portal.TargetMapId = targetMapId;
                portal.TargetPortalId = targetPortalId;

                var link = FindLinkForPortal(NormalizeMapId(map), fromPortalId);
                if (targetMapId.Length == 0)
                {
                    if (link != null)
                        _viewModel.CurrentProject.Links.Remove(link);
                }
                else if (link == null)
                {
                    link = new MapLink
                    {
                        From = new LinkEndpoint { MapId = NormalizeMapId(map), PortalId = (portal.Id ?? string.Empty).Trim() },
                        To = new LinkEndpoint { MapId = targetMapId, PortalId = targetPortalId }
                    };
                    _viewModel.CurrentProject.Links.Add(link);
                }
                else
                {
                    link.To.MapId = targetMapId;
                    link.To.PortalId = targetPortalId;
                }

                var godotRoot = ResolveGodotRootForEditor();
                _portalEditingExecutor.ApplyPortalPropertyChange(godotRoot, map, portal, "TargetMapId");
                _viewModel.MarkSelectedMapEdited("Portal target");
                if (link != null)
                    _viewModel.SelectLink(link);
                _viewModel.SetStatusText(targetMapId.Length == 0
                    ? "Portal link cleared: " + FormatPortalName(portal)
                    : "Portal link updated: " + FormatPortalName(portal));
            }
            catch (Exception ex)
            {
                portal.TargetMapId = oldTargetMapId;
                portal.TargetPortalId = oldTargetPortalId;
                RestoreLinkForPortal(map, fromPortalId, hadExistingLink, oldLinkToMapId, oldLinkToPortalId);
                _viewModel.SetStatusText("Set portal target failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Set portal target failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ApplySnapshotToUi();
            }
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
            var button = new ToolStripButton(text)
            {
                Name = name,
                CheckOnClick = checkOnClick,
                ToolTipText = "Prototype shell tool: " + text
            };
            if (checkOnClick)
                button.CheckedChanged += MapToolSelectionChanged;
            return button;
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
            else if (string.Equals(item.Name, "menu.edit.undo", StringComparison.Ordinal))
            {
                UndoLastAction();
            }
            else if (string.Equals(item.Name, "menu.edit.redo", StringComparison.Ordinal))
            {
                RedoLastAction();
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
                _undoManager.Clear();
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
                    _undoManager.Clear();
                    _viewModel.SetStatusText("Initialized foreground texture collision: " + foreground.Summary);
                }
                else
                {
                    var layout = MapEditorTool.Executor.MapCreation.CollisionLayoutData.Create(selected.RoomWidth, selected.RoomHeight);
                    var saved = _collisionLayoutExecutor.SaveLayout(godotRoot, selected, target, layout);
                    SetCollisionOverlay(saved.Layout, target, true);
                    _undoManager.Clear();
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
                _undoManager.Clear();
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
                var layoutToSave = _showCollisionOverlay &&
                    _currentCollisionOverlay != null &&
                    _currentCollisionOverlayTarget == target
                    ? _currentCollisionOverlay
                    : _collisionLayoutExecutor.LoadLayout(godotRoot, selected, target, true).Layout;
                var saved = _collisionLayoutExecutor.SaveLayout(godotRoot, selected, target, layoutToSave);
                SetCollisionOverlay(saved.Layout, target, true);
                _undoManager.Clear();
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

        private void PushCollisionLayoutUndo(
            string name,
            CollisionLayoutTarget target,
            MapEditorTool.Executor.MapCreation.CollisionLayoutData beforeLayout,
            MapEditorTool.Executor.MapCreation.CollisionLayoutData afterLayout)
        {
            if (_isReplayingUndo)
                return;
            if (beforeLayout == null || afterLayout == null)
                return;

            _undoManager.Push(new CollisionLayoutUndoAction(
                string.IsNullOrWhiteSpace(name) ? "Collision layout edit" : name,
                target,
                beforeLayout,
                afterLayout,
                ApplyCollisionLayoutUndoSnapshot));
        }

        private void UndoLastAction()
        {
            if (!_undoManager.CanUndo)
            {
                _viewModel.SetStatusText("Undo skipped: no action is available.");
                return;
            }

            _isReplayingUndo = true;
            try
            {
                var name = _undoManager.PeekUndoName();
                if (_undoManager.TryUndo())
                    _viewModel.SetStatusText("Undone: " + name + ". Use Save to write the collision file.");
            }
            finally
            {
                _isReplayingUndo = false;
                ApplySnapshotToUi();
            }
        }

        private void RedoLastAction()
        {
            if (!_undoManager.CanRedo)
            {
                _viewModel.SetStatusText("Redo skipped: no action is available.");
                return;
            }

            _isReplayingUndo = true;
            try
            {
                var name = _undoManager.PeekRedoName();
                if (_undoManager.TryRedo())
                    _viewModel.SetStatusText("Redone: " + name + ". Use Save to write the collision file.");
            }
            finally
            {
                _isReplayingUndo = false;
                ApplySnapshotToUi();
            }
        }

        private void ApplyCollisionLayoutUndoSnapshot(
            CollisionLayoutTarget target,
            MapEditorTool.Executor.MapCreation.CollisionLayoutData layout)
        {
            SetCollisionOverlay(CloneCollisionLayoutData(layout), target, true);
            _viewModel.MarkSelectedMapEdited("Collision layout undo");
        }

        private void MapToolSelectionChanged(object sender, EventArgs e)
        {
            if (_isApplyingSnapshot)
                return;

            var selectedToolItem = sender as ToolStripItem;
            var isToolButton = selectedToolItem != null && IsCollisionToolButton(selectedToolItem.Name);
            ApplyCollisionToolButtonSelection(selectedToolItem);
            if (!isToolButton)
            {
                ApplyCollisionModeSelectionToMap();
                RefreshCollisionOverlayFromToolbar(false);
            }
            UpdateCollisionEditorState();
            ApplySnapshotToUi();
        }

        private void ApplyCollisionToolButtonSelection(ToolStripItem selectedItem)
        {
            if (selectedItem == null || !IsCollisionToolButton(selectedItem.Name))
                return;

            _isApplyingSnapshot = true;
            try
            {
                foreach (var name in GetCollisionToolButtonNames())
                {
                    var button = FindToolStripItem(mapTools.Items, name) as ToolStripButton;
                    if (button != null)
                        button.Checked = string.Equals(button.Name, selectedItem.Name, StringComparison.Ordinal);
                }
            }
            finally
            {
                _isApplyingSnapshot = false;
            }
        }

        private void UpdateCollisionEditorState()
        {
            _mapPreviewCanvas.SetCollisionEditorState(GetSelectedCollisionEditorMode(), GetSelectedCollisionEditorTool());
        }

        private CollisionEditorMode GetSelectedCollisionEditorMode()
        {
            var combo = FindToolStripItem(mapTools.Items, "collisionEditModeCombo") as ToolStripComboBox;
            return combo != null && combo.SelectedIndex == 0
                ? CollisionEditorMode.TileSetCollision
                : CollisionEditorMode.CollisionLayout;
        }

        private CollisionEditorTool GetSelectedCollisionEditorTool()
        {
            if (IsToolButtonChecked("tool.vertex"))
                return CollisionEditorTool.Vertex;
            if (IsToolButtonChecked("tool.move"))
                return CollisionEditorTool.Move;
            if (IsToolButtonChecked("tool.rotate"))
                return CollisionEditorTool.Rotate;
            if (IsToolButtonChecked("tool.scale"))
                return CollisionEditorTool.Scale;
            if (IsToolButtonChecked("tool.addSquareCollision"))
                return CollisionEditorTool.AddBox;
            if (IsToolButtonChecked("tool.removeCollision"))
                return CollisionEditorTool.Remove;

            return CollisionEditorTool.Select;
        }

        private bool IsToolButtonChecked(string name)
        {
            var button = FindToolStripItem(mapTools.Items, name) as ToolStripButton;
            return button != null && button.Checked;
        }

        private static bool IsCollisionToolButton(string name)
        {
            foreach (var item in GetCollisionToolButtonNames())
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string[] GetCollisionToolButtonNames()
        {
            return new[]
            {
                "tool.select",
                "tool.vertex",
                "tool.move",
                "tool.rotate",
                "tool.scale",
                "tool.addSquareCollision",
                "tool.removeCollision"
            };
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
                if (GetSelectedCollisionEditorMode() == CollisionEditorMode.TileSetCollision)
                {
                    SetCollisionOverlay(null, GetSelectedCollisionLayoutTarget(), true);
                    _undoManager.Clear();
                    return;
                }

                var godotRoot = GodotProjectLocator.FindGodotRoot(GetGodotSearchStartDirectory());
                var target = GetSelectedCollisionLayoutTarget();
                var loaded = _collisionLayoutExecutor.LoadLayout(godotRoot, selected, target, ensureDefaultPath);
                SetCollisionOverlay(loaded.Layout, target, true);
                _undoManager.Clear();
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
            if (!_isReplayingUndo)
                _undoManager.Clear();
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

        private static MapEditorTool.Executor.MapCreation.CollisionLayoutData CloneCollisionLayoutData(
            MapEditorTool.Executor.MapCreation.CollisionLayoutData layout)
        {
            if (layout == null)
                return null;

            var clone = new MapEditorTool.Executor.MapCreation.CollisionLayoutData
            {
                RoomWidth = layout.RoomWidth,
                RoomHeight = layout.RoomHeight,
                Solid = layout.Solid == null ? new bool[0] : (bool[])layout.Solid.Clone(),
                Polygons = new List<List<MapEditorTool.Executor.MapCreation.GodotVector2Data>>()
            };

            if (layout.Polygons != null)
            {
                foreach (var polygon in layout.Polygons)
                {
                    var polygonClone = new List<MapEditorTool.Executor.MapCreation.GodotVector2Data>();
                    if (polygon != null)
                    {
                        foreach (var point in polygon)
                        {
                            if (point == null)
                                polygonClone.Add(new MapEditorTool.Executor.MapCreation.GodotVector2Data());
                            else
                                polygonClone.Add(new MapEditorTool.Executor.MapCreation.GodotVector2Data { X = point.X, Y = point.Y });
                        }
                    }

                    clone.Polygons.Add(polygonClone);
                }
            }

            return clone;
        }

        private static List<GodotVector2> CloneGodotVectorPoints(List<GodotVector2> points)
        {
            var clone = new List<GodotVector2>();
            if (points == null)
                return clone;

            foreach (var point in points)
                clone.Add(new GodotVector2(point.X, point.Y));

            return clone;
        }

        private static string FormatPortalName(Portal portal)
        {
            if (portal == null)
                return string.Empty;

            var name = (portal.Name ?? string.Empty).Trim();
            if (name.Length > 0)
                return name;

            var id = (portal.Id ?? string.Empty).Trim();
            if (id.Length > 0)
                return id;

            return (portal.NodePath ?? string.Empty).Trim();
        }

        private MapDefinition FindMapById(string mapId)
        {
            mapId = (mapId ?? string.Empty).Trim();
            if (_viewModel.CurrentProject == null || _viewModel.CurrentProject.Maps == null || mapId.Length == 0)
                return null;

            return _viewModel.CurrentProject.Maps.FirstOrDefault(map =>
                string.Equals((map.Id ?? string.Empty).Trim(), mapId, StringComparison.Ordinal) ||
                string.Equals((map.ScenePath ?? string.Empty).Trim(), mapId, StringComparison.Ordinal));
        }

        private static Portal FindPortalById(MapDefinition map, string portalId)
        {
            portalId = (portalId ?? string.Empty).Trim();
            if (map == null || map.Portals == null || portalId.Length == 0)
                return null;

            return map.Portals.FirstOrDefault(portal =>
                string.Equals((portal.Id ?? string.Empty).Trim(), portalId, StringComparison.Ordinal));
        }

        private MapLink FindLinkForPortal(string fromMapId, string fromPortalId)
        {
            fromMapId = (fromMapId ?? string.Empty).Trim();
            fromPortalId = (fromPortalId ?? string.Empty).Trim();
            if (_viewModel.CurrentProject == null || _viewModel.CurrentProject.Links == null)
                return null;

            return _viewModel.CurrentProject.Links.FirstOrDefault(link =>
                link != null &&
                link.From != null &&
                string.Equals((link.From.MapId ?? string.Empty).Trim(), fromMapId, StringComparison.Ordinal) &&
                string.Equals((link.From.PortalId ?? string.Empty).Trim(), fromPortalId, StringComparison.Ordinal));
        }

        private void RestoreLinkForPortal(MapDefinition map, string fromPortalId, bool hadExistingLink, string oldTargetMapId, string oldTargetPortalId)
        {
            if (_viewModel.CurrentProject == null || _viewModel.CurrentProject.Links == null || map == null)
                return;

            var fromMapId = NormalizeMapId(map);
            var link = FindLinkForPortal(fromMapId, fromPortalId);
            if (!hadExistingLink)
            {
                if (link != null)
                    _viewModel.CurrentProject.Links.Remove(link);
                return;
            }

            if (link == null)
            {
                link = new MapLink
                {
                    From = new LinkEndpoint { MapId = fromMapId, PortalId = (fromPortalId ?? string.Empty).Trim() },
                    To = new LinkEndpoint()
                };
                _viewModel.CurrentProject.Links.Add(link);
            }

            link.To.MapId = oldTargetMapId ?? string.Empty;
            link.To.PortalId = oldTargetPortalId ?? string.Empty;
        }

        private static string NormalizeMapId(MapDefinition map)
        {
            if (map == null)
                return string.Empty;

            var scenePath = (map.ScenePath ?? string.Empty).Trim();
            if (scenePath.Length > 0)
                return scenePath;

            return (map.Id ?? string.Empty).Trim();
        }

        private void NewProject()
        {
            _viewModel.CreateNewProject();
            _undoManager.Clear();
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
                    _undoManager.Clear();
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
                _mapPreviewCanvas.SetCollisionEditorState(GetSelectedCollisionEditorMode(), GetSelectedCollisionEditorTool());
                _linksPreviewCanvas.SetData(_viewModel.CurrentProject, _viewModel.SelectedMap, _viewModel.SelectedLink);
                ApplyUndoStateToMenu();
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

        private void ApplyUndoStateToMenu()
        {
            var undo = FindToolStripItem(mainMenu.Items, "menu.edit.undo");
            if (undo != null)
            {
                undo.Enabled = _undoManager.CanUndo;
                undo.Text = _undoManager.CanUndo ? "Undo: " + _undoManager.PeekUndoName() : "Undo";
            }

            var redo = FindToolStripItem(mainMenu.Items, "menu.edit.redo");
            if (redo != null)
            {
                redo.Enabled = _undoManager.CanRedo;
                redo.Text = _undoManager.CanRedo ? "Redo: " + _undoManager.PeekRedoName() : "Redo";
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

        private interface IUndoableAction
        {
            string Name { get; }
            void Undo();
            void Redo();
        }

        private sealed class UndoManager
        {
            private readonly Stack<IUndoableAction> _undo;
            private readonly Stack<IUndoableAction> _redo;

            public UndoManager()
            {
                _undo = new Stack<IUndoableAction>();
                _redo = new Stack<IUndoableAction>();
            }

            public bool CanUndo
            {
                get { return _undo.Count > 0; }
            }

            public bool CanRedo
            {
                get { return _redo.Count > 0; }
            }

            public void Clear()
            {
                _undo.Clear();
                _redo.Clear();
            }

            public string PeekUndoName()
            {
                return _undo.Count == 0 ? string.Empty : _undo.Peek().Name;
            }

            public string PeekRedoName()
            {
                return _redo.Count == 0 ? string.Empty : _redo.Peek().Name;
            }

            public void Push(IUndoableAction action)
            {
                if (action == null)
                    return;

                _undo.Push(action);
                _redo.Clear();
            }

            public bool TryUndo()
            {
                if (!CanUndo)
                    return false;

                var action = _undo.Pop();
                action.Undo();
                _redo.Push(action);
                return true;
            }

            public bool TryRedo()
            {
                if (!CanRedo)
                    return false;

                var action = _redo.Pop();
                action.Redo();
                _undo.Push(action);
                return true;
            }
        }

        private sealed class CollisionLayoutUndoAction : IUndoableAction
        {
            private readonly CollisionLayoutTarget _target;
            private readonly MapEditorTool.Executor.MapCreation.CollisionLayoutData _before;
            private readonly MapEditorTool.Executor.MapCreation.CollisionLayoutData _after;
            private readonly Action<CollisionLayoutTarget, MapEditorTool.Executor.MapCreation.CollisionLayoutData> _apply;

            public CollisionLayoutUndoAction(
                string name,
                CollisionLayoutTarget target,
                MapEditorTool.Executor.MapCreation.CollisionLayoutData before,
                MapEditorTool.Executor.MapCreation.CollisionLayoutData after,
                Action<CollisionLayoutTarget, MapEditorTool.Executor.MapCreation.CollisionLayoutData> apply)
            {
                Name = name ?? string.Empty;
                _target = target;
                _before = CloneCollisionLayoutData(before);
                _after = CloneCollisionLayoutData(after);
                _apply = apply;
            }

            public string Name { get; private set; }

            public void Undo()
            {
                if (_apply != null)
                    _apply(_target, CloneCollisionLayoutData(_before));
            }

            public void Redo()
            {
                if (_apply != null)
                    _apply(_target, CloneCollisionLayoutData(_after));
            }
        }
    }
}
