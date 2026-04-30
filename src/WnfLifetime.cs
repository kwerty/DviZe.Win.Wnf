namespace Kwerty.DviZe.Win.Wnf;

public enum WnfLifetime
{
    // "Persistent" and "Volatile" are preferred over Microsoft's "Permanent" and "Persistent"
    // respectively. A state that can be created or deleted at any time is not truly permanent,
    // and a state that does not survive a reboot is better described as volatile.

    /// <summary>Built-in state names defined by the OS.</summary>
    WellKnown = 0,

    /// <summary>Persists across reboots.</summary>
    /// <remarks>Microsoft refers to this as <b>Permanent</b> internally.</remarks>
    Persistent = 1,

    /// <summary>Removed upon reboot.</summary>
    /// <remarks>Microsoft refers to this as <b>Persistent</b> internally.</remarks>
    Volatile = 2,

    /// <summary>Removed when the process ends.</summary>
    Temporary = 3,
}
