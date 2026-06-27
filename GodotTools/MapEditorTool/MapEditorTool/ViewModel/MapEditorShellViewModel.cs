using System;
using System.Collections.Generic;
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

        private MapEditorShellViewModel()
        {
            _snapshot = new MapEditorUiSnapshot
            {
                DeveloperCommentModeEnabled = false,
                StatusText = "MapEditorTool shell ready. Enable Developer Comment Mode to collect UI feedback.",
                LastUpdatedAt = DateTimeOffset.Now,
                MapNames = new[]
                {
                    "Sample Map - Forest Entrance",
                    "Sample Map - Corridor",
                    "Sample Map - Boss Room"
                },
                LinkNames = new[]
                {
                    "Forest Entrance -> Corridor",
                    "Corridor -> Boss Room"
                },
                MapState = new MapShellState(),
                LinkState = new LinkShellState(),
                DeveloperCommentState = _developerCommentMachine.State
            };
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
    }
}
