using System;
using System.Runtime.InteropServices;

namespace Kwerty.DviZe.Win.Wnf;

internal static class Win32
{
    //
    // ntstatus.h
    //

    public const int STATUS_SUCCESS = 0x00000000;
    public const int STATUS_INVALID_PARAMETER = unchecked((int)0xC000000D);
    public const int STATUS_ACCESS_DENIED = unchecked((int)0xC0000022);
    public const int STATUS_BUFFER_TOO_SMALL = unchecked((int)0xC0000023);
    public const int STATUS_OBJECT_NAME_NOT_FOUND = unchecked((int)0xC0000034);
    public const int STATUS_PRIVILEGE_NOT_HELD = unchecked((int)0xC0000061);

    //
    // Undocumented APIs reverse engineered by nag0mez.
    // https://pwnedcoffee.com/blog/wnf-chronicles-i-introduction/
    //

    [DllImport("ntdll.dll")]
    public static extern int NtCreateWnfStateName(
        out ulong StateName,
        uint NameLifetime,
        uint DataScope,
        byte PersistData,
        IntPtr TypeId,
        uint MaximumStateSize,
        ref byte SecurityDescriptor
    );

    [DllImport("ntdll.dll")]
    public static extern int NtDeleteWnfStateName(ref ulong StateName);

    [DllImport("ntdll.dll")]
    public static extern int NtUpdateWnfStateData(
        ref ulong StateName,
        ref byte Buffer,
        uint Length,
        IntPtr TypeId,
        IntPtr ExplicitScope,
        uint MatchingChangeStamp,
        bool CheckStamp
    );

    [DllImport("ntdll.dll")]
    public static extern int NtDeleteWnfStateData(ref ulong StateName);

    [DllImport("ntdll.dll")]
    public static extern int NtQueryWnfStateData(
        ref ulong StateName,
        IntPtr TypeId,
        IntPtr ExplicitScope,
        out uint ChangeStamp,
        ref byte Buffer,
        ref uint BufferSize
    );

    [DllImport("ntdll.dll")]
    public static extern int RtlSubscribeWnfStateChangeNotification(
        out IntPtr Subscription,
        ulong StateName,
        uint ChangeStamp,
        WnfUserCallback Callback,
        IntPtr CallbackContext,
        IntPtr TypeId,
        uint SerializationGroup,
        uint Unknown
    );

    [DllImport("ntdll.dll")]
    public static extern int RtlUnsubscribeWnfStateChangeNotification(IntPtr Subscription);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int WnfUserCallback(
        ulong StateName,
        uint ChangeStamp,
        IntPtr TypeId,
        IntPtr CallbackContext,
        ref byte Buffer,
        uint BufferSize
    );
}