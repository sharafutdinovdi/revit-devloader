using System;

namespace RevitDevLoader.Core;

public sealed class DevManifestException : Exception
{
    public DevManifestException(string message)
        : base(message)
    {
    }

    public DevManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
