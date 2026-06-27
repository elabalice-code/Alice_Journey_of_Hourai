using ActorFramework;
using AIAssistant.Customizor.Compozor;
using AIAssistant.Customizor.Featuror;
using AIAssistant.Customizor.Incubator;
using System;

namespace AIAssistant.Customizor.Main
{
    public static class CustomizeExecutor
    {
        // Managed signal-frame entry currently hosted by Actor/AICallerActor.cs.
        //
        // This executor only moves simple signal values into and out of SignalTickFrame.
        // It must not copy arrays, structs, custom classes, or mutable object graphs.
        // Long text, file paths, and payload-like strings are also kept outside this
        // first signal projection; use separate Executors to reduce or expand them.
        // Complex source data should already be reduced to simple feature signals by
        // an earlier recognizer Executor. Complex output data should be expanded later
        // by a dedicated Executor after this signal round.
        public static CustomizeExecutionResult Execute(Workplace workplace)
        {
            if (workplace == null || workplace.Customizor == null)
            {
                return CustomizeExecutionResult.Empty;
            }

            var signalTickFrame = new SignalTickFrame();

            CopyFromProducers(workplace, signalTickFrame);

            CompozorEditor.Apply(signalTickFrame);
            IncubatorEditor.Apply(signalTickFrame);
            FeaturorEditor.Apply(signalTickFrame);

            CopyToConsumers(workplace, signalTickFrame);
            signalTickFrame.Consumers.AuditUnconsumed();

            return CreateResult(signalTickFrame);
        }

        private static void CopyFromProducers(Workplace workplace, SignalTickFrame signalTickFrame)
        {
            WriteProducer(signalTickFrame.Producers.Workplace.SrcType, workplace.SrcType, "Workplace.SrcType");
            WriteProducer(signalTickFrame.Producers.Workplace.FragmentSampleRate,
                workplace.FragmentSampleRate,
                "Workplace.FragmentSampleRate");
            WriteProducer(signalTickFrame.Producers.Workplace.FragmentChannels,
                workplace.FragmentChannels,
                "Workplace.FragmentChannels");

            if (workplace.Debug != null)
            {
                WriteProducer(signalTickFrame.Producers.Debug.EnableActorLogs,
                    workplace.Debug.EnableActorLogs,
                    "Workplace.Debug.EnableActorLogs");
                WriteProducer(signalTickFrame.Producers.Debug.EnableVirtualCameraLogs,
                    workplace.Debug.EnableVirtualCameraLogs,
                    "Workplace.Debug.EnableVirtualCameraLogs");
                WriteProducer(signalTickFrame.Producers.Debug.EnableMultimodalLogs,
                    workplace.Debug.EnableMultimodalLogs,
                    "Workplace.Debug.EnableMultimodalLogs");
                WriteProducer(signalTickFrame.Producers.Debug.EnableTestActorLogs,
                    workplace.Debug.EnableTestActorLogs,
                    "Workplace.Debug.EnableTestActorLogs");
                WriteProducer(signalTickFrame.Producers.Debug.EnableAiChatLogs,
                    workplace.Debug.EnableAiChatLogs,
                    "Workplace.Debug.EnableAiChatLogs");
                WriteProducer(signalTickFrame.Producers.Debug.EnableAiAgentLogs,
                    workplace.Debug.EnableAiAgentLogs,
                    "Workplace.Debug.EnableAiAgentLogs");
                WriteProducer(signalTickFrame.Producers.Debug.EnableGreenScreenChecker,
                    workplace.Debug.EnableGreenScreenChecker,
                    "Workplace.Debug.EnableGreenScreenChecker");
                WriteProducer(signalTickFrame.Producers.Debug.EnableAecAudioDump,
                    workplace.Debug.EnableAecAudioDump,
                    "Workplace.Debug.EnableAecAudioDump");
            }

            if (workplace.Customizor != null)
            {
                WriteProducer(signalTickFrame.Producers.Customizor.Enabled,
                    workplace.Customizor.Enabled,
                    "Workplace.Customizor.Enabled");
            }

            if (workplace.KWS != null)
            {
                WriteProducer(signalTickFrame.Producers.Kws.Result,
                    workplace.KWS.Result,
                    "Workplace.KWS.Result");
            }

            if (workplace.CentralControl != null)
            {
                WriteProducer(signalTickFrame.Producers.CentralControl.Mode,
                    workplace.CentralControl.Mode,
                    "Workplace.CentralControl.Mode");
                WriteProducer(signalTickFrame.Producers.CentralControl.ActiveUntilUnixMs,
                    workplace.CentralControl.ActiveUntilUnixMs,
                    "Workplace.CentralControl.ActiveUntilUnixMs");
                WriteProducer(signalTickFrame.Producers.CentralControl.PendingThanksClose,
                    workplace.CentralControl.PendingThanksClose,
                    "Workplace.CentralControl.PendingThanksClose");
                WriteProducer(signalTickFrame.Producers.CentralControl.PendingThanksCloseSinceUnixMs,
                    workplace.CentralControl.PendingThanksCloseSinceUnixMs,
                    "Workplace.CentralControl.PendingThanksCloseSinceUnixMs");
                WriteProducer(signalTickFrame.Producers.CentralControl.PendingThanksCloseDeferred,
                    workplace.CentralControl.PendingThanksCloseDeferred,
                    "Workplace.CentralControl.PendingThanksCloseDeferred");
                WriteProducer(signalTickFrame.Producers.CentralControl.TimeoutSeconds,
                    workplace.CentralControl.TimeoutSeconds,
                    "Workplace.CentralControl.TimeoutSeconds");
            }

            if (workplace.AI != null)
            {
                WriteProducer(signalTickFrame.Producers.AI.Backend,
                    workplace.AI.Backend,
                    "Workplace.AI.Backend");
                WriteProducer(signalTickFrame.Producers.AI.Enabled,
                    workplace.AI.Enabled,
                    "Workplace.AI.Enabled");
                WriteProducer(signalTickFrame.Producers.AI.TaskType,
                    workplace.AI.TaskType,
                    "Workplace.AI.TaskType");
                WriteProducer(signalTickFrame.Producers.AI.TaskRequestId,
                    workplace.AI.TaskRequestId,
                    "Workplace.AI.TaskRequestId");
                WriteProducer(signalTickFrame.Producers.AI.DomainWinner,
                    workplace.AI.DomainWinner,
                    "Workplace.AI.DomainWinner");
                WriteProducer(signalTickFrame.Producers.AI.DomainPriority,
                    workplace.AI.DomainPriority,
                    "Workplace.AI.DomainPriority");
                WriteProducer(signalTickFrame.Producers.AI.DomainPendingMedia,
                    workplace.AI.DomainPendingMedia,
                    "Workplace.AI.DomainPendingMedia");
                WriteProducer(signalTickFrame.Producers.AI.UseLegacyLocalTurnControl,
                    workplace.AI.UseLegacyLocalTurnControl,
                    "Workplace.AI.UseLegacyLocalTurnControl");
                WriteProducer(signalTickFrame.Producers.AI.ChatOutputsMuted,
                    workplace.AI.ChatOutputsMuted,
                    "Workplace.AI.ChatOutputsMuted");
                WriteProducer(signalTickFrame.Producers.AI.UseDeckMechanism,
                    workplace.AI.UseDeckMechanism,
                    "Workplace.AI.UseDeckMechanism");
                WriteProducer(signalTickFrame.Producers.AI.OfflineWavTestDirectOn,
                    workplace.AI.OfflineWavTestDirectOn,
                    "Workplace.AI.OfflineWavTestDirectOn");
            }

            if (workplace.VirtualCamera != null)
            {
                WriteProducer(signalTickFrame.Producers.VirtualCamera.Mode,
                    workplace.VirtualCamera.Mode,
                    "Workplace.VirtualCamera.Mode");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.Resolution,
                    workplace.VirtualCamera.Resolution,
                    "Workplace.VirtualCamera.Resolution");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.EnableNoDriverCamera,
                    workplace.VirtualCamera.EnableNoDriverCamera,
                    "Workplace.VirtualCamera.EnableNoDriverCamera");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.SuppressTextOverlay,
                    workplace.VirtualCamera.SuppressTextOverlay,
                    "Workplace.VirtualCamera.SuppressTextOverlay");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.LoopPlayback,
                    workplace.VirtualCamera.LoopPlayback,
                    "Workplace.VirtualCamera.LoopPlayback");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.MaxFrames,
                    workplace.VirtualCamera.MaxFrames,
                    "Workplace.VirtualCamera.MaxFrames");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.AgentForceBubble,
                    workplace.VirtualCamera.AgentForceBubble,
                    "Workplace.VirtualCamera.AgentForceBubble");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.RequestedMode,
                    workplace.VirtualCamera.RequestedMode,
                    "Workplace.VirtualCamera.RequestedMode");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.StreamForceCamera,
                    workplace.VirtualCamera.StreamForceCamera,
                    "Workplace.VirtualCamera.StreamForceCamera");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ClearRetainedFrameOnApply,
                    workplace.VirtualCamera.ClearRetainedFrameOnApply,
                    "Workplace.VirtualCamera.ClearRetainedFrameOnApply");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ClearTextOverlayOnApply,
                    workplace.VirtualCamera.ClearTextOverlayOnApply,
                    "Workplace.VirtualCamera.ClearTextOverlayOnApply");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ResetAnimationOnApply,
                    workplace.VirtualCamera.ResetAnimationOnApply,
                    "Workplace.VirtualCamera.ResetAnimationOnApply");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.HintLayerWorking,
                    workplace.VirtualCamera.HintLayerWorking,
                    "Workplace.VirtualCamera.HintLayerWorking");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.AgentLayerWorking,
                    workplace.VirtualCamera.AgentLayerWorking,
                    "Workplace.VirtualCamera.AgentLayerWorking");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ForwarderLayerWorking,
                    workplace.VirtualCamera.ForwarderLayerWorking,
                    "Workplace.VirtualCamera.ForwarderLayerWorking");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ForwarderClaimActive,
                    workplace.VirtualCamera.ForwarderClaimActive,
                    "Workplace.VirtualCamera.ForwarderClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ForwarderClaimPriority,
                    workplace.VirtualCamera.ForwarderClaimPriority,
                    "Workplace.VirtualCamera.ForwarderClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.MicClaimActive,
                    workplace.VirtualCamera.MicClaimActive,
                    "Workplace.VirtualCamera.MicClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.MicClaimPriority,
                    workplace.VirtualCamera.MicClaimPriority,
                    "Workplace.VirtualCamera.MicClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.HintClaimActive,
                    workplace.VirtualCamera.HintClaimActive,
                    "Workplace.VirtualCamera.HintClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.HintClaimPriority,
                    workplace.VirtualCamera.HintClaimPriority,
                    "Workplace.VirtualCamera.HintClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.AiAgentClaimActive,
                    workplace.VirtualCamera.AiAgentClaimActive,
                    "Workplace.VirtualCamera.AiAgentClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.AiAgentClaimPriority,
                    workplace.VirtualCamera.AiAgentClaimPriority,
                    "Workplace.VirtualCamera.AiAgentClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ArchiveListClaimActive,
                    workplace.VirtualCamera.ArchiveListClaimActive,
                    "Workplace.VirtualCamera.ArchiveListClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ArchiveListClaimPriority,
                    workplace.VirtualCamera.ArchiveListClaimPriority,
                    "Workplace.VirtualCamera.ArchiveListClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ImageClaimActive,
                    workplace.VirtualCamera.ImageClaimActive,
                    "Workplace.VirtualCamera.ImageClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.ImageClaimPriority,
                    workplace.VirtualCamera.ImageClaimPriority,
                    "Workplace.VirtualCamera.ImageClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.VideoClaimActive,
                    workplace.VirtualCamera.VideoClaimActive,
                    "Workplace.VirtualCamera.VideoClaimActive");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.VideoClaimPriority,
                    workplace.VirtualCamera.VideoClaimPriority,
                    "Workplace.VirtualCamera.VideoClaimPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.VideoPlaybackPauseEligible,
                    workplace.VirtualCamera.VideoPlaybackPauseEligible,
                    "Workplace.VirtualCamera.VideoPlaybackPauseEligible");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.VideoPlaybackPaused,
                    workplace.VirtualCamera.VideoPlaybackPaused,
                    "Workplace.VirtualCamera.VideoPlaybackPaused");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.VoteWinnerName,
                    workplace.VirtualCamera.VoteWinnerName,
                    "Workplace.VirtualCamera.VoteWinnerName");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.VoteWinnerPriority,
                    workplace.VirtualCamera.VoteWinnerPriority,
                    "Workplace.VirtualCamera.VoteWinnerPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.DominantLayerPriority,
                    workplace.VirtualCamera.DominantLayerPriority,
                    "Workplace.VirtualCamera.DominantLayerPriority");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.DominantLayerName,
                    workplace.VirtualCamera.DominantLayerName,
                    "Workplace.VirtualCamera.DominantLayerName");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.EnableTextHighlight,
                    workplace.VirtualCamera.EnableTextHighlight,
                    "Workplace.VirtualCamera.EnableTextHighlight");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.HasAiText,
                    !string.IsNullOrWhiteSpace(workplace.VirtualCamera.AiText),
                    "Workplace.VirtualCamera.HasAiText");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.HasOverlayText,
                    !string.IsNullOrWhiteSpace(workplace.VirtualCamera.OverlayText),
                    "Workplace.VirtualCamera.HasOverlayText");
                WriteProducer(signalTickFrame.Producers.VirtualCamera.HasHintText,
                    !string.IsNullOrWhiteSpace(workplace.VirtualCamera.HintText),
                    "Workplace.VirtualCamera.HasHintText");
            }

            if (workplace.TestTool != null)
            {
                WriteProducer(signalTickFrame.Producers.TestTool.EnableFastCameraCapture,
                    workplace.TestTool.EnableFastCameraCapture,
                    "Workplace.TestTool.EnableFastCameraCapture");
                WriteProducer(signalTickFrame.Producers.TestTool.CaptureCameraName,
                    workplace.TestTool.CaptureCameraName,
                    "Workplace.TestTool.CaptureCameraName");
                WriteProducer(signalTickFrame.Producers.TestTool.CaptureRawPreferred,
                    workplace.TestTool.CaptureRawPreferred,
                    "Workplace.TestTool.CaptureRawPreferred");
                WriteProducer(signalTickFrame.Producers.TestTool.CaptureIntervalMs,
                    workplace.TestTool.CaptureIntervalMs,
                    "Workplace.TestTool.CaptureIntervalMs");
                WriteProducer(signalTickFrame.Producers.TestTool.LastCaptureUnixMs,
                    workplace.TestTool.LastCaptureUnixMs,
                    "Workplace.TestTool.LastCaptureUnixMs");
            }
        }

        private static void CopyToConsumers(Workplace workplace, SignalTickFrame signalTickFrame)
        {
            if (workplace.CentralControl != null)
            {
                CopyConsumer(signalTickFrame.Consumers.CentralControl.Mode,
                    value => workplace.CentralControl.Mode = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.CentralControl.Mode");
                CopyConsumer(signalTickFrame.Consumers.CentralControl.ActiveUntilUnixMs,
                    value => workplace.CentralControl.ActiveUntilUnixMs = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.CentralControl.ActiveUntilUnixMs");
                CopyConsumer(signalTickFrame.Consumers.CentralControl.PendingThanksClose,
                    value => workplace.CentralControl.PendingThanksClose = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.CentralControl.PendingThanksClose");
                CopyConsumer(signalTickFrame.Consumers.CentralControl.PendingThanksCloseSinceUnixMs,
                    value => workplace.CentralControl.PendingThanksCloseSinceUnixMs = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.CentralControl.PendingThanksCloseSinceUnixMs");
                CopyConsumer(signalTickFrame.Consumers.CentralControl.PendingThanksCloseDeferred,
                    value => workplace.CentralControl.PendingThanksCloseDeferred = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.CentralControl.PendingThanksCloseDeferred");
                CopyConsumer(signalTickFrame.Consumers.CentralControl.TimeoutSeconds,
                    value => workplace.CentralControl.TimeoutSeconds = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.CentralControl.TimeoutSeconds");
            }

            if (workplace.AI != null)
            {
                CopyConsumer(signalTickFrame.Consumers.AI.Backend,
                    value => workplace.AI.Backend = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.Backend");
                CopyConsumer(signalTickFrame.Consumers.AI.Enabled,
                    value => workplace.AI.Enabled = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.Enabled");
                CopyConsumer(signalTickFrame.Consumers.AI.TaskType,
                    value => workplace.AI.TaskType = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.TaskType");
                CopyConsumer(signalTickFrame.Consumers.AI.TaskRequestId,
                    value => workplace.AI.TaskRequestId = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.TaskRequestId");
                CopyConsumer(signalTickFrame.Consumers.AI.DomainWinner,
                    value => workplace.AI.DomainWinner = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.DomainWinner");
                CopyConsumer(signalTickFrame.Consumers.AI.DomainPriority,
                    value => workplace.AI.DomainPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.DomainPriority");
                CopyConsumer(signalTickFrame.Consumers.AI.DomainPendingMedia,
                    value => workplace.AI.DomainPendingMedia = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.DomainPendingMedia");
                CopyConsumer(signalTickFrame.Consumers.AI.UseLegacyLocalTurnControl,
                    value => workplace.AI.UseLegacyLocalTurnControl = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.UseLegacyLocalTurnControl");
                CopyConsumer(signalTickFrame.Consumers.AI.ChatOutputsMuted,
                    value => workplace.AI.ChatOutputsMuted = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.ChatOutputsMuted");
                CopyConsumer(signalTickFrame.Consumers.AI.UseDeckMechanism,
                    value => workplace.AI.UseDeckMechanism = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.UseDeckMechanism");
                CopyConsumer(signalTickFrame.Consumers.AI.OfflineWavTestDirectOn,
                    value => workplace.AI.OfflineWavTestDirectOn = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.AI.OfflineWavTestDirectOn");
            }

            if (workplace.VirtualCamera != null)
            {
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.Mode,
                    value => workplace.VirtualCamera.Mode = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.Mode");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.Resolution,
                    value => workplace.VirtualCamera.Resolution = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.Resolution");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.EnableNoDriverCamera,
                    value => workplace.VirtualCamera.EnableNoDriverCamera = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.EnableNoDriverCamera");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.SuppressTextOverlay,
                    value => workplace.VirtualCamera.SuppressTextOverlay = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.SuppressTextOverlay");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.LoopPlayback,
                    value => workplace.VirtualCamera.LoopPlayback = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.LoopPlayback");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.MaxFrames,
                    value => workplace.VirtualCamera.MaxFrames = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.MaxFrames");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.AgentForceBubble,
                    value => workplace.VirtualCamera.AgentForceBubble = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.AgentForceBubble");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.RequestedMode,
                    value => workplace.VirtualCamera.RequestedMode = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.RequestedMode");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.StreamForceCamera,
                    value => workplace.VirtualCamera.StreamForceCamera = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.StreamForceCamera");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ClearRetainedFrameOnApply,
                    value => workplace.VirtualCamera.ClearRetainedFrameOnApply = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ClearRetainedFrameOnApply");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ClearTextOverlayOnApply,
                    value => workplace.VirtualCamera.ClearTextOverlayOnApply = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ClearTextOverlayOnApply");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ResetAnimationOnApply,
                    value => workplace.VirtualCamera.ResetAnimationOnApply = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ResetAnimationOnApply");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ForwarderClaimActive,
                    value => workplace.VirtualCamera.ForwarderClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ForwarderClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ForwarderClaimPriority,
                    value => workplace.VirtualCamera.ForwarderClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ForwarderClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.MicClaimActive,
                    value => workplace.VirtualCamera.MicClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.MicClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.MicClaimPriority,
                    value => workplace.VirtualCamera.MicClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.MicClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.HintClaimActive,
                    value => workplace.VirtualCamera.HintClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.HintClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.HintClaimPriority,
                    value => workplace.VirtualCamera.HintClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.HintClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.AiAgentClaimActive,
                    value => workplace.VirtualCamera.AiAgentClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.AiAgentClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.AiAgentClaimPriority,
                    value => workplace.VirtualCamera.AiAgentClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.AiAgentClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ArchiveListClaimActive,
                    value => workplace.VirtualCamera.ArchiveListClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ArchiveListClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ArchiveListClaimPriority,
                    value => workplace.VirtualCamera.ArchiveListClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ArchiveListClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ImageClaimActive,
                    value => workplace.VirtualCamera.ImageClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ImageClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.ImageClaimPriority,
                    value => workplace.VirtualCamera.ImageClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.ImageClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.VideoClaimActive,
                    value => workplace.VirtualCamera.VideoClaimActive = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.VideoClaimActive");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.VideoClaimPriority,
                    value => workplace.VirtualCamera.VideoClaimPriority = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.VideoClaimPriority");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.VideoPlaybackPauseEligible,
                    value => workplace.VirtualCamera.VideoPlaybackPauseEligible = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.VideoPlaybackPauseEligible");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.VideoPlaybackPaused,
                    value => workplace.VirtualCamera.VideoPlaybackPaused = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.VideoPlaybackPaused");
                CopyConsumer(signalTickFrame.Consumers.VirtualCamera.EnableTextHighlight,
                    value => workplace.VirtualCamera.EnableTextHighlight = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.VirtualCamera.EnableTextHighlight");
            }

            if (workplace.TestTool != null)
            {
                CopyConsumer(signalTickFrame.Consumers.TestTool.EnableFastCameraCapture,
                    value => workplace.TestTool.EnableFastCameraCapture = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.TestTool.EnableFastCameraCapture");
                CopyConsumer(signalTickFrame.Consumers.TestTool.CaptureCameraName,
                    value => workplace.TestTool.CaptureCameraName = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.TestTool.CaptureCameraName");
                CopyConsumer(signalTickFrame.Consumers.TestTool.CaptureRawPreferred,
                    value => workplace.TestTool.CaptureRawPreferred = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.TestTool.CaptureRawPreferred");
                CopyConsumer(signalTickFrame.Consumers.TestTool.CaptureIntervalMs,
                    value => workplace.TestTool.CaptureIntervalMs = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.TestTool.CaptureIntervalMs");
                CopyConsumer(signalTickFrame.Consumers.TestTool.LastCaptureUnixMs,
                    value => workplace.TestTool.LastCaptureUnixMs = value,
                    "CustomizeExecutor.CopyToConsumers.Workplace.TestTool.LastCaptureUnixMs");
            }
        }

        private static CustomizeExecutionResult CreateResult(SignalTickFrame signalTickFrame)
        {
            var enableTextHighlight =
                signalTickFrame.Consumers.VirtualCameraEnableTextHighlight.GetValueOrDefault(false);

            return new CustomizeExecutionResult(
                signalTickFrame.Producers.AiEnabled.GetValueOrDefault(false),
                signalTickFrame.Producers.AiUseDeckMechanism.GetValueOrDefault(false),
                enableTextHighlight,
                signalTickFrame.Diagnostics.HasConflicts,
                signalTickFrame.Diagnostics.HasIssues,
                signalTickFrame.Diagnostics.ToSummary());
        }

        private static void WriteProducer<T>(SignalSlot<T> slot, T value, string workplacePath)
        {
            slot.TryWrite(value, "CustomizeExecutor.CopyFromProducers." + workplacePath);
        }

        private static void CopyConsumer<T>(SignalSlot<T> slot, Action<T> apply, string consumer)
        {
            T value;
            if (!slot.TryGetValue(out value))
            {
                return;
            }

            apply(value);
            slot.MarkConsumed(consumer);
        }
    }
}
