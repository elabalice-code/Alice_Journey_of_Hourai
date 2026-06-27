# Customizor Signal Boundary

This document defines what may enter the Customizor signal layer and what must
stay in Actor or Executor owned heavy channels.

The current Customizor host is still named `Actor/AICallerActor.cs`. That file is
the temporary Customizor gate:

```text
Workplace live state
  -> AICaller/Customizor host selects simple facts
  -> CustomizeExecutor copies them into SignalTickFrame
  -> Featuror/Compozor/Incubator calculate simple relations
  -> CustomizeExecutor writes simple consumer values back
```

## Hard Rule

Only simple signals may enter `SignalTickFrame`.

Allowed:

- bool
- numeric values
- enums
- small scalar identifiers
- compact status labels
- compact priority values
- compact timestamps or ids

Not allowed:

- arrays
- multidimensional arrays
- byte buffers
- long text content
- file paths as payloads
- media payloads
- custom classes
- mutable object graphs
- queues, dictionaries, and lists
- client instances
- stream writers, timers, subscriptions, and disposable resources
- side effects such as toast, logging, file copy, playback, network send, or dispose

If heavy data must influence Customizor, it must first be reduced by its owner or
recognizer Executor into simple feature facts.

If Customizor output must create heavy data, Customizor should write only a
simple intent/status signal. A later dedicated Executor expands that signal into
text, media, playback, file, or network work.

## Do-Not-Touch Heavy Signals

These signals must not be read, copied, mutated, or transported directly by
Customizor Features. They belong to their owner Actor or Executor. Customizor may
only see reduced facts derived from them.

### `Workplace.FragmentPcm16Le`

Type:

```text
byte[]
```

Function:

Current raw PCM audio fragment. It is pushed to realtime backends, remembered in
pre-wake buffers, inspected by VAD/deck logic, dumped for audio debugging, and
used to estimate speech/audio timing.

Why Customizor cannot touch it:

It is a mutable payload buffer, not a signal. Moving it into `SignalTickFrame`
would make Customizor an audio queue.

Allowed facts:

```text
FragmentHasAudio
FragmentByteCount
FragmentSampleRate
FragmentChannels
FragmentApproxMs
```

### AICaller audio queues

Examples:

```text
_pendingAudioFragments
_deckWaveCapturePreRoll
_deckAzurePreRoll
```

Function:

They preserve audio ordering across backend readiness, wakeword discard,
Deck/Azure forwarding, pre-roll capture, and delayed submission.

Why Customizor cannot touch them:

They are FIFO transport state. Importing them would reintroduce queue semantics
and old broker behavior into the lightweight signal layer.

Allowed facts:

```text
PendingAudioFragmentCount
PendingAudioBytes
DeckPreRollCount
AzurePreRollCount
```

### AI and user text payloads

Examples:

```text
Workplace.AI.LatestText
_latestText
_latestRetainedText
_latestFullAzureText
_latestUserText
_latestDelta
_lastAppliedText
```

Function:

They carry real user/AI content, response deltas, retained subtitle text, and
shadow/full-text synchronization state.

Why Customizor cannot touch them:

They are long content payloads with synchronization semantics. A signal Feature
should not parse, trim, preserve, or publish text content directly.

Allowed facts:

```text
HasLatestText
LatestTextLength
HasUsableLatestText
HasMeaningfulUserText
LatestTextVersion
AppliedTextVersion
TextHeldForShadow
```

### VirtualCamera text payloads

Examples:

```text
Workplace.VirtualCamera.AiText
Workplace.VirtualCamera.OverlayText
Workplace.VirtualCamera.HintText
Workplace.VirtualCamera.UserQuestionText
Workplace.VirtualCamera.ArchiveListText
Workplace.VirtualCamera.LayerStateSummary
```

Function:

They are rendering and overlay content. The VirtualCamera runtime consumes them
to compose subtitles, hints, archive lists, retained frames, and visible UI
layers.

Why Customizor cannot touch them:

They are presentation payloads. Customizor can decide whether text should be
shown or cleared, but should not own the text body.

Allowed facts:

```text
HasAiText
AiTextLength
HasOverlayText
OverlayTextLength
HasHintText
HasArchiveListText
LayerStateCode
```

### Media paths and executable paths

Examples:

```text
Workplace.VirtualCamera.VideoPath
Workplace.VirtualCamera.ImagePath
Workplace.VirtualCamera.ArchiveListDirectory
Workplace.TestTool.FastCameraExePath
DummyImageAssetPath
DummyVideoAssetPath
_videoPlaybackControlPath
```

Function:

They point to files, directories, media playback targets, dummy assets, external
tools, or active playback control files.

Why Customizor cannot touch them:

Paths are payload handles. Reading or writing them implies file existence,
selection, copy, playback, or external process behavior.

Allowed facts:

```text
HasVideoPath
HasImagePath
ActiveMediaKind
MediaPlaybackActive
CaptureToolConfigured
```

### Camera probe frame payloads

Examples:

```text
ProbeFramePayload
ProbeFramePayload.Rgb24
ProbeFramePayload.Nv12
LogWorkplace probe frame queue
```

Function:

They carry camera diagnostic frames and pixel buffers for probe/debug flows.

Why Customizor cannot touch them:

They are image payloads and queues. Customizor should not transport camera frame
buffers.

Allowed facts:

```text
ProbeFrameAvailable
ProbePixelFormat
ProbeWidth
ProbeHeight
ProbeBufferBytes
ProbeAvgR
ProbeAvgG
ProbeAvgB
```

### Countdown and hint queue internals

Examples:

```text
CountdownWorkplace
CountdownChannelWorkplace
Dictionary<string, CountdownEntry>
CountdownEntry.Text
```

Function:

They manage timed hint entries, cooldown, deduplication, priority, and resolved
hint text.

Why Customizor cannot touch them:

They are mutable scheduling queues with text payloads. Customizor should only
emit or consume simple timing/intent facts.

Allowed facts:

```text
HintQueueHasActive
HintQueuePriority
HintQueueRemainingMs
HintCooldownActive
HintSetRequested
```

### Backend client instances

Examples:

```text
_azureClient
RealtimeVoiceLiveClient
Azure SDK clients
```

Function:

They own network connections, backend sessions, input buffers, callbacks,
request/response operations, and disposal/retry behavior.

Why Customizor cannot touch them:

They are disposable runtime objects and network state machines. Signal Features
must not connect, dispose, clear buffers, or send requests.

Allowed facts:

```text
BackendClientReady
BackendRetryDue
BackendUnavailable
BackendLastErrorCode
AzureSpeechActive
TtsSpeaking
```

### Pending user turn queues

Examples:

```text
PendingUserTurn
_pendingUserTurns
shadow text FIFO
retained text release state
```

Function:

They preserve user-turn ordering while an AI response is active, hold deferred
turns, and control when retained/shadow text can be released.

Why Customizor cannot touch them:

They are ordering queues. Customizor may decide simple policy, but should not
enqueue, dequeue, or inspect queued turn objects directly.

Allowed facts:

```text
PendingUserTurnCount
PendingUserTurnAvailable
PendingUserTurnOldestAgeMs
ShadowTextHeld
ShadowReleaseReady
```

### Deck, VAD, AEC, timers, and dump writers

Examples:

```text
DeckMechanismRuntime
DeckWaveCaptureWriter
AecAudioDumpWriter
_audioCompletionSafetyTimer
pre/post AEC dump writers
```

Function:

They manage speech detection, deck confirmation, AEC diagnostics, wave capture,
audio completion safety, and debug file output.

Why Customizor cannot touch them:

They are stateful runtime machinery. Customizor should not run audio algorithms,
write dump files, or manage timers directly.

Allowed facts:

```text
DeckMicActive
DeckWaveAlive
DeckMeaningfulSpeech
AecDumpEnabled
CompletionDeferredForAudio
CompletionDeferredForVisual
CompletionDeferredAgeMs
```

### Side-effect operations

Examples:

```text
Console.WriteLine
DesktopToastRuntime.Show
File.Copy
Directory.CreateDirectory
Actor.SendMessage
Speaker.BroadcastNotification
client.Dispose
client.ClearInputAudioBuffer
RequestResponseForLatestUserTurn
CommitInputAudioTurnAndRequestResponse
VirtualCameraPlayer release/playback calls
```

Function:

They mutate the outside world: logs, files, UI notifications, actors, backend
buffers, network requests, and media playback state.

Why Customizor cannot touch them:

The Customizor layer is a relation calculator. It should output simple intents,
not perform side effects.

Allowed facts or intents:

```text
ToastRequested
MediaReleaseRequested
BackendDisposeRequested
InputCommitRequested
VisualRefreshRequested
```

## Pure Producer Side

Pure producers are read-only facts for one Customizor tick.

They are selected by the current Customizor host, still named
`Actor/AICallerActor.cs`, and copied by `CustomizeExecutor` into
`SignalTickFrame.Producers`.

Producer rules:

- A producer is a fact snapshot, not a command.
- A producer may be read by many Features.
- A Feature must not write back to a producer slot.
- A producer should be stable for the current tick.
- A producer must be simple: bool, number, enum, compact id, compact status.
- A producer derived from heavy data must be computed before the Customizor round.
- A producer must not expose a queue, payload, client, path payload, or mutable object.

The producer side answers:

```text
What simple facts are true at the start of this tick?
```

The consumer side answers:

```text
What simple decisions should be applied after relation calculation?
```

### Current Producer Groups

The current `SignalTickFrame.Producers` are grouped by their source Workplace
area.

#### Workplace

```text
Workplace.SrcType
Workplace.FragmentSampleRate
Workplace.FragmentChannels
```

Meaning:

These are lightweight frame/source facts. `FragmentPcm16Le` itself is excluded;
only sample-rate/channel metadata may enter directly.

Potential future reductions:

```text
FragmentHasAudio
FragmentByteCount
FragmentApproxMs
```

#### Debug

```text
Workplace.Debug.EnableActorLogs
Workplace.Debug.EnableVirtualCameraLogs
Workplace.Debug.EnableMultimodalLogs
Workplace.Debug.EnableTestActorLogs
Workplace.Debug.EnableAiChatLogs
Workplace.Debug.EnableAiAgentLogs
Workplace.Debug.EnableGreenScreenChecker
Workplace.Debug.EnableAecAudioDump
```

Meaning:

Debug flags are simple policy facts. They may influence whether a Feature writes
diagnostic intent, but logging side effects themselves stay outside Customizor.

#### Customizor

```text
Workplace.Customizor.Enabled
```

Meaning:

This is the top-level gate fact. It explains why the current tick is using the
managed signal path. It should not become a normal Feature-controlled toggle
without a separate host-level decision.

#### KWS

```text
Workplace.KWS.Result
```

Meaning:

Keyword result is compact enum state. Raw KWS text and fault strings are not
currently producer facts because they are content / diagnostic payloads.

Potential future reductions:

```text
KwsWakewordDetected
KwsThanksDetected
KwsFaulted
```

#### CentralControl

```text
Workplace.CentralControl.Mode
Workplace.CentralControl.ActiveUntilUnixMs
Workplace.CentralControl.PendingThanksClose
Workplace.CentralControl.PendingThanksCloseSinceUnixMs
Workplace.CentralControl.PendingThanksCloseDeferred
Workplace.CentralControl.TimeoutSeconds
```

Meaning:

These are timing and control facts. They are simple enough to enter the signal
layer directly and are expected to drive future `CentralControl/Timing.cs` and
`CentralControl/TriggerMode.cs` Features.

#### AI

```text
Workplace.AI.Backend
Workplace.AI.Enabled
Workplace.AI.TaskType
Workplace.AI.TaskRequestId
Workplace.AI.DomainWinner
Workplace.AI.DomainPriority
Workplace.AI.DomainPendingMedia
Workplace.AI.UseLegacyLocalTurnControl
Workplace.AI.ChatOutputsMuted
Workplace.AI.UseDeckMechanism
Workplace.AI.OfflineWavTestDirectOn
```

Meaning:

These are AI workflow control-plane facts. They are not model content. The
notable exclusion is `Workplace.AI.LatestText`, which is heavy text content and
must first become simple facts such as `HasLatestText` or `LatestTextLength`.

#### VirtualCamera

```text
Workplace.VirtualCamera.Mode
Workplace.VirtualCamera.Resolution
Workplace.VirtualCamera.EnableNoDriverCamera
Workplace.VirtualCamera.SuppressTextOverlay
Workplace.VirtualCamera.LoopPlayback
Workplace.VirtualCamera.MaxFrames
Workplace.VirtualCamera.AgentForceBubble
Workplace.VirtualCamera.RequestedMode
Workplace.VirtualCamera.StreamForceCamera
Workplace.VirtualCamera.ClearRetainedFrameOnApply
Workplace.VirtualCamera.ClearTextOverlayOnApply
Workplace.VirtualCamera.ResetAnimationOnApply
Workplace.VirtualCamera.HintLayerWorking
Workplace.VirtualCamera.AgentLayerWorking
Workplace.VirtualCamera.ForwarderLayerWorking
Workplace.VirtualCamera.ForwarderClaimActive
Workplace.VirtualCamera.ForwarderClaimPriority
Workplace.VirtualCamera.MicClaimActive
Workplace.VirtualCamera.MicClaimPriority
Workplace.VirtualCamera.HintClaimActive
Workplace.VirtualCamera.HintClaimPriority
Workplace.VirtualCamera.AiAgentClaimActive
Workplace.VirtualCamera.AiAgentClaimPriority
Workplace.VirtualCamera.ArchiveListClaimActive
Workplace.VirtualCamera.ArchiveListClaimPriority
Workplace.VirtualCamera.ImageClaimActive
Workplace.VirtualCamera.ImageClaimPriority
Workplace.VirtualCamera.VideoClaimActive
Workplace.VirtualCamera.VideoClaimPriority
Workplace.VirtualCamera.VideoPlaybackPauseEligible
Workplace.VirtualCamera.VideoPlaybackPaused
Workplace.VirtualCamera.VoteWinnerName
Workplace.VirtualCamera.VoteWinnerPriority
Workplace.VirtualCamera.DominantLayerPriority
Workplace.VirtualCamera.DominantLayerName
Workplace.VirtualCamera.EnableTextHighlight
```

Meaning:

These are compact visual state facts and claim facts. Text payloads, media paths,
layer summaries, frame buffers, and rendering payloads are excluded. If a Feature
needs to know whether text or media exists, add a reduced fact such as
`HasVideoPath`, `HasOverlayText`, or `OverlayTextLength`.

#### TestTool

```text
Workplace.TestTool.EnableFastCameraCapture
Workplace.TestTool.CaptureCameraName
Workplace.TestTool.CaptureRawPreferred
Workplace.TestTool.CaptureIntervalMs
Workplace.TestTool.LastCaptureUnixMs
```

Meaning:

These are simple tool-control facts. `FastCameraExePath` is excluded because it
is a path payload; expose `CaptureToolConfigured` or a similar boolean if the
signal layer needs to reason about it.

### Producer Reduction Rule

When heavy data needs to become a producer, use this pattern:

```text
heavy owner data
  -> owner / recognizer Executor computes simple facts
  -> facts are placed on Workplace or another approved simple source
  -> CustomizeExecutor copies facts into SignalTickFrame.Producers
```

Examples:

```text
FragmentPcm16Le -> FragmentHasAudio, FragmentByteCount, FragmentApproxMs
LatestText -> HasLatestText, LatestTextLength, HasUsableLatestText
VideoPath -> HasVideoPath, ActiveMediaKind
CountdownChannelWorkplace -> HintQueueHasActive, HintQueueRemainingMs
RealtimeVoiceLiveClient -> BackendClientReady, BackendRetryDue
ProbeFramePayload -> ProbeFrameAvailable, ProbeWidth, ProbeHeight
```

Do not let a Feature open or inspect the heavy object directly. A Feature should
only consume the reduced producer fact.

## Heavy Signal Catalog

### Raw Audio Fragments

Current heavy data:

```text
Workplace.FragmentPcm16Le: byte[]
AICaller._pendingAudioFragments: Queue<byte[]>
AICaller._deckWaveCapturePreRoll: Queue<byte[]>
AICaller._deckAzurePreRoll: Queue<byte[]>
```

Why excluded:

Raw PCM buffers are payloads. They are cloned, queued, trimmed, pushed to
backends, dumped, and analyzed by audio-specific code. Copying them into
Customizor would turn the signal frame into another audio transport.

Allowed replacement facts:

```text
FragmentSampleRate
FragmentChannels
FragmentHasAudio
FragmentByteCount
FragmentApproxMs
MicPeakBucket
MicSpeechDetected
DeckWaveAlive
DeckMeaningfulSpeech
```

Owner / recognizer direction:

```text
audio buffer -> audio/deck recognizer Executor -> simple audio facts -> Customizor
Customizor intent -> audio Executor -> enqueue / commit / dump / discard
```

### AI Text And User Text

Current heavy data:

```text
Workplace.AI.LatestText
AICaller._latestText
AICaller._latestRetainedText
AICaller._latestFullAzureText
AICaller._latestUserText
AICaller._lastAppliedText
AICaller._latestDelta
AICaller._offlineWaveObservedText
AICaller._offlineWaveObservedUserText
```

Why excluded:

Text can be long, user-facing, versioned, synchronized with audio, filtered for
wakeword-only replies, and released through subtitle / shadow FIFO logic. It is
content, not a bare signal.

Allowed replacement facts:

```text
HasLatestText
LatestTextLength
HasUsableLatestText
HasMeaningfulUserText
LatestTextVersion
AppliedTextVersion
TextHeldForShadow
ShadowDraining
WakewordOnlyUserText
WakewordGreetingReply
```

Owner / recognizer direction:

```text
text content -> text recognizer Executor -> simple text readiness facts -> Customizor
Customizor intent -> subtitle/text Executor -> apply text to VirtualCamera
```

### VirtualCamera Text Payloads

Current heavy data:

```text
Workplace.VirtualCamera.AiText
Workplace.VirtualCamera.OverlayText
Workplace.VirtualCamera.HintText
Workplace.VirtualCamera.UserQuestionText
Workplace.VirtualCamera.ArchiveListText
Workplace.VirtualCamera.LayerStateSummary
```

Why excluded:

These fields are presentation payloads. They are consumed by rendering code,
inheritance rules, overlay composition, subtitle behavior, hint queues, and
archive display. They should not be recomposed inside a signal relation frame.

Allowed replacement facts:

```text
VirtualCameraAiTextLength
HasAiText
VirtualCameraOverlayTextLength
HasOverlayText
HasHintText
HasUserQuestionText
HasArchiveListText
LayerStateCode
ClearTextOverlayOnApply
SuppressTextOverlay
```

Owner / recognizer direction:

```text
text payload -> visual/text Executor -> simple visual text facts -> Customizor
Customizor intent -> visual/text Executor -> update concrete text fields
```

### Media Paths And Playback Payloads

Current heavy data:

```text
Workplace.VirtualCamera.VideoPath
Workplace.VirtualCamera.ImagePath
Workplace.VirtualCamera.ArchiveListDirectory
Workplace.TestTool.FastCameraExePath
AICaller.DummyImageAssetPath
AICaller.DummyVideoAssetPath
AICaller._videoPlaybackControlPath
```

Why excluded:

Paths are payload handles. They imply file existence, copying, playback,
selection, and external process or media runtime behavior. Customizor should not
validate, copy, or play files.

Allowed replacement facts:

```text
HasVideoPath
HasImagePath
ActiveMediaKind
MediaPlaybackActive
MediaReleaseRequested
CaptureToolConfigured
CaptureCameraName
```

Owner / recognizer direction:

```text
path / playback runtime -> media Executor -> simple media facts -> Customizor
Customizor intent -> media Executor -> open / release / copy / play
```

### Probe Frames And Camera Buffers

Current heavy data:

```text
ProbeFramePayload
ProbeFramePayload.Rgb24: byte[]
ProbeFramePayload.Nv12: byte[]
LogWorkplace._probeFrames: ConcurrentQueue<ProbeFramePayload>
```

Why excluded:

Probe frames are image buffers and diagnostic payloads. They can be large and are
owned by camera / logging diagnostics.

Allowed replacement facts:

```text
ProbeFrameAvailable
ProbePixelFormat
ProbeWidth
ProbeHeight
ProbeBufferBytes
ProbeAvgR
ProbeAvgG
ProbeAvgB
ProbeBottomUp
```

Owner / recognizer direction:

```text
frame payload -> camera/log recognizer -> simple frame facts -> Customizor
Customizor intent -> camera/log Executor -> drain / store / inspect payloads
```

### Countdown And Hint Queues

Current heavy data:

```text
CountdownWorkplace
CountdownChannelWorkplace
Dictionary<string, CountdownEntry>
CountdownEntry.Text
```

Why excluded:

Countdowns are mutable scheduling queues with deduplication, cooldown, priority,
and text payloads. Customizor should not own the queue internals.

Allowed replacement facts:

```text
HintQueueHasActive
HintQueuePriority
HintQueueRemainingMs
HintCooldownActive
HintSetRequested
```

Owner / recognizer direction:

```text
countdown queue -> countdown Executor -> simple timing facts -> Customizor
Customizor intent -> countdown Executor -> Set/TryResolve concrete entries
```

### Backend Clients And Network State

Current heavy data:

```text
AICaller._azureClient
RealtimeVoiceLiveClient
Azure SDK client objects
network sessions / subscriptions / request streams
```

Why excluded:

Clients are disposable resources and network state machines. They include
connection lifecycle, retries, buffers, callbacks, and exceptions. A signal frame
must never hold or clone client instances.

Allowed replacement facts:

```text
Backend
BackendClientReady
BackendRetryDue
BackendUnavailable
BackendLastErrorCode
AzureSpeechActive
TtsSpeaking
```

Owner / recognizer direction:

```text
client/runtime -> backend Executor -> simple readiness facts -> Customizor
Customizor intent -> backend Executor -> connect / dispose / send / retry
```

### Pending User Turns And FIFOs

Current heavy data:

```text
AICaller.PendingUserTurn
AICaller._pendingUserTurns: Queue<PendingUserTurn>
AICaller._pendingAudioFragments: Queue<byte[]>
shadow text FIFO / retained text state
```

Why excluded:

FIFO queues are ordering systems, not simple tick signals. Importing them
directly would recreate Akka-like queue semantics inside the lightweight signal
layer.

Allowed replacement facts:

```text
PendingUserTurnCount
PendingUserTurnOldestAgeMs
PendingUserTurnAvailable
PendingAudioFragmentCount
PendingAudioBytes
ShadowTextHeld
ShadowReleaseReady
```

Owner / recognizer direction:

```text
FIFO owner -> queue recognizer -> simple queue facts -> Customizor
Customizor intent -> FIFO Executor -> enqueue / dequeue / drop / release
```

### Deck, VAD, AEC, And Audio Diagnostics Runtime Objects

Current heavy data:

```text
DeckMechanismRuntime
DeckWaveCaptureWriter
AecAudioDumpWriter
timers
pre-roll buffers
audio completion safety timer
```

Why excluded:

These objects manage stateful audio processing, files, timers, and safety
fallbacks. Customizor can decide simple policy, but it must not execute the
runtime machinery.

Allowed replacement facts:

```text
DeckMechanismEnabled
DeckMicActive
DeckWaveAlive
DeckMeaningfulSpeech
AecDumpEnabled
CompletionDeferredForAudio
CompletionDeferredForVisual
CompletionDeferredAgeMs
BargeInResetRequested
```

Owner / recognizer direction:

```text
runtime objects -> audio/deck recognizers -> simple facts -> Customizor
Customizor intent -> audio/deck Executors -> update runtime state
```

`Workplace.VirtualCamera.ResetAnimationOnApply` is currently allowed only as a
simple one-shot reset flag. The heavier barge-in orchestration that decides when
the reset should happen must stay in the AI playback / audio runtime until it is
reduced to a simple fact such as `BargeInResetRequested`.

### Side Effects

Current heavy actions:

```text
Console.WriteLine / logging side effects
DesktopToastRuntime.Show
File.Copy
Directory.CreateDirectory
Actor SendMessage / BroadcastNotification
client.Dispose
client.ClearInputAudioBuffer
RequestResponseForLatestUserTurn
CommitInputAudioTurnAndRequestResponse
VirtualCameraPlayer release/playback operations
```

Why excluded:

The signal layer should calculate a decision, not perform the world mutation
itself. Side effects belong to Actors or Executors.

Allowed replacement facts or intents:

```text
ToastRequested
MediaReleaseRequested
BackendDisposeRequested
InputCommitRequested
VisualRefreshRequested
```

Those intents should be consumed by a dedicated Executor after the signal round.

## Border Examples

Allowed:

```text
Workplace.AI.Enabled -> Producers.AI.Enabled
Workplace.AI.Backend -> Producers.AI.Backend
Workplace.VirtualCamera.Mode -> Producers.VirtualCamera.Mode
Workplace.FragmentSampleRate -> Producers.Workplace.FragmentSampleRate
Workplace.FragmentChannels -> Producers.Workplace.FragmentChannels
```

Not allowed directly:

```text
Workplace.FragmentPcm16Le -> SignalTickFrame
Workplace.AI.LatestText -> SignalTickFrame
Workplace.VirtualCamera.AiText -> SignalTickFrame
Workplace.VirtualCamera.VideoPath -> SignalTickFrame
LogWorkplace.ProbeFramePayload -> SignalTickFrame
CountdownChannelWorkplace -> SignalTickFrame
RealtimeVoiceLiveClient -> SignalTickFrame
Queue<byte[]> -> SignalTickFrame
```

Allowed after reduction:

```text
FragmentPcm16Le.Length > 0 -> FragmentHasAudio
LatestText length > 0 -> HasLatestText
VideoPath not empty -> HasVideoPath
ProbeFramePayload.BufferBytes -> ProbeBufferBytes
CountdownChannel has live entry -> HintQueueHasActive
Azure client connected -> BackendClientReady
```

## Practical Migration Checklist

Before adding any new producer or consumer to `SignalTickFrame`, answer:

```text
1. Is the value bool, numeric, enum, or a small scalar identifier?
2. Can it be copied without cloning a mutable object graph?
3. Is it a fact or decision, not a queue, client, payload, or side effect?
4. If it came from heavy data, which Executor reduced it?
5. If it produces heavy data, which Executor will expand it later?
```

If any answer is unclear, do not add the signal yet. Add a recognizer or
Executor boundary first.
