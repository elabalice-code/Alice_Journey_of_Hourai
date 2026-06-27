using System;
using System.Collections.Generic;
using System.Globalization;
using ActorFramework;

namespace AIAssistant.Customizor.Main
{
    // Design boundary:
    // SignalTickFrame only carries simple tick signals: bool, numeric values, enums,
    // and small scalar identifiers. Arrays, structs, and custom objects stay outside
    // this signal layer.
    //
    // Complex -> simple:
    // owner or recognizer Executors must reduce complex data into feature signals
    // before this Customizor signal round begins.
    //
    // Simple -> complex:
    // later dedicated Executors must expand consumer feature signals into complex
    // data, rendering payloads, or playback payloads after this signal round ends.
    //
    // Feature isolation:
    // independent Issues, Ideas, Features, and Featurors must not share private
    // intermediate signal variables. If a signal must be shared by multiple modules,
    // promote it to Workplace first, then expose it here as a normal producer or
    // consumer field.
    //
    // Self-check:
    // consumer outputs use SignalSlot<T>. Duplicate same-value writes are recorded.
    // conflicting different-value writes are recorded and rejected so they cannot be
    // applied back to Workplace silently.
    //
    // Producer/consumer coverage:
    // producer reads must go through SignalSlot<T>.TryRead(...) so missing producers
    // are diagnosed. Consumer slots must be marked as consumed by the write-back
    // stage, otherwise they are reported as unconsumed outputs.
    // This catches relations that accidentally depend on an unwritten producer, and
    // outputs that were calculated but never applied to a real Workplace consumer.
    public sealed class SignalTickFrame
    {
        public SignalTickFrame()
        {
            Diagnostics = new SignalDiagnostics();
            Producers = new CustomizeProducers(Diagnostics);
            Consumers = new CustomizeConsumers(Diagnostics);
        }

        public SignalDiagnostics Diagnostics { get; }
        public CustomizeProducers Producers { get; }
        public CustomizeConsumers Consumers { get; }
    }

    public sealed class CustomizeProducers
    {
        internal CustomizeProducers(SignalDiagnostics diagnostics)
        {
            Workplace = new WorkplaceProducerSignals(diagnostics);
            Debug = new DebugProducerSignals(diagnostics);
            Customizor = new CustomizorProducerSignals(diagnostics);
            Kws = new KwsProducerSignals(diagnostics);
            CentralControl = new CentralControlProducerSignals(diagnostics);
            AI = new AiProducerSignals(diagnostics);
            VirtualCamera = new VirtualCameraProducerSignals(diagnostics);
            TestTool = new TestToolProducerSignals(diagnostics);
        }

        public WorkplaceProducerSignals Workplace { get; }
        public DebugProducerSignals Debug { get; }
        public CustomizorProducerSignals Customizor { get; }
        public KwsProducerSignals Kws { get; }
        public CentralControlProducerSignals CentralControl { get; }
        public AiProducerSignals AI { get; }
        public VirtualCameraProducerSignals VirtualCamera { get; }
        public TestToolProducerSignals TestTool { get; }

        public SignalSlot<bool> AiEnabled { get { return AI.Enabled; } }
        public SignalSlot<bool> AiUseDeckMechanism { get { return AI.UseDeckMechanism; } }
    }

    public sealed class CustomizeConsumers
    {
        internal CustomizeConsumers(SignalDiagnostics diagnostics)
        {
            CentralControl = new CentralControlConsumerSignals(diagnostics);
            AI = new AiConsumerSignals(diagnostics);
            VirtualCamera = new VirtualCameraConsumerSignals(diagnostics);
            TestTool = new TestToolConsumerSignals(diagnostics);
        }

        public CentralControlConsumerSignals CentralControl { get; }
        public AiConsumerSignals AI { get; }
        public VirtualCameraConsumerSignals VirtualCamera { get; }
        public TestToolConsumerSignals TestTool { get; }

        public SignalSlot<bool> VirtualCameraEnableTextHighlight
        {
            get { return VirtualCamera.EnableTextHighlight; }
        }

        internal void AuditUnconsumed()
        {
            CentralControl.AuditUnconsumed();
            AI.AuditUnconsumed();
            VirtualCamera.AuditUnconsumed();
            TestTool.AuditUnconsumed();
        }
    }

    public sealed class WorkplaceProducerSignals
    {
        internal WorkplaceProducerSignals(SignalDiagnostics diagnostics)
        {
            SrcType = new SignalSlot<DriveSrcType>("Workplace.SrcType", diagnostics);
            FragmentSampleRate = new SignalSlot<int>("Workplace.FragmentSampleRate", diagnostics);
            FragmentChannels = new SignalSlot<short>("Workplace.FragmentChannels", diagnostics);
        }

        public SignalSlot<DriveSrcType> SrcType { get; }
        public SignalSlot<int> FragmentSampleRate { get; }
        public SignalSlot<short> FragmentChannels { get; }
    }

    public sealed class DebugProducerSignals
    {
        internal DebugProducerSignals(SignalDiagnostics diagnostics)
        {
            EnableActorLogs = new SignalSlot<bool>("Workplace.Debug.EnableActorLogs", diagnostics);
            EnableVirtualCameraLogs = new SignalSlot<bool>("Workplace.Debug.EnableVirtualCameraLogs", diagnostics);
            EnableMultimodalLogs = new SignalSlot<bool>("Workplace.Debug.EnableMultimodalLogs", diagnostics);
            EnableTestActorLogs = new SignalSlot<bool>("Workplace.Debug.EnableTestActorLogs", diagnostics);
            EnableAiChatLogs = new SignalSlot<bool>("Workplace.Debug.EnableAiChatLogs", diagnostics);
            EnableAiAgentLogs = new SignalSlot<bool>("Workplace.Debug.EnableAiAgentLogs", diagnostics);
            EnableGreenScreenChecker = new SignalSlot<bool>("Workplace.Debug.EnableGreenScreenChecker", diagnostics);
            EnableAecAudioDump = new SignalSlot<bool>("Workplace.Debug.EnableAecAudioDump", diagnostics);
        }

        public SignalSlot<bool> EnableActorLogs { get; }
        public SignalSlot<bool> EnableVirtualCameraLogs { get; }
        public SignalSlot<bool> EnableMultimodalLogs { get; }
        public SignalSlot<bool> EnableTestActorLogs { get; }
        public SignalSlot<bool> EnableAiChatLogs { get; }
        public SignalSlot<bool> EnableAiAgentLogs { get; }
        public SignalSlot<bool> EnableGreenScreenChecker { get; }
        public SignalSlot<bool> EnableAecAudioDump { get; }
    }

    public sealed class CustomizorProducerSignals
    {
        internal CustomizorProducerSignals(SignalDiagnostics diagnostics)
        {
            Enabled = new SignalSlot<bool>("Workplace.Customizor.Enabled", diagnostics);
        }

        public SignalSlot<bool> Enabled { get; }
    }

    public sealed class KwsProducerSignals
    {
        internal KwsProducerSignals(SignalDiagnostics diagnostics)
        {
            Result = new SignalSlot<KwsResult>("Workplace.KWS.Result", diagnostics);
        }

        public SignalSlot<KwsResult> Result { get; }
    }

    public sealed class CentralControlProducerSignals
    {
        internal CentralControlProducerSignals(SignalDiagnostics diagnostics)
        {
            Mode = new SignalSlot<CentralControlMode>("Workplace.CentralControl.Mode", diagnostics);
            ActiveUntilUnixMs = new SignalSlot<long>("Workplace.CentralControl.ActiveUntilUnixMs", diagnostics);
            PendingThanksClose = new SignalSlot<bool>("Workplace.CentralControl.PendingThanksClose", diagnostics);
            PendingThanksCloseSinceUnixMs =
                new SignalSlot<long>("Workplace.CentralControl.PendingThanksCloseSinceUnixMs", diagnostics);
            PendingThanksCloseDeferred =
                new SignalSlot<bool>("Workplace.CentralControl.PendingThanksCloseDeferred", diagnostics);
            TimeoutSeconds = new SignalSlot<int>("Workplace.CentralControl.TimeoutSeconds", diagnostics);
        }

        public SignalSlot<CentralControlMode> Mode { get; }
        public SignalSlot<long> ActiveUntilUnixMs { get; }
        public SignalSlot<bool> PendingThanksClose { get; }
        public SignalSlot<long> PendingThanksCloseSinceUnixMs { get; }
        public SignalSlot<bool> PendingThanksCloseDeferred { get; }
        public SignalSlot<int> TimeoutSeconds { get; }
    }

    public sealed class AiProducerSignals
    {
        internal AiProducerSignals(SignalDiagnostics diagnostics)
        {
            Backend = new SignalSlot<AiBackend>("Workplace.AI.Backend", diagnostics);
            Enabled = new SignalSlot<bool>("Workplace.AI.Enabled", diagnostics);
            TaskType = new SignalSlot<AiTaskType>("Workplace.AI.TaskType", diagnostics);
            TaskRequestId = new SignalSlot<long>("Workplace.AI.TaskRequestId", diagnostics);
            DomainWinner = new SignalSlot<string>("Workplace.AI.DomainWinner", diagnostics);
            DomainPriority = new SignalSlot<int>("Workplace.AI.DomainPriority", diagnostics);
            DomainPendingMedia = new SignalSlot<bool>("Workplace.AI.DomainPendingMedia", diagnostics);
            UseLegacyLocalTurnControl = new SignalSlot<bool>("Workplace.AI.UseLegacyLocalTurnControl", diagnostics);
            ChatOutputsMuted = new SignalSlot<bool>("Workplace.AI.ChatOutputsMuted", diagnostics);
            UseDeckMechanism = new SignalSlot<bool>("Workplace.AI.UseDeckMechanism", diagnostics);
            OfflineWavTestDirectOn = new SignalSlot<bool>("Workplace.AI.OfflineWavTestDirectOn", diagnostics);
        }

        public SignalSlot<AiBackend> Backend { get; }
        public SignalSlot<bool> Enabled { get; }
        public SignalSlot<AiTaskType> TaskType { get; }
        public SignalSlot<long> TaskRequestId { get; }
        public SignalSlot<string> DomainWinner { get; }
        public SignalSlot<int> DomainPriority { get; }
        public SignalSlot<bool> DomainPendingMedia { get; }
        public SignalSlot<bool> UseLegacyLocalTurnControl { get; }
        public SignalSlot<bool> ChatOutputsMuted { get; }
        public SignalSlot<bool> UseDeckMechanism { get; }
        public SignalSlot<bool> OfflineWavTestDirectOn { get; }
    }

    public sealed class VirtualCameraProducerSignals
    {
        internal VirtualCameraProducerSignals(SignalDiagnostics diagnostics)
        {
            Mode = new SignalSlot<VirtualCameraMode>("Workplace.VirtualCamera.Mode", diagnostics);
            Resolution = new SignalSlot<VirtualCameraResolutionPreset>("Workplace.VirtualCamera.Resolution", diagnostics);
            EnableNoDriverCamera = new SignalSlot<bool>("Workplace.VirtualCamera.EnableNoDriverCamera", diagnostics);
            SuppressTextOverlay = new SignalSlot<bool>("Workplace.VirtualCamera.SuppressTextOverlay", diagnostics);
            LoopPlayback = new SignalSlot<bool>("Workplace.VirtualCamera.LoopPlayback", diagnostics);
            MaxFrames = new SignalSlot<int>("Workplace.VirtualCamera.MaxFrames", diagnostics);
            AgentForceBubble = new SignalSlot<bool>("Workplace.VirtualCamera.AgentForceBubble", diagnostics);
            RequestedMode = new SignalSlot<VirtualCameraMode>("Workplace.VirtualCamera.RequestedMode", diagnostics);
            StreamForceCamera = new SignalSlot<bool>("Workplace.VirtualCamera.StreamForceCamera", diagnostics);
            ClearRetainedFrameOnApply =
                new SignalSlot<bool>("Workplace.VirtualCamera.ClearRetainedFrameOnApply", diagnostics);
            ClearTextOverlayOnApply =
                new SignalSlot<bool>("Workplace.VirtualCamera.ClearTextOverlayOnApply", diagnostics);
            ResetAnimationOnApply = new SignalSlot<bool>("Workplace.VirtualCamera.ResetAnimationOnApply", diagnostics);
            HintLayerWorking = new SignalSlot<bool>("Workplace.VirtualCamera.HintLayerWorking", diagnostics);
            AgentLayerWorking = new SignalSlot<bool>("Workplace.VirtualCamera.AgentLayerWorking", diagnostics);
            ForwarderLayerWorking = new SignalSlot<bool>("Workplace.VirtualCamera.ForwarderLayerWorking", diagnostics);
            ForwarderClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.ForwarderClaimActive", diagnostics);
            ForwarderClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.ForwarderClaimPriority", diagnostics);
            MicClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.MicClaimActive", diagnostics);
            MicClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.MicClaimPriority", diagnostics);
            HintClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.HintClaimActive", diagnostics);
            HintClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.HintClaimPriority", diagnostics);
            AiAgentClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.AiAgentClaimActive", diagnostics);
            AiAgentClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.AiAgentClaimPriority", diagnostics);
            ArchiveListClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.ArchiveListClaimActive", diagnostics);
            ArchiveListClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.ArchiveListClaimPriority", diagnostics);
            ImageClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.ImageClaimActive", diagnostics);
            ImageClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.ImageClaimPriority", diagnostics);
            VideoClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.VideoClaimActive", diagnostics);
            VideoClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.VideoClaimPriority", diagnostics);
            VideoPlaybackPauseEligible =
                new SignalSlot<bool>("Workplace.VirtualCamera.VideoPlaybackPauseEligible", diagnostics);
            VideoPlaybackPaused = new SignalSlot<bool>("Workplace.VirtualCamera.VideoPlaybackPaused", diagnostics);
            VoteWinnerName = new SignalSlot<string>("Workplace.VirtualCamera.VoteWinnerName", diagnostics);
            VoteWinnerPriority = new SignalSlot<int>("Workplace.VirtualCamera.VoteWinnerPriority", diagnostics);
            DominantLayerPriority = new SignalSlot<int>("Workplace.VirtualCamera.DominantLayerPriority", diagnostics);
            DominantLayerName = new SignalSlot<string>("Workplace.VirtualCamera.DominantLayerName", diagnostics);
            EnableTextHighlight = new SignalSlot<bool>("Workplace.VirtualCamera.EnableTextHighlight", diagnostics);
            HasAiText = new SignalSlot<bool>("Workplace.VirtualCamera.HasAiText", diagnostics);
            HasOverlayText = new SignalSlot<bool>("Workplace.VirtualCamera.HasOverlayText", diagnostics);
            HasHintText = new SignalSlot<bool>("Workplace.VirtualCamera.HasHintText", diagnostics);
        }

        public SignalSlot<VirtualCameraMode> Mode { get; }
        public SignalSlot<VirtualCameraResolutionPreset> Resolution { get; }
        public SignalSlot<bool> EnableNoDriverCamera { get; }
        public SignalSlot<bool> SuppressTextOverlay { get; }
        public SignalSlot<bool> LoopPlayback { get; }
        public SignalSlot<int> MaxFrames { get; }
        public SignalSlot<bool> AgentForceBubble { get; }
        public SignalSlot<VirtualCameraMode> RequestedMode { get; }
        public SignalSlot<bool> StreamForceCamera { get; }
        public SignalSlot<bool> ClearRetainedFrameOnApply { get; }
        public SignalSlot<bool> ClearTextOverlayOnApply { get; }
        public SignalSlot<bool> ResetAnimationOnApply { get; }
        public SignalSlot<bool> HintLayerWorking { get; }
        public SignalSlot<bool> AgentLayerWorking { get; }
        public SignalSlot<bool> ForwarderLayerWorking { get; }
        public SignalSlot<bool> ForwarderClaimActive { get; }
        public SignalSlot<int> ForwarderClaimPriority { get; }
        public SignalSlot<bool> MicClaimActive { get; }
        public SignalSlot<int> MicClaimPriority { get; }
        public SignalSlot<bool> HintClaimActive { get; }
        public SignalSlot<int> HintClaimPriority { get; }
        public SignalSlot<bool> AiAgentClaimActive { get; }
        public SignalSlot<int> AiAgentClaimPriority { get; }
        public SignalSlot<bool> ArchiveListClaimActive { get; }
        public SignalSlot<int> ArchiveListClaimPriority { get; }
        public SignalSlot<bool> ImageClaimActive { get; }
        public SignalSlot<int> ImageClaimPriority { get; }
        public SignalSlot<bool> VideoClaimActive { get; }
        public SignalSlot<int> VideoClaimPriority { get; }
        public SignalSlot<bool> VideoPlaybackPauseEligible { get; }
        public SignalSlot<bool> VideoPlaybackPaused { get; }
        public SignalSlot<string> VoteWinnerName { get; }
        public SignalSlot<int> VoteWinnerPriority { get; }
        public SignalSlot<int> DominantLayerPriority { get; }
        public SignalSlot<string> DominantLayerName { get; }
        public SignalSlot<bool> EnableTextHighlight { get; }
        public SignalSlot<bool> HasAiText { get; }
        public SignalSlot<bool> HasOverlayText { get; }
        public SignalSlot<bool> HasHintText { get; }
    }

    public sealed class TestToolProducerSignals
    {
        internal TestToolProducerSignals(SignalDiagnostics diagnostics)
        {
            EnableFastCameraCapture = new SignalSlot<bool>("Workplace.TestTool.EnableFastCameraCapture", diagnostics);
            CaptureCameraName = new SignalSlot<string>("Workplace.TestTool.CaptureCameraName", diagnostics);
            CaptureRawPreferred = new SignalSlot<bool>("Workplace.TestTool.CaptureRawPreferred", diagnostics);
            CaptureIntervalMs = new SignalSlot<int>("Workplace.TestTool.CaptureIntervalMs", diagnostics);
            LastCaptureUnixMs = new SignalSlot<long>("Workplace.TestTool.LastCaptureUnixMs", diagnostics);
        }

        public SignalSlot<bool> EnableFastCameraCapture { get; }
        public SignalSlot<string> CaptureCameraName { get; }
        public SignalSlot<bool> CaptureRawPreferred { get; }
        public SignalSlot<int> CaptureIntervalMs { get; }
        public SignalSlot<long> LastCaptureUnixMs { get; }
    }

    public sealed class CentralControlConsumerSignals
    {
        internal CentralControlConsumerSignals(SignalDiagnostics diagnostics)
        {
            Mode = new SignalSlot<CentralControlMode>("Workplace.CentralControl.Mode", diagnostics);
            ActiveUntilUnixMs = new SignalSlot<long>("Workplace.CentralControl.ActiveUntilUnixMs", diagnostics);
            PendingThanksClose = new SignalSlot<bool>("Workplace.CentralControl.PendingThanksClose", diagnostics);
            PendingThanksCloseSinceUnixMs =
                new SignalSlot<long>("Workplace.CentralControl.PendingThanksCloseSinceUnixMs", diagnostics);
            PendingThanksCloseDeferred =
                new SignalSlot<bool>("Workplace.CentralControl.PendingThanksCloseDeferred", diagnostics);
            TimeoutSeconds = new SignalSlot<int>("Workplace.CentralControl.TimeoutSeconds", diagnostics);
        }

        public SignalSlot<CentralControlMode> Mode { get; }
        public SignalSlot<long> ActiveUntilUnixMs { get; }
        public SignalSlot<bool> PendingThanksClose { get; }
        public SignalSlot<long> PendingThanksCloseSinceUnixMs { get; }
        public SignalSlot<bool> PendingThanksCloseDeferred { get; }
        public SignalSlot<int> TimeoutSeconds { get; }

        internal void AuditUnconsumed()
        {
            Mode.AuditUnconsumedConsumer();
            ActiveUntilUnixMs.AuditUnconsumedConsumer();
            PendingThanksClose.AuditUnconsumedConsumer();
            PendingThanksCloseSinceUnixMs.AuditUnconsumedConsumer();
            PendingThanksCloseDeferred.AuditUnconsumedConsumer();
            TimeoutSeconds.AuditUnconsumedConsumer();
        }
    }

    public sealed class AiConsumerSignals
    {
        internal AiConsumerSignals(SignalDiagnostics diagnostics)
        {
            Backend = new SignalSlot<AiBackend>("Workplace.AI.Backend", diagnostics);
            Enabled = new SignalSlot<bool>("Workplace.AI.Enabled", diagnostics);
            TaskType = new SignalSlot<AiTaskType>("Workplace.AI.TaskType", diagnostics);
            TaskRequestId = new SignalSlot<long>("Workplace.AI.TaskRequestId", diagnostics);
            DomainWinner = new SignalSlot<string>("Workplace.AI.DomainWinner", diagnostics);
            DomainPriority = new SignalSlot<int>("Workplace.AI.DomainPriority", diagnostics);
            DomainPendingMedia = new SignalSlot<bool>("Workplace.AI.DomainPendingMedia", diagnostics);
            UseLegacyLocalTurnControl = new SignalSlot<bool>("Workplace.AI.UseLegacyLocalTurnControl", diagnostics);
            ChatOutputsMuted = new SignalSlot<bool>("Workplace.AI.ChatOutputsMuted", diagnostics);
            UseDeckMechanism = new SignalSlot<bool>("Workplace.AI.UseDeckMechanism", diagnostics);
            OfflineWavTestDirectOn = new SignalSlot<bool>("Workplace.AI.OfflineWavTestDirectOn", diagnostics);
        }

        public SignalSlot<AiBackend> Backend { get; }
        public SignalSlot<bool> Enabled { get; }
        public SignalSlot<AiTaskType> TaskType { get; }
        public SignalSlot<long> TaskRequestId { get; }
        public SignalSlot<string> DomainWinner { get; }
        public SignalSlot<int> DomainPriority { get; }
        public SignalSlot<bool> DomainPendingMedia { get; }
        public SignalSlot<bool> UseLegacyLocalTurnControl { get; }
        public SignalSlot<bool> ChatOutputsMuted { get; }
        public SignalSlot<bool> UseDeckMechanism { get; }
        public SignalSlot<bool> OfflineWavTestDirectOn { get; }

        internal void AuditUnconsumed()
        {
            Backend.AuditUnconsumedConsumer();
            Enabled.AuditUnconsumedConsumer();
            TaskType.AuditUnconsumedConsumer();
            TaskRequestId.AuditUnconsumedConsumer();
            DomainWinner.AuditUnconsumedConsumer();
            DomainPriority.AuditUnconsumedConsumer();
            DomainPendingMedia.AuditUnconsumedConsumer();
            UseLegacyLocalTurnControl.AuditUnconsumedConsumer();
            ChatOutputsMuted.AuditUnconsumedConsumer();
            UseDeckMechanism.AuditUnconsumedConsumer();
            OfflineWavTestDirectOn.AuditUnconsumedConsumer();
        }
    }

    public sealed class VirtualCameraConsumerSignals
    {
        internal VirtualCameraConsumerSignals(SignalDiagnostics diagnostics)
        {
            Mode = new SignalSlot<VirtualCameraMode>("Workplace.VirtualCamera.Mode", diagnostics);
            Resolution = new SignalSlot<VirtualCameraResolutionPreset>("Workplace.VirtualCamera.Resolution", diagnostics);
            EnableNoDriverCamera = new SignalSlot<bool>("Workplace.VirtualCamera.EnableNoDriverCamera", diagnostics);
            SuppressTextOverlay = new SignalSlot<bool>("Workplace.VirtualCamera.SuppressTextOverlay", diagnostics);
            LoopPlayback = new SignalSlot<bool>("Workplace.VirtualCamera.LoopPlayback", diagnostics);
            MaxFrames = new SignalSlot<int>("Workplace.VirtualCamera.MaxFrames", diagnostics);
            AgentForceBubble = new SignalSlot<bool>("Workplace.VirtualCamera.AgentForceBubble", diagnostics);
            RequestedMode = new SignalSlot<VirtualCameraMode>("Workplace.VirtualCamera.RequestedMode", diagnostics);
            StreamForceCamera = new SignalSlot<bool>("Workplace.VirtualCamera.StreamForceCamera", diagnostics);
            ClearRetainedFrameOnApply =
                new SignalSlot<bool>("Workplace.VirtualCamera.ClearRetainedFrameOnApply", diagnostics);
            ClearTextOverlayOnApply =
                new SignalSlot<bool>("Workplace.VirtualCamera.ClearTextOverlayOnApply", diagnostics);
            ResetAnimationOnApply = new SignalSlot<bool>("Workplace.VirtualCamera.ResetAnimationOnApply", diagnostics);
            ForwarderClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.ForwarderClaimActive", diagnostics);
            ForwarderClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.ForwarderClaimPriority", diagnostics);
            MicClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.MicClaimActive", diagnostics);
            MicClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.MicClaimPriority", diagnostics);
            HintClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.HintClaimActive", diagnostics);
            HintClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.HintClaimPriority", diagnostics);
            AiAgentClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.AiAgentClaimActive", diagnostics);
            AiAgentClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.AiAgentClaimPriority", diagnostics);
            ArchiveListClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.ArchiveListClaimActive", diagnostics);
            ArchiveListClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.ArchiveListClaimPriority", diagnostics);
            ImageClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.ImageClaimActive", diagnostics);
            ImageClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.ImageClaimPriority", diagnostics);
            VideoClaimActive = new SignalSlot<bool>("Workplace.VirtualCamera.VideoClaimActive", diagnostics);
            VideoClaimPriority = new SignalSlot<int>("Workplace.VirtualCamera.VideoClaimPriority", diagnostics);
            VideoPlaybackPauseEligible =
                new SignalSlot<bool>("Workplace.VirtualCamera.VideoPlaybackPauseEligible", diagnostics);
            VideoPlaybackPaused = new SignalSlot<bool>("Workplace.VirtualCamera.VideoPlaybackPaused", diagnostics);
            EnableTextHighlight = new SignalSlot<bool>("Workplace.VirtualCamera.EnableTextHighlight", diagnostics);
        }

        public SignalSlot<VirtualCameraMode> Mode { get; }
        public SignalSlot<VirtualCameraResolutionPreset> Resolution { get; }
        public SignalSlot<bool> EnableNoDriverCamera { get; }
        public SignalSlot<bool> SuppressTextOverlay { get; }
        public SignalSlot<bool> LoopPlayback { get; }
        public SignalSlot<int> MaxFrames { get; }
        public SignalSlot<bool> AgentForceBubble { get; }
        public SignalSlot<VirtualCameraMode> RequestedMode { get; }
        public SignalSlot<bool> StreamForceCamera { get; }
        public SignalSlot<bool> ClearRetainedFrameOnApply { get; }
        public SignalSlot<bool> ClearTextOverlayOnApply { get; }
        public SignalSlot<bool> ResetAnimationOnApply { get; }
        public SignalSlot<bool> ForwarderClaimActive { get; }
        public SignalSlot<int> ForwarderClaimPriority { get; }
        public SignalSlot<bool> MicClaimActive { get; }
        public SignalSlot<int> MicClaimPriority { get; }
        public SignalSlot<bool> HintClaimActive { get; }
        public SignalSlot<int> HintClaimPriority { get; }
        public SignalSlot<bool> AiAgentClaimActive { get; }
        public SignalSlot<int> AiAgentClaimPriority { get; }
        public SignalSlot<bool> ArchiveListClaimActive { get; }
        public SignalSlot<int> ArchiveListClaimPriority { get; }
        public SignalSlot<bool> ImageClaimActive { get; }
        public SignalSlot<int> ImageClaimPriority { get; }
        public SignalSlot<bool> VideoClaimActive { get; }
        public SignalSlot<int> VideoClaimPriority { get; }
        public SignalSlot<bool> VideoPlaybackPauseEligible { get; }
        public SignalSlot<bool> VideoPlaybackPaused { get; }
        public SignalSlot<bool> EnableTextHighlight { get; }

        internal void AuditUnconsumed()
        {
            Mode.AuditUnconsumedConsumer();
            Resolution.AuditUnconsumedConsumer();
            EnableNoDriverCamera.AuditUnconsumedConsumer();
            SuppressTextOverlay.AuditUnconsumedConsumer();
            LoopPlayback.AuditUnconsumedConsumer();
            MaxFrames.AuditUnconsumedConsumer();
            AgentForceBubble.AuditUnconsumedConsumer();
            RequestedMode.AuditUnconsumedConsumer();
            StreamForceCamera.AuditUnconsumedConsumer();
            ClearRetainedFrameOnApply.AuditUnconsumedConsumer();
            ClearTextOverlayOnApply.AuditUnconsumedConsumer();
            ResetAnimationOnApply.AuditUnconsumedConsumer();
            ForwarderClaimActive.AuditUnconsumedConsumer();
            ForwarderClaimPriority.AuditUnconsumedConsumer();
            MicClaimActive.AuditUnconsumedConsumer();
            MicClaimPriority.AuditUnconsumedConsumer();
            HintClaimActive.AuditUnconsumedConsumer();
            HintClaimPriority.AuditUnconsumedConsumer();
            AiAgentClaimActive.AuditUnconsumedConsumer();
            AiAgentClaimPriority.AuditUnconsumedConsumer();
            ArchiveListClaimActive.AuditUnconsumedConsumer();
            ArchiveListClaimPriority.AuditUnconsumedConsumer();
            ImageClaimActive.AuditUnconsumedConsumer();
            ImageClaimPriority.AuditUnconsumedConsumer();
            VideoClaimActive.AuditUnconsumedConsumer();
            VideoClaimPriority.AuditUnconsumedConsumer();
            VideoPlaybackPauseEligible.AuditUnconsumedConsumer();
            VideoPlaybackPaused.AuditUnconsumedConsumer();
            EnableTextHighlight.AuditUnconsumedConsumer();
        }
    }

    public sealed class TestToolConsumerSignals
    {
        internal TestToolConsumerSignals(SignalDiagnostics diagnostics)
        {
            EnableFastCameraCapture = new SignalSlot<bool>("Workplace.TestTool.EnableFastCameraCapture", diagnostics);
            CaptureCameraName = new SignalSlot<string>("Workplace.TestTool.CaptureCameraName", diagnostics);
            CaptureRawPreferred = new SignalSlot<bool>("Workplace.TestTool.CaptureRawPreferred", diagnostics);
            CaptureIntervalMs = new SignalSlot<int>("Workplace.TestTool.CaptureIntervalMs", diagnostics);
            LastCaptureUnixMs = new SignalSlot<long>("Workplace.TestTool.LastCaptureUnixMs", diagnostics);
        }

        public SignalSlot<bool> EnableFastCameraCapture { get; }
        public SignalSlot<string> CaptureCameraName { get; }
        public SignalSlot<bool> CaptureRawPreferred { get; }
        public SignalSlot<int> CaptureIntervalMs { get; }
        public SignalSlot<long> LastCaptureUnixMs { get; }

        internal void AuditUnconsumed()
        {
            EnableFastCameraCapture.AuditUnconsumedConsumer();
            CaptureCameraName.AuditUnconsumedConsumer();
            CaptureRawPreferred.AuditUnconsumedConsumer();
            CaptureIntervalMs.AuditUnconsumedConsumer();
            LastCaptureUnixMs.AuditUnconsumedConsumer();
        }
    }

    public sealed class SignalSlot<T>
    {
        private readonly SignalDiagnostics _diagnostics;
        private readonly List<SignalWriteRecord> _writes = new List<SignalWriteRecord>();
        private readonly List<string> _readers = new List<string>();
        private readonly List<string> _consumers = new List<string>();

        public SignalSlot(string name, SignalDiagnostics diagnostics)
        {
            Name = name ?? string.Empty;
            _diagnostics = diagnostics;
        }

        public string Name { get; }
        public bool HasValue { get; private set; }
        public bool HasConflict { get; private set; }
        public bool WasConsumed { get { return _consumers.Count > 0; } }
        public T Value { get; private set; }
        public string Writer { get; private set; }
        public IList<SignalWriteRecord> Writes { get { return _writes.AsReadOnly(); } }
        public IList<string> Readers { get { return _readers.AsReadOnly(); } }
        public IList<string> Consumers { get { return _consumers.AsReadOnly(); } }

        public bool TryWrite(T value, string writer)
        {
            var normalizedWriter = string.IsNullOrWhiteSpace(writer) ? "unknown" : writer;
            var incomingValue = FormatValue(value);

            if (!HasValue)
            {
                HasValue = true;
                Value = value;
                Writer = normalizedWriter;
                _writes.Add(SignalWriteRecord.Accepted(Name, normalizedWriter, incomingValue));
                return true;
            }

            var existingValue = FormatValue(Value);
            if (!EqualityComparer<T>.Default.Equals(Value, value))
            {
                HasConflict = true;
                _writes.Add(SignalWriteRecord.Conflict(Name, normalizedWriter, incomingValue));
                if (_diagnostics != null)
                {
                    _diagnostics.AddConflict(Name, Writer, normalizedWriter, existingValue, incomingValue);
                }
                return false;
            }

            _writes.Add(SignalWriteRecord.Duplicate(Name, normalizedWriter, incomingValue));
            if (_diagnostics != null)
            {
                _diagnostics.AddDuplicate(Name, Writer, normalizedWriter, incomingValue);
            }
            return true;
        }

        public bool TryGetValue(out T value)
        {
            value = Value;
            return HasValue && !HasConflict;
        }

        public bool TryRead(string reader, out T value)
        {
            var normalizedReader = string.IsNullOrWhiteSpace(reader) ? "unknown" : reader;
            _readers.Add(normalizedReader);
            value = Value;

            if (!HasValue)
            {
                if (_diagnostics != null)
                {
                    _diagnostics.AddMissingProducer(Name, normalizedReader);
                }
                return false;
            }

            if (HasConflict)
            {
                if (_diagnostics != null)
                {
                    _diagnostics.AddConflictedRead(Name, normalizedReader);
                }
                return false;
            }

            return true;
        }

        public T GetValueOrDefault(T fallback)
        {
            return HasValue && !HasConflict ? Value : fallback;
        }

        public void MarkConsumed(string consumer)
        {
            var normalizedConsumer = string.IsNullOrWhiteSpace(consumer) ? "unknown" : consumer;
            _consumers.Add(normalizedConsumer);
        }

        internal void AuditUnconsumedConsumer()
        {
            if (HasValue && !HasConflict && !WasConsumed && _diagnostics != null)
            {
                _diagnostics.AddUnconsumedConsumer(Name, Writer, FormatValue(Value));
            }
        }

        private static string FormatValue(T value)
        {
            if (value == null)
            {
                return "null";
            }

            var convertible = value as IConvertible;
            if (convertible != null)
            {
                return convertible.ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }
    }

    public sealed class SignalWriteRecord
    {
        private SignalWriteRecord(string signalName, string writer, string value, string state)
        {
            SignalName = signalName;
            Writer = writer;
            Value = value;
            State = state;
        }

        public string SignalName { get; }
        public string Writer { get; }
        public string Value { get; }
        public string State { get; }

        public static SignalWriteRecord Accepted(string signalName, string writer, string value)
        {
            return new SignalWriteRecord(signalName, writer, value, "accepted");
        }

        public static SignalWriteRecord Duplicate(string signalName, string writer, string value)
        {
            return new SignalWriteRecord(signalName, writer, value, "duplicate");
        }

        public static SignalWriteRecord Conflict(string signalName, string writer, string value)
        {
            return new SignalWriteRecord(signalName, writer, value, "conflict");
        }
    }

    public sealed class SignalDiagnostics
    {
        private readonly List<SignalDiagnosticEntry> _entries = new List<SignalDiagnosticEntry>();

        public bool HasConflicts { get; private set; }
        public bool HasIssues { get; private set; }
        public bool HasEntries { get { return _entries.Count > 0; } }
        public IList<SignalDiagnosticEntry> Entries { get { return _entries.AsReadOnly(); } }

        internal void AddConflict(
            string signalName,
            string firstWriter,
            string incomingWriter,
            string firstValue,
            string incomingValue)
        {
            HasConflicts = true;
            HasIssues = true;
            _entries.Add(new SignalDiagnosticEntry(
                "conflict",
                signalName,
                firstWriter,
                incomingWriter,
                firstValue,
                incomingValue));
        }

        internal void AddDuplicate(
            string signalName,
            string firstWriter,
            string incomingWriter,
            string value)
        {
            _entries.Add(new SignalDiagnosticEntry(
                "duplicate",
                signalName,
                firstWriter,
                incomingWriter,
                value,
                value));
        }

        internal void AddMissingProducer(string signalName, string reader)
        {
            HasIssues = true;
            _entries.Add(new SignalDiagnosticEntry(
                "missing_producer",
                signalName,
                string.Empty,
                reader,
                string.Empty,
                string.Empty));
        }

        internal void AddConflictedRead(string signalName, string reader)
        {
            HasIssues = true;
            _entries.Add(new SignalDiagnosticEntry(
                "conflicted_read",
                signalName,
                string.Empty,
                reader,
                string.Empty,
                string.Empty));
        }

        internal void AddUnconsumedConsumer(string signalName, string writer, string value)
        {
            HasIssues = true;
            _entries.Add(new SignalDiagnosticEntry(
                "unconsumed_consumer",
                signalName,
                writer,
                string.Empty,
                value,
                string.Empty));
        }

        public string ToSummary()
        {
            if (_entries.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var i = 0; i < _entries.Count && i < 8; i++)
            {
                parts.Add(_entries[i].ToSummary());
            }

            if (_entries.Count > 8)
            {
                parts.Add("more=" + (_entries.Count - 8).ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(";", parts.ToArray());
        }
    }

    public sealed class SignalDiagnosticEntry
    {
        public SignalDiagnosticEntry(
            string kind,
            string signalName,
            string firstWriter,
            string incomingWriter,
            string firstValue,
            string incomingValue)
        {
            Kind = kind ?? string.Empty;
            SignalName = signalName ?? string.Empty;
            FirstWriter = firstWriter ?? string.Empty;
            IncomingWriter = incomingWriter ?? string.Empty;
            FirstValue = firstValue ?? string.Empty;
            IncomingValue = incomingValue ?? string.Empty;
        }

        public string Kind { get; }
        public string SignalName { get; }
        public string FirstWriter { get; }
        public string IncomingWriter { get; }
        public string FirstValue { get; }
        public string IncomingValue { get; }

        public string ToSummary()
        {
            if (Kind == "missing_producer")
            {
                return Kind + ":" + SignalName + ":reader=" + IncomingWriter;
            }

            if (Kind == "conflicted_read")
            {
                return Kind + ":" + SignalName + ":reader=" + IncomingWriter;
            }

            if (Kind == "unconsumed_consumer")
            {
                return Kind + ":" + SignalName + ":writer=" + FirstWriter + "=" + FirstValue;
            }

            return Kind +
                   ":" + SignalName +
                   ":" + FirstWriter + "=" + FirstValue +
                   " vs " + IncomingWriter + "=" + IncomingValue;
        }
    }

    public sealed class CustomizeExecutionResult
    {
        public static readonly CustomizeExecutionResult Empty =
            new CustomizeExecutionResult(false, false, false, false, false, string.Empty);

        public CustomizeExecutionResult(
            bool aiEnabled,
            bool aiUseDeckMechanism,
            bool virtualCameraEnableTextHighlight,
            bool hasSignalConflicts,
            bool hasSignalIssues,
            string signalDiagnosticsSummary)
        {
            AiEnabled = aiEnabled;
            AiUseDeckMechanism = aiUseDeckMechanism;
            VirtualCameraEnableTextHighlight = virtualCameraEnableTextHighlight;
            HasSignalConflicts = hasSignalConflicts;
            HasSignalIssues = hasSignalIssues;
            SignalDiagnosticsSummary = signalDiagnosticsSummary ?? string.Empty;
        }

        public bool AiEnabled { get; }
        public bool AiUseDeckMechanism { get; }
        public bool VirtualCameraEnableTextHighlight { get; }
        public bool HasSignalConflicts { get; }
        public bool HasSignalIssues { get; }
        public string SignalDiagnosticsSummary { get; }
    }
}
