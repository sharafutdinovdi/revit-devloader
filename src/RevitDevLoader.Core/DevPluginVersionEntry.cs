using System;

namespace RevitDevLoader.Core;

public sealed class DevPluginVersionEntry
{
    public DevPluginVersionEntry(string revitVersion, string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(revitVersion))
            throw new ArgumentException("Revit version is required.", nameof(revitVersion));
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));

        RevitVersion = revitVersion.Trim();
        AssemblyPath = assemblyPath.Trim();
    }

    public string RevitVersion { get; }

    public string AssemblyPath { get; }
}
