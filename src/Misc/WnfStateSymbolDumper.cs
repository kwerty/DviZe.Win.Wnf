using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Kwerty.DviZe.Win.Wnf.Misc;

public static class WnfStateSymbolDumper
{
    public static IEnumerable<Symbol> GetAll(string dllPath = null)
    {
        dllPath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ContentDeliveryManager.Utilities.dll");

        using var stream = File.OpenRead(dllPath);
        using var reader = new PEReader(stream);
        var rdata = reader.PEHeaders.SectionHeaders.First(s => s.Name == ".rdata");
        var rdataSpan = reader.GetSectionData(".rdata").GetContent().AsSpan();
        var rdataBase = reader.PEHeaders.PEHeader.ImageBase + (ulong)rdata.VirtualAddress;
        var strPrefix = Encoding.Unicode.GetBytes("WNF_");

        var result = new List<Symbol>();
        for (var i = 0; i <= rdataSpan.Length - strPrefix.Length; i++)
        {
            if (!rdataSpan.Slice(i, strPrefix.Length).SequenceEqual(strPrefix))
            {
                continue;
            }

            var strVA = BitConverter.GetBytes(rdataBase + (ulong)i);

            var pos = 0;
            while (true)
            {
                var structNameFieldOffset = IndexOf(rdataSpan, strVA, pos);
                if (structNameFieldOffset == -1)
                {
                    break;
                }

                var structIdFieldOffset = (int)(BinaryPrimitives.ReadUInt64LittleEndian(rdataSpan[(structNameFieldOffset - 8)..]) - rdataBase);
                var structDescFieldOffset = (int)(BinaryPrimitives.ReadUInt64LittleEndian(rdataSpan[(structNameFieldOffset + 8)..]) - rdataBase);

                if (structIdFieldOffset < 0 || structIdFieldOffset + 8 > rdata.SizeOfRawData
                    || structDescFieldOffset < 0 || structDescFieldOffset + 8 > rdata.SizeOfRawData)
                {
                    pos++;
                    continue;
                }

                result.Add(new Symbol
                {
                    StateName = BinaryPrimitives.ReadUInt64LittleEndian(rdataSpan[structIdFieldOffset..]),
                    InternalName = NullTerminated(rdataSpan[i..]),
                    InternalDescription = NullTerminated(rdataSpan[structDescFieldOffset..]),
                });

                break;
            }
        }
        return result;
    }

    static int IndexOf(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value, int startIndex)
    {
        var idx = span[startIndex..].IndexOf(value);
        return idx == -1 ? -1 : startIndex + idx;
    }

    static string NullTerminated(ReadOnlySpan<byte> data)
    {
        var chars = MemoryMarshal.Cast<byte, char>(data);
        return new string(chars[..chars.IndexOf('\0')]);
    }

    public sealed class Symbol
    {
        public ulong StateName { get; init; }

        public string InternalName { get; init; }

        public string InternalDescription { get; init; }
    }
}
