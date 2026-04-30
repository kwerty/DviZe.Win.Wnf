using System;

namespace Kwerty.DviZe.Win.Wnf;

public class WnfException(string message = "Operation failed.", Exception innerException = null)
    : Exception(message, innerException)
{
    internal const string InvalidStateName = "Invalid state name.";
    internal const string MustBeRunningAsLocalSystem = "Must be running as LocalSystem to perform this operation.";
    internal const string AccessDenied = "Access denied.";
}

public sealed class WnfBufferTooSmallException(int requiredSize)
    : WnfException($"Buffer too small, must be {requiredSize} or greater.")
{
    public int RequiredSize => requiredSize;
}