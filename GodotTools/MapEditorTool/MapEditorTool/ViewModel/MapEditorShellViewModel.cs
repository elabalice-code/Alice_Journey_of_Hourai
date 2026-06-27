using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;
using MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment;

namespace MapEditorTool.ViewModel
{
    // Signal bus: the central cable hub that receives all producer UI signals
    // and folds them into the consumer snapshot used to refresh the UI.
    public sealed class MapEditorShellViewModel
    {
        private readonly List<UiSignal> _producerSignals = new List<UiSignal>();
        private readonly DeveloperCommentSignalMachine _developerCommentMachine = new DeveloperCommentSignalMachine();
        private readonly MapEditorUiSnapshot _snapshot;
        private MapProject _currentProject;
        private int _selectedMapIndex;

        private MapEditorShellViewModel()
        {
            _snapshot = new MapEditorUiSnapshot
            {
                DeveloperCommentModeEnabled = false,
                StatusText = "MapEditorTool shell ready. Enable Developer Comment Mode to collect UI feedback.",
                LastUpdatedAt = DateTimeOffset.Now,
                MapNames = new string[0],
                LinkNames = new string[0],
                MapState = new MapShellState(),
                LinkState = new LinkShellState(),
                DeveloperCommentState = _developerCommentMachine.State
            };
            _currentProject = new MapProject();
        }

        public MapEditorUiSnapshot Snapshot
        {
            get { return _snapshot; }
        }

        public MapEditorUiSnapshot ConsumerSnapshot
        {
            get { return _snapshot; }
        }

        public IReadOnlyList<UiSignal> ProducerSignals
        {
            get { return _producerSignals.AsReadOnly(); }
        }

        public UiSignal LatestProducerSignal
        {
            get
            {
                return _producerSignals.Count == 0
                    ? null
                    : _producerSignals[_producerSignals.Count - 1];
            }
        }

        public MapProject CurrentProject
        {
            get { return _currentProject; }
        }

        public bool HasCurrentProject
        {
            get { return _currentProject != null && _currentProject.Maps != null && _currentProject.Maps.Count > 0; }
        }

        public MapDefinition SelectedMap
        {
            get
            {
                if (!HasCurrentProject)
                    return null;
                if (_selectedMapIndex < 0 || _selectedMapIndex >= _currentProject.Maps.Count)
                    return null;
                return _currentProject.Maps[_selectedMapIndex];
            }
        }

        public MapLink SelectedLink
        {
            get
            {
                if (_currentProject == null || _currentProject.Links == null)
                    return null;
                var index = _snapshot.SelectedLinkIndex;
                if (index < 0 || index >= _currentProject.Links.Count)
                    return null;
                return _currentProject.Links[index];
            }
        }

        public static MapEditorShellViewModel CreateShellDefaults()
        {
            return new MapEditorShellViewModel();
        }

        public void SubmitSignal(UiSignal signal)
        {
            SubmitSignal(signal, null);
        }

        public DeveloperCommentSignalDecision SubmitDeveloperCommentClick(UiSignal signal)
        {
            var decision = _developerCommentMachine.ConsumeUiClick(signal);
            SubmitSignal(signal, decision);
            return decision;
        }

        private void SubmitSignal(UiSignal signal, DeveloperCommentSignalDecision developerCommentDecision)
        {
            if (signal == null)
                return;

            _producerSignals.Add(signal);
            _snapshot.SignalCount = _producerSignals.Count;
            _snapshot.LastSignalSummary = signal.ToSummary();
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
            ApplyDeveloperCommentDecision(developerCommentDecision);
        }

        public void SetDeveloperCommentMode(bool enabled, UiSignal signal)
        {
            var decision = _developerCommentMachine.SetMode(enabled, signal);
            SubmitSignal(signal, decision);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void ConsumeDeveloperCommentOpenRequest()
        {
            _snapshot.DeveloperCommentOpenRequested = false;
            _snapshot.DeveloperCommentRequestSource = string.Empty;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void LoadImportedProject(MapProject project, string sourceDescription)
        {
            SetCurrentProject(project, string.Empty, true);
            _snapshot.StatusText = string.Format(
                "Imported {0} map(s) and {1} link(s) from {2}.",
                _currentProject.Maps.Count,
                _currentProject.Links.Count,
                sourceDescription ?? "Godot");
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void LoadProjectFile(MapProject project, string projectPath)
        {
            SetCurrentProject(project, projectPath, false);
            _snapshot.StatusText = string.Format(
                "Opened {0} map(s) and {1} link(s) from {2}.",
                _currentProject.Maps.Count,
                _currentProject.Links.Count,
                projectPath ?? string.Empty);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void CreateNewProject()
        {
            SetCurrentProject(MapProject.CreateDefault(), string.Empty, true);
            _snapshot.StatusText = "New MapEditorTool project created.";
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void MarkProjectSaved(string projectPath)
        {
            _snapshot.CurrentProjectPath = projectPath ?? string.Empty;
            _snapshot.ProjectDirty = false;
            _snapshot.StatusText = "Project saved: " + _snapshot.CurrentProjectPath;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void SelectMapByIndex(int index)
        {
            if (!HasCurrentProject)
            {
                _selectedMapIndex = -1;
                _snapshot.SelectedMapIndex = -1;
                _snapshot.MapState = new MapShellState();
                _snapshot.LastUpdatedAt = DateTimeOffset.Now;
                return;
            }

            if (index < 0 || index >= _currentProject.Maps.Count)
                index = 0;

            _selectedMapIndex = index;
            _snapshot.SelectedMapIndex = _selectedMapIndex;
            _snapshot.MapState = BuildMapState(SelectedMap);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void SelectLinkByIndex(int index)
        {
            if (_currentProject == null || _currentProject.Links == null || _currentProject.Links.Count == 0)
            {
                _snapshot.SelectedLinkIndex = -1;
                _snapshot.LinkState = new LinkShellState();
                _snapshot.LastUpdatedAt = DateTimeOffset.Now;
                return;
            }

            if (index < 0 || index >= _currentProject.Links.Count)
                index = 0;

            _snapshot.SelectedLinkIndex = index;
            _snapshot.LinkState = BuildLinkState(SelectedLink);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void MarkSelectedMapEdited(string propertyName)
        {
            RefreshProjectSnapshot();
            _snapshot.ProjectDirty = true;
            _snapshot.StatusText = string.IsNullOrWhiteSpace(propertyName)
                ? "Selected map updated."
                : "Selected map updated: " + propertyName;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void AddMap(MapDefinition map)
        {
            if (map == null)
                return;

            if (_currentProject == null)
                _currentProject = new MapProject();
            if (_currentProject.Maps == null)
                _currentProject.Maps = new List<MapDefinition>();

            _currentProject.Maps.Add(map);
            _selectedMapIndex = _currentProject.Maps.Count - 1;
            RefreshProjectSnapshot();
            _snapshot.ProjectDirty = true;
            _snapshot.StatusText = "Added map: " + FormatMapName(map);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void RemoveSelectedMap()
        {
            var selected = SelectedMap;
            if (selected == null)
                return;

            _currentProject.RemoveMapById(selected.Id);
            if (_selectedMapIndex >= _currentProject.Maps.Count)
                _selectedMapIndex = _currentProject.Maps.Count - 1;
            if (_currentProject.Maps.Count == 0)
                _selectedMapIndex = -1;

            RefreshProjectSnapshot();
            _snapshot.ProjectDirty = true;
            _snapshot.StatusText = "Removed map: " + FormatMapName(selected);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        private void SetCurrentProject(MapProject project, string projectPath, bool dirty)
        {
            _currentProject = project ?? new MapProject();
            _selectedMapIndex = HasCurrentProject ? 0 : -1;
            RefreshProjectSnapshot();
            _snapshot.CurrentProjectPath = projectPath ?? string.Empty;
            _snapshot.ProjectDirty = dirty;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        private void RefreshProjectSnapshot()
        {
            _snapshot.MapNames = _currentProject.Maps
                .Select(FormatMapName)
                .ToArray();

            _snapshot.LinkNames = _currentProject.Links
                .Select(link => link.DisplayName)
                .ToArray();

            _snapshot.SelectedMapIndex = _selectedMapIndex;
            if (_snapshot.SelectedLinkIndex >= _currentProject.Links.Count)
                _snapshot.SelectedLinkIndex = _currentProject.Links.Count - 1;
            if (_currentProject.Links.Count == 0)
                _snapshot.SelectedLinkIndex = -1;
            _snapshot.MapState = BuildMapState(SelectedMap);
            _snapshot.LinkState = BuildLinkState(SelectedLink);
        }

        public void SetStatusText(string statusText)
        {
            _snapshot.StatusText = statusText ?? string.Empty;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void SetReportSummary(string title, string reportSummary)
        {
            _snapshot.LastReportSummary = reportSummary ?? string.Empty;
            _snapshot.StatusText = string.IsNullOrWhiteSpace(title)
                ? "Report generated."
                : title + " generated.";
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        private void ApplyDeveloperCommentDecision(DeveloperCommentSignalDecision decision)
        {
            _snapshot.DeveloperCommentState = _developerCommentMachine.State;
            _snapshot.DeveloperCommentModeEnabled = _snapshot.DeveloperCommentState.ModeEnabled;

            if (decision == null)
                return;

            _snapshot.DeveloperCommentOpenRequested = decision.OpenCommentBox;
            _snapshot.DeveloperCommentSourceSignalConsumed = decision.SourceSignalConsumed;
            _snapshot.DeveloperCommentRequestSource = decision.SourceDescription;
            _snapshot.StatusText = decision.StatusText;
        }

        private static string FormatMapName(MapDefinition map)
        {
            if (map == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(map.DisplayName))
                return map.DisplayName + "  [" + map.ScenePath + "]";

            return map.ScenePath;
        }

        private static MapShellState BuildMapState(MapDefinition map)
        {
            if (map == null)
                return new MapShellState();

            return new MapShellState
            {
                SelectedMap = string.IsNullOrWhiteSpace(map.DisplayName) ? map.Id : map.DisplayName,
                ScenePath = map.ScenePath,
                RoomWidth = map.RoomWidth,
                RoomHeight = map.RoomHeight,
                TileLayerCount = map.TileLayers == null ? 0 : map.TileLayers.Count,
                PortalCount = map.Portals == null ? 0 : map.Portals.Count,
                EntityCount = map.Entities == null ? 0 : map.Entities.Count,
                Notes = "Read-only snapshot imported from Godot scene data."
            };
        }

        private static LinkShellState BuildLinkState(MapLink link)
        {
            if (link == null)
                return new LinkShellState();

            return new LinkShellState
            {
                FromMap = link.From.MapId,
                FromPortal = link.From.PortalId,
                ToMap = link.To.MapId,
                ToPortal = link.To.PortalId,
                Notes = "Read-only link inferred from portal target data."
            };
        }
    }
}
