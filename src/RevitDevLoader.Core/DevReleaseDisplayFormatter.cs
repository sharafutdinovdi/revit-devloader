using System;
using System.Globalization;
using System.Linq;

namespace RevitDevLoader.Core;

public static class DevReleaseDisplayFormatter
{
    public static string Format(string releaseId, string assemblyVersion)
    {
        var version = FormatAssemblyVersion(assemblyVersion);
        var date = FormatReleaseDate(releaseId);

        if (!string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(date))
            return $"{version} · {date}";

        if (!string.IsNullOrWhiteSpace(version))
            return version;

        if (!string.IsNullOrWhiteSpace(date))
            return date;

        return string.IsNullOrWhiteSpace(releaseId) ? "version unknown" : releaseId.Trim();
    }

    private static string FormatAssemblyVersion(string assemblyVersion)
    {
        if (string.IsNullOrWhiteSpace(assemblyVersion))
            return string.Empty;

        var value = assemblyVersion.Trim();
        if (string.Equals(value, "1.0.0.0", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var parts = value.Split('.').ToList();
        while (parts.Count > 2 && parts[parts.Count - 1] == "0")
            parts.RemoveAt(parts.Count - 1);

        return "v" + string.Join(".", parts);
    }

    private static string FormatReleaseDate(string releaseId)
    {
        if (string.IsNullOrWhiteSpace(releaseId) || releaseId.Length < 15)
            return string.Empty;

        var stamp = releaseId.Substring(0, 15);
        return DateTime.TryParseExact(
            stamp,
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
    }
}
