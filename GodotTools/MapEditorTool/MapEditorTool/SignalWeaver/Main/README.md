# Customizor Main

`Main` is the production entry area for the Customizor battlefield.

`CustomizeExecutor.cs` is the entry point.

It is the direct subordinate of:

```text
Actor/AICallerActor.cs
```

For now, `AICallerActor` is the old unmanaged signal pool and the temporary Customizor host.
Actor code should cross into production Customizor logic only through `CustomizeExecutor.Execute(...)`.

It owns the round shape:

```text
Workplace
  -> one necessary clone
  -> pure producer facts
  -> relation calculation
  -> pure consumer outputs
  -> Workplace
```

Signal boundary:

```text
Allowed: bool, numeric values, enums, and small scalar identifiers.
Excluded: arrays, multidimensional arrays, structs, and custom classes.
```

Detailed heavy-signal boundary: `../SIGNAL_BOUNDARY.md`.

Boundary bridge:

```text
complex data -> recognizer Executor -> simple feature signal -> SignalTickFrame
SignalTickFrame -> simple feature signal -> later Executor -> complex data / playback payload
```

Feature isolation:

```text
private intermediate signals stay inside one Issue / Idea / Feature / Featuror.
shared signals must be promoted to Workplace before multiple modules use them.
```

Frame self-check:

```text
producer and consumer signals are SignalSlot<T> values.
same-value duplicate writes are recorded.
different-value duplicate writes are conflicts and are not applied to Workplace.
producer reads before production are reported as missing_producer.
consumer writes without write-back consumption are reported as unconsumed_consumer.
```

`Main` may call modules from `Compozor`, `Incubator`, and `Featuror`, but those folders should not directly own Actor entry.

Current migrated example:

```text
Featuror/VirtualCamera/TextHighlight.cs
  -> VirtualCameraEnableTextHighlight = false
  -> Workplace.VirtualCamera.EnableTextHighlight
```
