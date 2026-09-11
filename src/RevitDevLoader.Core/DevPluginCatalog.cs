using System;
using System.Collections.Generic;

namespace RevitDevLoader.Core;

public static class DevPluginCatalog
{
    public static IReadOnlyList<DevPluginCatalogItem> CreateDefault() => Array.Empty<DevPluginCatalogItem>();
}
