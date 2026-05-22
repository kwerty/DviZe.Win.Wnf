# WnfClient

**Namespace:** `Kwerty.DviZe.Win.Wnf`

A managed wrapper around the Windows Notification Facility (WNF) undocumented native APIs.

## Constructor

```csharp
public WnfClient(ILoggerFactory loggerFactory);
```

## Create

```csharp
public ulong Create(WnfLifetime lifetime, WnfScope scope, RawSecurityDescriptor securityDescriptor = null, int maxDataSize = 0, bool isDataPersistent = false);
```

Creates a new WNF state.

Returns the state name.

Must be running as **LocalSystem** for `WnfLifetime.WellKnown` and `WnfLifetime.Persistent` lifetimes.

`isDataPersistent` determines whether data persists across reboots. Only applicable for `WnfLifetime.WellKnown` and `WnfLifetime.Persistent` lifetimes.

### WnfLifetime

| Value         | Description
| :-            | :-
| `WellKnown`   | Built-in state names defined by the OS.
| `Persistent`  | Persists across reboots. Microsoft refers to this as **Permanent** internally.
| `Volatile`    | Removed upon reboot. Microsoft refers to this as **Persistent** internally.
| `Temporary`   | Removed when the process ends.

### WnfScope

| Value
| :-
| `System`
| `Session`
| `User`
| `Process`
| `Machine`
| `PhysicalMachine`

## Create\<T\>

```csharp
public ulong Create<T>(WnfLifetime lifetime, WnfScope scope, RawSecurityDescriptor securityDescriptor = null, bool isDataPersistent = false) where T : unmanaged;
```

Creates a new WNF state, deriving the max data size from `T`.

Returns the state name.

## Delete

```csharp
public void Delete(ulong stateName);
```

Deletes the specified WNF state.

Note, this causes subscribers to receive an event with an empty buffer.

## GetSize

```csharp
public int GetSize(ulong stateName);
```

Gets the current size of the specified WNF state.

## Query

```csharp
public WnfState Query(ulong stateName);
public WnfState Query(ulong stateName, Span<byte> buffer);
```

Queries the specified WNF state.

If no buffer is supplied, a buffer of `WnfClient.MaxDataSize` bytes will be allocated.

Throws `WnfBufferTooSmallException` if the supplied buffer is too small.

### WnfState

| Property      | Type                  | Description
| :-            | :-                    | :-
| `Data`        | `ReadOnlySpan<byte>`  | The state data.
| `ChangeStamp` | `uint`                | The current change stamp.

## Query\<T\>

```csharp
public WnfState<T> Query<T>(ulong stateName) where T : unmanaged;
public WnfState<T> Query<T>(ulong stateName, Span<byte> buffer) where T : unmanaged;
```

Queries the specified WNF state, reinterpreting state data as `T`.

### WnfState\<T\>

| Property      | Type      | Description
| :-            | :-        | :-
| `Data`        | `T`       | The state data, reinterpreted as `T`.
| `ChangeStamp` | `uint`    | The current change stamp.

## Update

```csharp
public void Update(ulong stateName, uint? currentChangeStamp = null);
public void Update(ulong stateName, ReadOnlySpan<byte> buffer, uint? currentChangeStamp = null);
```

Updates the specified WNF state.

If `currentChangeStamp` is specified, the update only succeeds if the current change stamp matches.

If an empty/no buffer is provided, the WNF state will be cleared. Typically reserved for pulse-only states (data size 0).

No schema is enforced. The native API only rejects writes that exceed the state's maximum data size. Writing too little data or data of the wrong layout will silently corrupt the state.

## Update\<T\>

```csharp
public void Update<T>(ulong stateName, T stateData, uint? currentChangeStamp = null) where T : unmanaged;
```

Updates the specified WNF state by writing the bytes of `T`.

If `currentChangeStamp` is specified, the update only succeeds if the current change stamp matches.

No schema is enforced. The native API only rejects writes that exceed the state's maximum data size. Using the wrong type for `T` will silently corrupt the state.

## SubscribeAsync

```csharp
public Task<IDisposable> SubscribeAsync(ulong stateName, Action<WnfState> callback, CancellationToken cancellationToken = default);
public Task<IDisposable> SubscribeAsync(ulong stateName, uint currentChangeStamp, Action<WnfState> callback, CancellationToken cancellationToken = default);
```

Subscribes to state change notifications for the specified WNF state.

If `currentChangeStamp` is specified, the subscription starts from that change stamp.

Dispose the returned `IDisposable` to unsubscribe.

## SubscribeAsync\<T\>

```csharp
public Task<IDisposable> SubscribeAsync<T>(ulong stateName, Action<WnfState<T>> callback, CancellationToken cancellationToken = default) where T : unmanaged;
public Task<IDisposable> SubscribeAsync<T>(ulong stateName, uint currentChangeStamp, Action<WnfState<T>> callback, CancellationToken cancellationToken = default) where T : unmanaged;
```

Subscribes to state change notifications for the specified WNF state, reinterpreting state data as `T`.

If `currentChangeStamp` is specified, the subscription starts from that change stamp.

Dispose the returned `IDisposable` to unsubscribe.

## DisposeAsync

```csharp
public ValueTask DisposeAsync();
```

Unsubscribes all subscriptions and brings the client safely to a close.
