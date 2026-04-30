namespace Kwerty.DviZe.Win.Wnf.Misc;

public sealed class WnfStateNameInfo
{
    WnfStateNameInfo()
    {
    }

    public int Version { get; init; }

    public WnfLifetime Lifetime { get; init; }

    public WnfScope Scope { get; init; }

    public bool IsDataPersistent { get; init; }

    public static WnfStateNameInfo Parse(ulong stateName)
    {
        // Magic constant reverse engineered by nag0mez.
        // https://pwnedcoffee.com/blog/wnf-chronicles-i-introduction/#statenames

        var decoded = stateName ^ 0x41C64E6DA3BC0074UL;

        return new WnfStateNameInfo
        {
            Version = (int)(decoded & 0xF),
            Lifetime = (WnfLifetime)((decoded >> 4) & 0x3),
            Scope = (WnfScope)((decoded >> 6) & 0xF),
            IsDataPersistent = ((decoded >> 10) & 0x1) == 1,
        };
    }
}
