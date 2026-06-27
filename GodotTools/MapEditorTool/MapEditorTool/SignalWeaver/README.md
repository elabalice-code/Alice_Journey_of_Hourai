# Customizor Main Battlefield

Date: 2026-06-16

This folder is the intended new battlefield for the former `AICaller` signal-dense responsibilities.

The goal is not to wrap the old `AICaller` architecture with more layers. The enabled `Workplace.Customizor.Enabled` path means:

```text
We are not using the old AICaller product architecture.
We are incubating this product again.
```

## Core Flow

The desired flow is:

```text
Workplace pure producers
  -> Customizor pure producers
  -> necessary architectural computation relations
  -> Customizor pure consumers
  -> Workplace pure consumers
```

## Main Rule

The Customizor battlefield should contain pure producers, pure consumers, and explicit computation relations between them.

Broker / middleman signals are ignored for now.

This is intentional. A broker carries old architectural ambiguity. If we import brokers directly, we only recreate the old AICaller knot inside a new folder.

## Clone Rule

Avoid piling up intermediate variables.

One clone is allowed when it creates a necessary boundary:

```text
Workplace live mutable object
  -> SignalTickFrame clone
  -> pure relation calculation
  -> apply consumer outputs back to Workplace
```

The clone exists to keep the battlefield clean:

- no accidental writes to live `Workplace` during calculation;
- no hidden dependency on old AICaller static state;
- no broker fields sneaking into relation code;
- clearer testability for pure producer-to-consumer mappings.

Do not add another layer unless it removes a real coupling.

## Signal Boundary

This layer only handles simple signal values.

Detailed boundary catalog: `SIGNAL_BOUNDARY.md`.

Allowed signal shapes:

- bool
- numeric values
- enums
- small scalar identifiers such as ids or compact names

Excluded signal shapes:

- arrays and multidimensional arrays
- structs used as data bundles
- custom classes or mutable object graphs

Those larger data forms should stay in their owner modules. If they need to influence Customizor, expose simple producer facts such as presence, count, mode, id, or status.

## Boundary Bridge Rule

Complex data may connect to this layer only through simple feature signals.

For complex-to-simple producer paths:

```text
array / struct / custom class
  -> owner or recognizer Executor
  -> simple feature signal
  -> SignalTickFrame.Producers
```

The recognizer Executor should compute feature values before the Customizor signal round begins. Examples include presence, count, duration, selected id, status, confidence score, or mode.

For simple-to-complex consumer paths:

```text
SignalTickFrame.Consumers
  -> simple feature signal
  -> later dedicated Executor
  -> array / struct / custom class / playback payload
```

The later Executor should expand complex data after the Customizor signal round. This keeps relation calculation simple while still allowing downstream playback or rendering systems to build rich payloads.

## Feature Isolation Rule

Independent Issues, Ideas, Features, and Featurors must not leak intermediate signal variables to each other.

Allowed:

- private intermediate variables inside one Issue / Idea / Feature / Featuror;
- public producer and consumer fields declared on `SignalTickFrame`;
- shared cross-module signals that have been promoted into `Workplace`.

Forbidden:

- one Feature reading another Feature's private intermediate variable;
- hidden shared state between `Compozor`, `Incubator`, and `Featuror`;
- static side-channel fields used as a private signal bus.

If a new signal is genuinely needed by multiple Issues or Features, it must be added under `Workplace` first and then copied into `SignalTickFrame` as a normal producer or consumer.

## Frame Self-Check Rule

Producer and consumer signals must be accessed through audited signal slots.

```text
first write with value A
  -> accepted

second write with value A
  -> duplicate, allowed and recorded

second write with value B
  -> conflict, recorded and not applied back to Workplace

read producer before any value was written
  -> missing_producer, recorded

write consumer but no write-back stage consumes it
  -> unconsumed_consumer, recorded
```

This preserves the bare signal-frame shape while still making one-tick wiring errors observable. Producers are copied from `Workplace` as facts and must be read through audited accessors. Consumers are the contested output surface and must be consumed by the write-back stage before the round ends.

## Minimal Future Code Shape

Keep the first implementation small:

```text
Customizor/
|- README.md
|- Main/
|  |- CustomizeData.cs
|  |- CustomizeExecutor.cs
|- Compozor/
|- Incubator/
|- Featuror/
```

Suggested responsibilities:

- `Main/`: the Customizor battlefield entry and relation runner.
- `Main/CustomizeData.cs`: simple signal data structures only.
- `Main/CustomizeExecutor.cs`: the managed signal-frame entry currently hosted by `Actor/AICallerActor.cs`; owns one round of producer clone, editor calls, and consumer writeback.
- `Compozor/`: externally driven feature composition.
- `Incubator/`: internally driven idea incubation.
- `Featuror/`: mature feature families after promotion.

Do not start with a large class hierarchy.

## Folder Contract

`Main` is the only place that should know how one Customizor round is executed.

`Compozor`, `Incubator`, and `Featuror` should provide relation modules or feature modules for `Main` to call. They should not directly become alternate entry points into the Actor world.

## Frame Shape

The first `SignalTickFrame` should look conceptually like:

```text
SignalTickFrame
|- Producers
|  |- session facts
|  |- selected input facts
|  |- selected backend/playback facts
|- Consumers
|  |- AI state outputs
|  |- VirtualCamera state outputs
|  |- hint/log outputs if truly needed
```

Anything that is neither a clean producer nor a clean consumer should stay out until it is split.

## Candidate Pure Producers

Initial Workplace-derived producer facts may include:

- `CentralControl.Mode`
- `AI.Enabled`
- `AI.Backend`
- `AI.UseDeckMechanism`
- `AI.ChatOutputsMuted`
- `FragmentPcm16Le` presence, count, or status only; not raw arrays or old forwarding brokers
- current `VirtualCamera` playback facts if they can be represented as simple read-only facts
- `VirtualCamera` text-presence facts such as `HasAiText`, `HasOverlayText`, and `HasHintText`; not the text bodies themselves

## Candidate Pure Consumers

Initial Workplace consumer outputs may include:

- `AI.TaskType`
- `AI.TaskRequestId`
- `AI.DomainWinner`
- `AI.DomainPriority`
- `AI.DomainPendingMedia`
- `AI.LatestText`
- `VirtualCamera.AiText`
- `VirtualCamera.Mode`
- `VirtualCamera.RequestedMode`
- `VirtualCamera.ClearRetainedFrameOnApply`

These are not final feature commitments. They are endpoints that can receive results after the new relation layer computes them.

## Explicitly Excluded For Now

Do not import these old AICaller broker zones directly:

- pending user-turn FIFO as-is;
- response lock as-is;
- backend client lifecycle as-is;
- deck interrupt orchestration as-is;
- shadow text release as-is;
- audio-driven / visual-driven completion as-is;
- `_lastObservedWorkplace` re-entry as-is.

Each of those must be split before entering this battlefield:

```text
old broker
  -> producer fact(s)
  -> relation(s)
  -> consumer output(s)
```

## First Implementation Principle

The first code should prove the shape, not solve the whole assistant.

Start with one narrow relation, for example:

```text
Feature: Featuror/VirtualCamera/TextHighlight.cs
Relation: VirtualCameraEnableTextHighlight = false
VirtualCameraEnableTextHighlight: Workplace.VirtualCamera.EnableTextHighlight
```

Then add one real product relation after the shape is testable.
