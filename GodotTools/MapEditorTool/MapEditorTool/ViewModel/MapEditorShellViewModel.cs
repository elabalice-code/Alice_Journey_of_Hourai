using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEditorTool.Models;
using MapEditorTool.SignalWeaver.Main;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation;
using MapEditorTool.SignalWeaver.Featuror.Persistence.Action;
using MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment;
using MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction;

namespace MapEditorTool.ViewModel
{
    // Signal bus: the central cable hub that receives all producer UI signals
    // and folds them into the consumer snapshot used to refresh the UI.
    public sealed class MapEditorShellViewModel
    {
        private readonly List<UiSignal> _producerSignals = new List<UiSignal>();
        private readonly MapEditorSignalWeaverHost _signalWeaverHost = new MapEditorSignalWeaverHost();
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
                DeveloperCommentState = _signalWeaverHost.States.DeveloperComment,
                TerminalActionState = _signalWeaverHost.States.TerminalAction,
                MapGraphSelectionState = _signalWeaverHost.States.MapGraphSelection,
                MapCanvasToolModeState = _signalWeaverHost.States.MapCanvasToolMode,
                MapProjectMutationState = _signalWeaverHost.States.MapProjectMutation,
                PersistenceActionState = _signalWeaverHost.States.PersistenceAction
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
            SubmitSignal(signal, new MapEditorSignalDecisions());
        }

        public void SubmitUiClick(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.UiClick, signal);
            SubmitSignal(signal, frame.Decisions);
        }

        public DeveloperCommentSignalDecision SubmitDeveloperCommentClick(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.DeveloperCommentClick, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.DeveloperComment;
        }

        public TerminalActionSignalDecision SubmitTerminalActionClick(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.TerminalActionClick, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.TerminalAction;
        }

        public MapGraphSelectionSignalDecision SubmitMapSelectionChanged(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.MapSelection, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.MapGraphSelection;
        }

        public MapGraphSelectionSignalDecision SubmitMapSelectionById(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.MapSelectionById, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.MapGraphSelection;
        }

        public MapGraphSelectionSignalDecision SubmitLinkSelectionChanged(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.LinkSelection, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.MapGraphSelection;
        }

        public MapGraphSelectionSignalDecision SubmitLinkSelectionByKey(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.LinkSelectionByKey, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.MapGraphSelection;
        }

        public MapCanvasToolModeSignalDecision SubmitMapCanvasToolModeChanged(UiSignal signal)
        {
            var frame = _signalWeaverHost.Submit(MapEditorSignalRoute.MapCanvasToolMode, signal);
            SubmitSignal(signal, frame.Decisions);
            return frame.Decisions.MapCanvasToolMode;
        }

        private void SubmitSignal(UiSignal signal, MapEditorSignalDecisions decisions)
        {
            if (signal == null)
                return;

            decisions = decisions ?? new MapEditorSignalDecisions();

            _producerSignals.Add(signal);
            _snapshot.SignalCount = _producerSignals.Count;
            _snapshot.LastSignalSummary = signal.ToSummary();
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
            ApplyDeveloperCommentDecision(decisions.DeveloperComment);
            ApplyTerminalActionDecision(decisions.TerminalAction);
            ApplyMapGraphSelectionDecision(decisions.MapGraphSelection);
            ApplyMapCanvasToolModeDecision(decisions.MapCanvasToolMode);
            ApplyMapProjectMutationDecision(decisions.MapProjectMutation);
            ApplyPersistenceActionDecision(decisions.PersistenceAction);
        }

        public void SetDeveloperCommentMode(bool enabled, UiSignal signal)
        {
            var frame = _signalWeaverHost.SetDeveloperCommentMode(enabled, signal);
            SubmitSignal(signal, frame.Decisions);
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void ConsumeDeveloperCommentOpenRequest()
        {
            _snapshot.DeveloperCommentOpenRequested = false;
            _snapshot.DeveloperCommentRequestSource = string.Empty;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void ConsumeTerminalActionRequest()
        {
            _snapshot.TerminalActionRequested = false;
            _snapshot.TerminalActionKey = string.Empty;
            _snapshot.TerminalActionRequestSource = string.Empty;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void ConsumePersistenceActionRequest()
        {
            _snapshot.PersistenceActionRequested = false;
            _snapshot.PersistenceActionKind = PersistenceActionKind.None;
            _snapshot.PersistenceActionKey = string.Empty;
            _snapshot.PersistenceActionRequestSource = string.Empty;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void ConsumeMapProjectMutationRequest()
        {
            _snapshot.MapProjectMutationRequested = false;
            _snapshot.MapProjectMutationKind = MapProjectMutationKind.None;
            _snapshot.MapProjectMutationActionKey = string.Empty;
            _snapshot.MapProjectMutationRequestSource = string.Empty;
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

        public void SetPinnedStartingMapPath(string scenePath)
        {
            _snapshot.PinnedStartingMapPath = NormalizeResPath(scenePath);
            RefreshProjectSnapshot();
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

        public void SelectMapById(string mapId)
        {
            mapId = (mapId ?? string.Empty).Trim();
            if (!HasCurrentProject || mapId.Length == 0)
                return;

            var index = _currentProject.Maps.FindIndex(map =>
                string.Equals((map.Id ?? string.Empty).Trim(), mapId, StringComparison.Ordinal) ||
                string.Equals((map.ScenePath ?? string.Empty).Trim(), mapId, StringComparison.Ordinal));
            if (index >= 0)
                SelectMapByIndex(index);
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

        public void SelectLink(MapLink link)
        {
            if (_currentProject == null || _currentProject.Links == null || link == null)
                return;

            var index = _currentProject.Links.FindIndex(item => ReferenceEquals(item, link));
            if (index >= 0)
                SelectLinkByIndex(index);
        }

        public void AddLink(MapLink link)
        {
            if (link == null)
                return;

            if (_currentProject == null)
                _currentProject = new MapProject();
            if (_currentProject.Links == null)
                _currentProject.Links = new List<MapLink>();

            _currentProject.Links.Add(link);
            _snapshot.SelectedLinkIndex = _currentProject.Links.Count - 1;
            RefreshProjectSnapshot();
            _snapshot.ProjectDirty = true;
            _snapshot.StatusText = "Added link: " + link.DisplayName;
            _snapshot.LastUpdatedAt = DateTimeOffset.Now;
        }

        public void RemoveSelectedLink()
        {
            var selected = SelectedLink;
            if (selected == null || _currentProject == null || _currentProject.Links == null)
                return;

            _currentProject.Links.Remove(selected);
            if (_snapshot.SelectedLinkIndex >= _currentProject.Links.Count)
                _snapshot.SelectedLinkIndex = _currentProject.Links.Count - 1;
            if (_currentProject.Links.Count == 0)
                _snapshot.SelectedLinkIndex = -1;

            RefreshProjectSnapshot();
            _snapshot.ProjectDirty = true;
            _snapshot.StatusText = "Deleted link: " + selected.DisplayName;
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

        public void MarkSelectedLinkEdited(string propertyName)
        {
            RefreshProjectSnapshot();
            _snapshot.ProjectDirty = true;
            _snapshot.StatusText = string.IsNullOrWhiteSpace(propertyName)
                ? "Selected link updated."
                : "Selected link updated: " + propertyName;
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
                .Select(FormatLinkName)
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
            _snapshot.DeveloperCommentState = _signalWeaverHost.States.DeveloperComment;
            _snapshot.DeveloperCommentModeEnabled = _snapshot.DeveloperCommentState.ModeEnabled;

            if (decision == null)
                return;

            _snapshot.DeveloperCommentOpenRequested = decision.OpenCommentBox;
            _snapshot.DeveloperCommentSourceSignalConsumed = decision.SourceSignalConsumed;
            _snapshot.DeveloperCommentRequestSource = decision.SourceDescription;
            _snapshot.StatusText = decision.StatusText;
        }

        private void ApplyTerminalActionDecision(TerminalActionSignalDecision decision)
        {
            _snapshot.TerminalActionState = _signalWeaverHost.States.TerminalAction;

            if (decision == null)
                return;

            _snapshot.TerminalActionRequested = decision.ExecuteAction;
            _snapshot.TerminalActionSourceSignalConsumed = decision.SourceSignalConsumed;
            _snapshot.TerminalActionKey = decision.ActionKey;
            _snapshot.TerminalActionRequestSource = decision.SourceDescription;
            if (decision.ExecuteAction)
                _snapshot.StatusText = decision.StatusText;
        }

        private void ApplyMapGraphSelectionDecision(MapGraphSelectionSignalDecision decision)
        {
            _snapshot.MapGraphSelectionState = _signalWeaverHost.States.MapGraphSelection;

            if (decision == null || !decision.ApplySelection)
                return;

            if (decision.Target == MapGraphSelectionTarget.Map)
            {
                if (!string.IsNullOrWhiteSpace(decision.SelectedKey))
                    SelectMapById(decision.SelectedKey);
                else
                    SelectMapByIndex(decision.SelectedIndex);
            }
            else if (decision.Target == MapGraphSelectionTarget.Link)
            {
                if (!string.IsNullOrWhiteSpace(decision.SelectedKey))
                    SelectLinkByKey(decision.SelectedKey);
                else
                    SelectLinkByIndex(decision.SelectedIndex);
            }
        }

        private void ApplyMapCanvasToolModeDecision(MapCanvasToolModeSignalDecision decision)
        {
            _snapshot.MapCanvasToolModeState = _signalWeaverHost.States.MapCanvasToolMode;

            if (decision == null || !decision.ApplyToolMode)
                return;

            _snapshot.StatusText = decision.StatusText;
        }

        private void ApplyPersistenceActionDecision(PersistenceActionSignalDecision decision)
        {
            _snapshot.PersistenceActionState = _signalWeaverHost.States.PersistenceAction;

            if (decision == null)
                return;

            _snapshot.PersistenceActionRequested = decision.RequestAction;
            _snapshot.PersistenceActionSourceSignalConsumed = decision.SourceSignalConsumed;
            _snapshot.PersistenceActionKind = decision.ActionKind;
            _snapshot.PersistenceActionKey = decision.ActionKey;
            _snapshot.PersistenceActionRequestSource = decision.SourceDescription;
        }

        private void ApplyMapProjectMutationDecision(MapProjectMutationSignalDecision decision)
        {
            _snapshot.MapProjectMutationState = _signalWeaverHost.States.MapProjectMutation;

            if (decision == null)
                return;

            _snapshot.MapProjectMutationRequested = decision.RequestMutation;
            _snapshot.MapProjectMutationSourceSignalConsumed = decision.SourceSignalConsumed;
            _snapshot.MapProjectMutationKind = decision.MutationKind;
            _snapshot.MapProjectMutationActionKey = decision.ActionKey;
            _snapshot.MapProjectMutationRequestSource = decision.SourceDescription;
        }

        private void SelectLinkByKey(string linkKey)
        {
            if (_currentProject == null || _currentProject.Links == null)
                return;

            var index = _currentProject.Links.FindIndex(link =>
                string.Equals(BuildLinkKey(link), linkKey, StringComparison.Ordinal));
            if (index >= 0)
                SelectLinkByIndex(index);
        }

        public static string BuildLinkKey(MapLink link)
        {
            if (link == null || link.From == null)
                return string.Empty;

            return (link.From.MapId ?? string.Empty).Trim() + "|" + (link.From.PortalId ?? string.Empty).Trim();
        }

        private string FormatMapName(MapDefinition map)
        {
            if (map == null)
                return string.Empty;

            var name = !string.IsNullOrWhiteSpace(map.DisplayName)
                ? map.DisplayName.Trim()
                : Path.GetFileNameWithoutExtension(NormalizeResPath(map.ScenePath));
            if (string.IsNullOrWhiteSpace(name))
                name = NormalizeResPath(map.ScenePath);

            return IsPinnedStartingMap(map) ? "[Pinned] " + name : name;
        }

        private string FormatLinkName(MapLink link)
        {
            if (link == null)
                return string.Empty;

            return FormatMapReferenceName(link.From == null ? string.Empty : link.From.MapId) +
                " -> " +
                FormatMapReferenceName(link.To == null ? string.Empty : link.To.MapId);
        }

        private string FormatMapReferenceName(string mapId)
        {
            var normalized = NormalizeResPath(mapId);
            if (_currentProject != null && _currentProject.Maps != null)
            {
                var map = _currentProject.Maps.FirstOrDefault(candidate =>
                    string.Equals(NormalizeResPath(candidate == null ? string.Empty : candidate.Id), normalized, StringComparison.Ordinal) ||
                    string.Equals(NormalizeResPath(candidate == null ? string.Empty : candidate.ScenePath), normalized, StringComparison.Ordinal));
                if (map != null && !string.IsNullOrWhiteSpace(map.DisplayName))
                    return map.DisplayName.Trim();
            }

            var fileName = Path.GetFileNameWithoutExtension(normalized);
            return string.IsNullOrWhiteSpace(fileName) ? normalized : fileName;
        }

        private bool IsPinnedStartingMap(MapDefinition map)
        {
            var scenePath = NormalizeResPath(map == null ? string.Empty : map.ScenePath);
            return scenePath.Length > 0 &&
                string.Equals(scenePath, _snapshot.PinnedStartingMapPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeResPath(string value)
        {
            value = (value ?? string.Empty).Trim().Replace('\\', '/');
            if (value.Length == 0)
                return string.Empty;
            if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return "res://" + value.Substring("res://".Length).TrimStart('/');
            return value;
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
