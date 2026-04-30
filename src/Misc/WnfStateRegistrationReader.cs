using Microsoft.Win32;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Security.AccessControl;

namespace Kwerty.DviZe.Win.Wnf.Misc;

public static class WnfStateRegistrationReader
{
    public static IEnumerable<Registration> GetAll(WnfLifetime lifetime)
    {
        var keyPath = lifetime switch
        {
            WnfLifetime.WellKnown => @"SYSTEM\CurrentControlSet\Control\Notifications",
            WnfLifetime.Persistent => @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Notifications",
            WnfLifetime.Volatile => @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\VolatileNotifications",
            _ => throw new NotSupportedException(),
        };

        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        var result = new List<Registration>();
        foreach (string valueName in key.GetValueNames())
        {
            if (!ulong.TryParse(valueName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var stateName))
            {
                continue;
            }

            var payload = (byte[])key.GetValue(valueName);
            var securityDescriptor = new RawSecurityDescriptor(payload, 0);
            var maxStateSize = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(securityDescriptor.BinaryLength));
            result.Add(new Registration(stateName, securityDescriptor, (int)maxStateSize));
        }
        return result;
    }

    public sealed class Registration
    {
        internal Registration(ulong stateName, RawSecurityDescriptor securityDescriptor, int maxStateSize)
        {
            StateName = stateName;
            SecurityDescriptor = securityDescriptor;
            MaxStateSize = maxStateSize;
        }

        public ulong StateName { get; }

        public RawSecurityDescriptor SecurityDescriptor { get; }

        public int MaxStateSize { get; }
    }
}