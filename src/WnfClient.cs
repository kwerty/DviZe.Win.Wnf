using Kwerty.DviZe.Workers;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace Kwerty.DviZe.Win.Wnf;

public sealed class WnfClient : IAsyncDisposable
{
    public const int MaxDataSize = 4096;
    readonly ILoggerFactory loggerFactory;
    readonly Runner<WnfSubscription> subscriptionRunner;
    volatile bool closed;

    public WnfClient(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory, nameof(loggerFactory));

        this.loggerFactory = loggerFactory;
        subscriptionRunner = new Runner<WnfSubscription>(loggerFactory);
    }

    /// <summary>
    /// Creates a new WNF state.
    /// </summary>
    /// <param name="isDataPersistent">Determines whether data persists across reboots. Only applicable for <see cref="WnfLifetime.WellKnown"/> and <see cref="WnfLifetime.Persistent"/> lifetimes.</param>
    /// <returns>The state name.</returns>
    /// <remarks>
    /// Must be running as <b>LocalSystem</b> for <see cref="WnfLifetime.WellKnown"/> and <see cref="WnfLifetime.Persistent"/> lifetimes.
    /// </remarks>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public ulong Create(WnfLifetime lifetime, WnfScope scope, RawSecurityDescriptor securityDescriptor = null, int maxDataSize = 0, bool isDataPersistent = false)
    {
        // Technically any account with SeCreatePermanentPrivilege (granted via Local Security Policy and enabled via AdjustProcessToken) can
        // create WellKnown/Persistent states, but you shouldn't do that unless its for research/dev purposes. If you're deploying an app,
        // you would typically have your own Windows service running as LocalSystem, and it would be responsible for creating your states.

        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxDataSize, MaxDataSize, nameof(maxDataSize));

        securityDescriptor ??= new RawSecurityDescriptor(ControlFlags.None, null, null, null, null);

        if (lifetime >= WnfLifetime.Volatile)
        {
            isDataPersistent = false;
        }

        var sdBytes = new byte[securityDescriptor.BinaryLength];
        securityDescriptor.GetBinaryForm(sdBytes, 0);

        var result = Win32.NtCreateWnfStateName(out var stateName, (uint)lifetime, (uint)scope, Convert.ToByte(isDataPersistent), IntPtr.Zero, (uint)maxDataSize, ref MemoryMarshal.GetReference(sdBytes));
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtCreateWnfStateName), result);

            if (result == Win32.STATUS_PRIVILEGE_NOT_HELD)
            {
                throw new WnfException(WnfException.MustBeRunningAsLocalSystem, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }

        return stateName;
    }

    /// <summary>
    /// Creates a new WNF state, deriving the max data size from <typeparamref name="T"/>.
    /// </summary>
    /// <param name="isDataPersistent">Determines whether data persists across reboots. Only applicable for <see cref="WnfLifetime.WellKnown"/> and <see cref="WnfLifetime.Persistent"/> lifetimes.</param>
    /// <returns>The state name.</returns>
    /// <remarks>
    /// Must be running as <b>LocalSystem</b> for <see cref="WnfLifetime.WellKnown"/> and <see cref="WnfLifetime.Persistent"/> lifetimes.
    /// </remarks>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public ulong Create<T>(WnfLifetime lifetime, WnfScope scope, RawSecurityDescriptor securityDescriptor = null, bool isDataPersistent = false) where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() > MaxDataSize)
        {
            throw new ArgumentException($"{typeof(T).Name} exceeds {MaxDataSize} bytes.");
        }

        return Create(lifetime, scope, securityDescriptor, maxDataSize: Unsafe.SizeOf<T>(), isDataPersistent);
    }

    /// <summary>
    /// Deletes the specified WNF state.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public void Delete(ulong stateName)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));

        var result = Win32.NtDeleteWnfStateName(ref stateName);
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtDeleteWnfStateName), result);

            if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_PRIVILEGE_NOT_HELD)
            {
                throw new WnfException(WnfException.MustBeRunningAsLocalSystem, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }
    }

    /// <summary>
    /// Gets the current size of the specified WNF state.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public int GetSize(ulong stateName)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));

        var size = 0;

        var result = Win32.NtQueryWnfStateData(ref stateName, IntPtr.Zero, IntPtr.Zero, out uint changeStamp, ref Unsafe.NullRef<byte>(), ref Unsafe.As<int, uint>(ref size));

        if (result != 0
            && result != Win32.STATUS_BUFFER_TOO_SMALL)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtQueryWnfStateData), result);

            if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_ACCESS_DENIED)
            {
                throw new WnfException(WnfException.AccessDenied, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }

        return size;
    }

    /// <summary>
    /// Queries the specified WNF state.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public WnfState Query(ulong stateName)
        => Query(stateName, new byte[MaxDataSize]);

    /// <summary>
    /// Queries the specified WNF state.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfBufferTooSmallException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public WnfState Query(ulong stateName, Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));
        
        var size = buffer.Length;

        var result = Win32.NtQueryWnfStateData(ref stateName, IntPtr.Zero, IntPtr.Zero, out uint changeStamp, ref MemoryMarshal.GetReference(buffer), ref Unsafe.As<int, uint>(ref size));

        if (result == Win32.STATUS_BUFFER_TOO_SMALL)
        {
            throw new WnfBufferTooSmallException(size);
        }

        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtQueryWnfStateData), result);

            if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_ACCESS_DENIED)
            {
                throw new WnfException(WnfException.AccessDenied, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }

        return new WnfState(buffer[..size], changeStamp);
    }

    /// <summary>
    /// Queries the specified WNF state, reinterpreting state data as <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public WnfState<T> Query<T>(ulong stateName) where T : unmanaged
        => Query<T>(stateName, new byte[Unsafe.SizeOf<T>()]);

    /// <summary>
    /// Queries the specified WNF state, reinterpreting state data as <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public WnfState<T> Query<T>(ulong stateName, Span<byte> buffer) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));

        var size = Unsafe.SizeOf<T>();
        if (size != buffer.Length)
        {
            throw new ArgumentException($"Buffer size must match {typeof(T).Name}.", nameof(buffer));
        }

        var result = Win32.NtQueryWnfStateData(ref stateName, IntPtr.Zero, IntPtr.Zero, out uint changeStamp, ref MemoryMarshal.GetReference(buffer), ref Unsafe.As<int, uint>(ref size));

        if (result == Win32.STATUS_BUFFER_TOO_SMALL
            || (result == 0 && size < Unsafe.SizeOf<T>()))
        {
            throw new WnfException($"Data size {size} does not match {typeof(T).Name}.");
        }

        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtQueryWnfStateData), result);

            if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_ACCESS_DENIED)
            {
                throw new WnfException(WnfException.AccessDenied, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }

        return new WnfState<T>(MemoryMarshal.Read<T>(buffer), changeStamp);
    }

    /// <summary>
    /// Clears and/or pulses the specified WNF state.
    /// </summary>
    /// <param name="currentChangeStamp">If specified, the update only succeeds if the current change stamp matches.</param>
    /// <remarks>
    /// Typically reserved for pulse-only states (data size 0). If the state carries data, this will silently corrupt it.
    /// </remarks>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public void Update(ulong stateName, uint? currentChangeStamp = null)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));

        var result = Win32.NtUpdateWnfStateData(ref stateName, ref Unsafe.NullRef<byte>(), 0, IntPtr.Zero, IntPtr.Zero, currentChangeStamp ?? 0, currentChangeStamp.HasValue);
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtUpdateWnfStateData), result);

            if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_ACCESS_DENIED)
            {
                throw new WnfException(WnfException.AccessDenied, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }
    }

    /// <summary>
    /// Updates the specified WNF state.
    /// </summary>
    /// <param name="currentChangeStamp">If specified, the update only succeeds if the current change stamp matches.</param>
    /// <remarks>
    /// No schema is enforced, the native API only rejects updates that exceed the state's maximum data size.
    /// Writing too little data or data of the wrong layout will silently corrupt the state.
    /// </remarks>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public void Update(ulong stateName, ReadOnlySpan<byte> buffer, uint? currentChangeStamp = null)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));
        if (buffer.Length > MaxDataSize)
        {
            throw new ArgumentException($"Buffer must not exceed {MaxDataSize} bytes.", nameof(buffer));
        }

        var result = Win32.NtUpdateWnfStateData(ref stateName, ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length, IntPtr.Zero, IntPtr.Zero, currentChangeStamp ?? 0, currentChangeStamp.HasValue);
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtUpdateWnfStateData), result);

            if (result == Win32.STATUS_INVALID_PARAMETER)
            {
                throw new WnfException($"{nameof(Win32.NtUpdateWnfStateData)} returned {nameof(Win32.STATUS_INVALID_PARAMETER)}, likely indicating that the buffer exceeds the state's max data size.", nativeException);
            }
            else if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_ACCESS_DENIED)
            {
                throw new WnfException(WnfException.AccessDenied, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }
    }

    /// <summary>
    /// Updates the specified WNF state by writing the bytes of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="currentChangeStamp">If specified, the update only succeeds if the current change stamp matches.</param>
    /// <remarks>
    /// No schema is enforced, the native API only rejects writes that exceed the state's maximum data size.
    /// Using the wrong type for <typeparamref name="T"/> will silently corrupt the state.
    /// </remarks>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public void Update<T>(ulong stateName, T stateData, uint? currentChangeStamp = null) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));
        if (Unsafe.SizeOf<T>() > MaxDataSize)
        {
            throw new ArgumentException($"{typeof(T).Name} exceeds {MaxDataSize} bytes.");
        }

        var buffer = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref stateData, 1));
        var result = Win32.NtUpdateWnfStateData(ref stateName, ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length, IntPtr.Zero, IntPtr.Zero, currentChangeStamp ?? 0, currentChangeStamp.HasValue);
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.NtUpdateWnfStateData), result);

            if (result == Win32.STATUS_INVALID_PARAMETER)
            {
                throw new WnfException($"{nameof(Win32.NtUpdateWnfStateData)} returned {nameof(Win32.STATUS_INVALID_PARAMETER)}, likely indicating that {typeof(T).Name} exceeds the state's max data size.", nativeException);
            }
            else if (result == Win32.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                throw new WnfException(WnfException.InvalidStateName, nativeException);
            }
            else if (result == Win32.STATUS_ACCESS_DENIED)
            {
                throw new WnfException(WnfException.AccessDenied, nativeException);
            }

            throw new WnfException(innerException: nativeException);
        }
    }

    /// <summary>
    /// Subscribes to state change notifications for the specified WNF state.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public Task<IDisposable> SubscribeAsync(ulong stateName, Action<WnfState> callback, CancellationToken cancellationToken = default)
        => SubscribeAsync(stateName, currentChangeStamp: 0, callback, cancellationToken);

    /// <summary>
    /// Subscribes to state change notifications for the specified WNF state.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public async Task<IDisposable> SubscribeAsync(ulong stateName, uint currentChangeStamp, Action<WnfState> callback, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));
        ArgumentNullException.ThrowIfNull(callback, nameof(callback));

        var subscription = new WnfSubscription(stateName, currentChangeStamp, callback, loggerFactory);
        await subscriptionRunner.StartWorkerAsync(subscription, cancellationToken).ConfigureAwait(false);
        return subscription;
    }

    /// <summary>
    /// Subscribes to state change notifications for the specified WNF state, reinterpreting state data as <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public Task<IDisposable> SubscribeAsync<T>(ulong stateName, Action<WnfState<T>> callback, CancellationToken cancellationToken = default) where T : unmanaged
        => SubscribeAsync(stateName, currentChangeStamp: 0, callback, cancellationToken);

    /// <summary>
    /// Subscribes to state change notifications for the specified WNF state, reinterpreting state data as <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentException" />
    /// <exception cref="WnfException" />
    /// <exception cref="ObjectDisposedException" />
    public async Task<IDisposable> SubscribeAsync<T>(ulong stateName, uint currentChangeStamp, Action<WnfState<T>> callback, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(closed, this);
        ArgumentOutOfRangeException.ThrowIfZero(stateName, nameof(stateName));
        ArgumentNullException.ThrowIfNull(callback, nameof(callback));

        var subscription = new WnfSubscription<T>(stateName, currentChangeStamp, callback, loggerFactory);
        await subscriptionRunner.StartWorkerAsync(subscription, cancellationToken).ConfigureAwait(false);
        return subscription;
    }

    public async ValueTask DisposeAsync()
    {
        closed = true;
        await subscriptionRunner.DisposeAsync().ConfigureAwait(false);
    }
}
