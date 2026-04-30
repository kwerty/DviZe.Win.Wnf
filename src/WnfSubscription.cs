using Kwerty.DviZe.Workers;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Kwerty.DviZe.Win.Wnf;

internal class WnfSubscription : Worker, IDisposable
{
    protected readonly ILogger logger;
    readonly ulong stateName;
    readonly uint currentChangeStamp;
    readonly Action<WnfState> callback;
    Win32.WnfUserCallback handler;
    IntPtr nativeSubscription;

    public WnfSubscription(ulong stateName, uint currentChangeStamp, Action<WnfState> callback, ILoggerFactory loggerFactory)
    {
        this.stateName = stateName;
        this.currentChangeStamp = currentChangeStamp;
        this.callback = callback;
        logger = loggerFactory.CreateLogger(GetType().Name);
    }

    protected override Task OnStartingAsync(WorkerStartingContext startingContext)
    {
        handler = HandleNotification;

        var result = Win32.RtlSubscribeWnfStateChangeNotification(out nativeSubscription, stateName, currentChangeStamp, handler, IntPtr.Zero, IntPtr.Zero, 0, 0);
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.RtlSubscribeWnfStateChangeNotification), result);

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

        return Task.CompletedTask;
    }

    protected override Task OnStoppingAsync()
    {
        var result = Win32.RtlUnsubscribeWnfStateChangeNotification(nativeSubscription);
        if (result != 0)
        {
            var nativeException = Win32Exception.FromError(nameof(Win32.RtlUnsubscribeWnfStateChangeNotification), result);
            logger.LogCritical(nativeException, "Unsubscribe failed.");
        }

        return Task.CompletedTask;
    }

    protected virtual int HandleNotification(ulong stateName, uint changeStamp, IntPtr typeId, IntPtr callbackContext, ref byte buffer, uint bufferSize)
    {
        if (!Context.StoppingToken.IsCancellationRequested)
        {
            var data = MemoryMarshal.CreateReadOnlySpan(ref buffer, (int)bufferSize);
            callback(new WnfState(data, changeStamp));
        }

        return Win32.STATUS_SUCCESS;
    }

    void IDisposable.Dispose() => Context.TryStop();
}

internal sealed class WnfSubscription<T>(ulong stateName, uint currentChangeStamp, Action<WnfState<T>> callback, ILoggerFactory loggerFactory)
    : WnfSubscription(stateName, currentChangeStamp, callback: null, loggerFactory) where T : unmanaged
{
    protected override int HandleNotification(ulong stateName, uint changeStamp, IntPtr typeId, IntPtr callbackContext, ref byte buffer, uint bufferSize)
    {
        if (Context.StoppingToken.IsCancellationRequested)
        {
            return Win32.STATUS_SUCCESS;
        }

        if (bufferSize != Unsafe.SizeOf<T>())
        {
            logger.LogError("Data size {size} does not match {type}.", bufferSize, typeof(T).Name);

            Context.TryStop();

            return Win32.STATUS_SUCCESS;
        }

        var data = MemoryMarshal.Read<T>(MemoryMarshal.CreateReadOnlySpan(ref buffer, Unsafe.SizeOf<T>()));
        callback(new WnfState<T>(data, changeStamp));

        return Win32.STATUS_SUCCESS;
    }
}
