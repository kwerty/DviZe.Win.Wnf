using System;

namespace Kwerty.DviZe.Win.Wnf;

public readonly ref struct WnfState(ReadOnlySpan<byte> data, uint changeStamp)
{
    public readonly ReadOnlySpan<byte> Data = data;

    public readonly uint ChangeStamp = changeStamp;
}

public readonly ref struct WnfState<T>(T data, uint changeStamp) where T : unmanaged
{
    public readonly T Data => data;

    public readonly uint ChangeStamp = changeStamp;
}
