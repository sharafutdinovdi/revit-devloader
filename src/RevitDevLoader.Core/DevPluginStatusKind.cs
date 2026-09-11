namespace RevitDevLoader.Core;

public enum DevPluginStatusKind
{
    NotInstalled,
    InstalledNoPackage,
    Latest,
    UpdateAvailable,
    UnsupportedRevitVersion,
    CommandSlotUnavailable,
    PackageError
}
