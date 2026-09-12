using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace RevitDevLoader.Core;

public sealed class DevPluginRegistry
{
    public const int CommandSlotCount = 20;

    private readonly string _localAppDataRoot;
    private readonly Action<string, string> _writeTemporaryFile;

    public DevPluginRegistry()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), null)
    {
    }

    public DevPluginRegistry(string localAppDataRoot)
        : this(localAppDataRoot, null)
    {
    }

    public DevPluginRegistry(string localAppDataRoot, Action<string, string>? writeTemporaryFile)
    {
        if (string.IsNullOrWhiteSpace(localAppDataRoot))
            throw new ArgumentException("Local app data root is required.", nameof(localAppDataRoot));

        _localAppDataRoot = localAppDataRoot;
        _writeTemporaryFile = writeTemporaryFile ?? WriteTemporaryFile;
    }

    public string GetManifestPath(string pluginName)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            throw new ArgumentException("Plugin name is required.", nameof(pluginName));

        return Path.Combine(
            _localAppDataRoot,
            "RevitDevLoader",
            "plugins",
            $"{pluginName.Trim()}.devmanifest");
    }

    public DevPluginManifest Load(string pluginName)
    {
        var manifestPath = GetManifestPath(pluginName);
        if (!File.Exists(manifestPath))
            throw new DevManifestException($"Dev manifest not found: {manifestPath}");

        try
        {
            return DevManifestSerializer.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
        }
        catch (DevManifestException exception)
        {
            throw new DevManifestException($"Cannot parse dev manifest because it is damaged: {manifestPath}. {exception.Message}", exception);
        }
        catch (Exception exception)
        {
            throw new DevManifestException($"Cannot read dev manifest: {manifestPath}", exception);
        }
    }

    public bool Exists(string pluginName)
    {
        return File.Exists(GetManifestPath(pluginName));
    }

    public bool Delete(string pluginName)
    {
        var manifestPath = GetManifestPath(pluginName);
        if (!File.Exists(manifestPath))
            return false;

        File.Delete(manifestPath);
        return true;
    }

    public bool Delete(string pluginName, string applicationDataRoot, out IReadOnlyList<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(applicationDataRoot))
            throw new ArgumentException("Application data root is required.", nameof(applicationDataRoot));

        var manifestPath = GetManifestPath(pluginName);
        if (!File.Exists(manifestPath))
        {
            warnings = Array.Empty<string>();
            return false;
        }

        var manifest = Load(pluginName);
        var warningList = new List<string>();
        if (manifest.PluginType == DevPluginType.Application)
        {
            foreach (var version in manifest.Versions.Select(item => item.RevitVersion).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var addinPath = GetApplicationManifestPath(applicationDataRoot, version, manifest.PluginName);
                if (!File.Exists(addinPath))
                    continue;

                if (IsOwnedApplicationManifest(addinPath, manifest.PluginName))
                {
                    File.Delete(addinPath);
                    continue;
                }

                warningList.Add($"Manifest '{addinPath}' was not deleted: its Assembly is outside the DevLoader directory for plugin '{manifest.PluginName}'.");
            }
        }

        File.Delete(manifestPath);
        warnings = warningList;
        return true;
    }

    public IReadOnlyList<string> GetRegisteredPluginNames()
    {
        var directory = GetManifestDirectory();
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.GetFiles(directory, "*.devmanifest")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> EnsureCommandSlots()
    {
        var errors = new List<string>();
        var manifests = new List<DevPluginManifest>();
        foreach (var pluginName in GetRegisteredPluginNames())
        {
            try
            {
                var manifest = Load(pluginName);
                if (manifest.PluginType == DevPluginType.Command)
                    manifests.Add(manifest);
            }
            catch (DevManifestException exception)
            {
                errors.Add(exception.Message);
            }
        }

        foreach (var manifest in manifests)
        {
            if (!TryAssignCommandSlots(manifest.PluginName, manifest.Commands, out var commands, out var error))
            {
                errors.Add(error);
                continue;
            }
            if (!manifest.Commands.Select(command => command.Slot).SequenceEqual(commands.Select(command => command.Slot)))
                Save(CopyWithCommands(manifest, commands));
        }

        return errors;
    }

    public bool TryGetCommandSlot(string pluginName, out int commandSlot, out string error)
    {
        commandSlot = 0;
        error = string.Empty;
        if (Exists(pluginName))
        {
            try
            {
                var existing = Load(pluginName);
                if (existing.CommandSlot.HasValue)
                {
                    commandSlot = existing.CommandSlot.Value;
                    return true;
                }
            }
            catch (DevManifestException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        var occupied = GetOccupiedCommandSlots();
        commandSlot = Enumerable.Range(1, CommandSlotCount).FirstOrDefault(candidate => !occupied.Contains(candidate));
        if (commandSlot != 0)
            return true;

        error = CreateSlotsExhaustedMessage(occupied.Count);
        return false;
    }

    public bool TryLoadByCommandSlot(int commandSlot, out DevPluginManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        if (commandSlot < 1 || commandSlot > CommandSlotCount)
        {
            error = $"Invalid DevLoader slot: {commandSlot}.";
            return false;
        }

        foreach (var pluginName in GetRegisteredPluginNames())
        {
            try
            {
                var candidate = Load(pluginName);
                if (candidate.Commands.Any(command => command.Slot == commandSlot))
                {
                    manifest = candidate;
                    return true;
                }
            }
            catch (DevManifestException)
            {
            }
        }

        error = $"DevLoader slot {commandSlot:00} is not assigned to a plugin. Open the manager and reinstall the plugin.";
        return false;
    }

    public void Save(DevPluginManifest manifest)
    {
        if (manifest is null)
            throw new ArgumentNullException(nameof(manifest));

        var manifestPath = GetManifestPath(manifest.PluginName);
        var directory = Path.GetDirectoryName(manifestPath);
        if (directory is null)
            throw new DevManifestException($"Cannot resolve manifest directory: {manifestPath}");

        WriteAtomically(manifestPath, DevManifestSerializer.Serialize(manifest), _writeTemporaryFile);
    }

    internal bool CanReplaceApplicationManifest(string addinPath, string pluginName, out string warning)
    {
        warning = string.Empty;
        if (!File.Exists(addinPath) || IsOwnedApplicationManifest(addinPath, pluginName))
            return true;

        warning = $"Manifest '{addinPath}' already exists and is not owned by RevitDevLoader. The file was not changed.";
        return false;
    }

    internal bool IsRegisteredApplicationAssemblyPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            return false;
        foreach (var pluginName in GetRegisteredPluginNames())
        {
            try
            {
                var manifest = Load(pluginName);
                if (manifest.PluginType == DevPluginType.Application && manifest.Versions.Any(version =>
                        string.Equals(Path.GetFullPath(version.AssemblyPath), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            catch (DevManifestException)
            {
            }
        }
        return false;
    }

    internal bool IsManagedAssemblyPath(string pluginName, string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(pluginName) || string.IsNullOrWhiteSpace(assemblyPath))
            return false;

        try
        {
            var pluginRoot = Path.GetFullPath(Path.Combine(GetManifestDirectory(), pluginName.Trim()))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var resolvedAssembly = Path.GetFullPath(assemblyPath.Trim());
            return resolvedAssembly.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static string GetApplicationManifestPath(string applicationDataRoot, string revitVersion, string pluginName)
    {
        return Path.Combine(
            applicationDataRoot,
            "Autodesk",
            "Revit",
            "Addins",
            revitVersion,
            pluginName + ".addin");
    }

    internal static void WriteAtomically(string path, string content, Action<string, string>? writeTemporaryFile = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
            throw new DevManifestException($"Cannot resolve manifest directory: {path}");

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            (writeTemporaryFile ?? WriteTemporaryFile)(temporaryPath, content);
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
            }
        }
    }

    private static void WriteTemporaryFile(string path, string content)
    {
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private string GetManifestDirectory()
    {
        return Path.Combine(_localAppDataRoot, "RevitDevLoader", "plugins");
    }

    private bool IsOwnedApplicationManifest(string addinPath, string pluginName)
    {
        try
        {
            var document = XDocument.Load(addinPath);
            var addIns = document.Descendants().Where(item => item.Name.LocalName == "AddIn").ToList();
            if (addIns.Count != 1)
                return false;

            var assemblyPath = addIns[0].Elements()
                .FirstOrDefault(item => item.Name.LocalName == "Assembly")
                ?.Value;
            return IsManagedAssemblyPath(pluginName, assemblyPath ?? string.Empty);
        }
        catch
        {
            return false;
        }
    }

    private HashSet<int> GetOccupiedCommandSlots()
    {
        var occupied = new HashSet<int>();
        foreach (var pluginName in GetRegisteredPluginNames())
        {
            try
            {
                foreach (var command in Load(pluginName).Commands)
                    if (command.Slot.HasValue)
                        occupied.Add(command.Slot.Value);
            }
            catch (DevManifestException)
            {
            }
        }

        return occupied;
    }

    public bool TryAssignCommandSlots(string pluginName, IEnumerable<DevPackageCommand> requested,
        out IReadOnlyList<DevPackageCommand> commands, out string error)
    {
        var occupied = new HashSet<int>();
        var previous = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetRegisteredPluginNames())
        {
            DevPluginManifest manifest;
            try
            {
                manifest = Load(name);
            }
            catch (DevManifestException)
            {
                continue;
            }
            foreach (var command in manifest.Commands)
            {
                if (string.Equals(name, pluginName, StringComparison.OrdinalIgnoreCase))
                    previous[command.Id] = command.Slot;
                else if (command.Slot.HasValue)
                    occupied.Add(command.Slot.Value);
            }
        }
        var assigned = requested.Select(command => command.WithSlot(null)).ToList();
        foreach (var command in assigned)
            if (previous.TryGetValue(command.Id, out var slot) && slot is >= 1 and <= CommandSlotCount && occupied.Add(slot.Value))
                command.Slot = slot;
        foreach (var command in assigned.Where(command => !command.Slot.HasValue))
        {
            var slot = Enumerable.Range(1, CommandSlotCount).FirstOrDefault(candidate => !occupied.Contains(candidate));
            if (slot == 0)
            {
                commands = Array.Empty<DevPackageCommand>();
                error = CreateSlotsExhaustedMessage(occupied.Count);
                return false;
            }
            command.Slot = slot;
            occupied.Add(slot);
        }
        commands = assigned;
        error = string.Empty;
        return true;
    }

    private static DevPluginManifest CopyWithCommands(DevPluginManifest manifest, IReadOnlyList<DevPackageCommand> commands)
    {
        return new DevPluginManifest(
            manifest.PluginName, manifest.DisplayName, manifest.CommandType,
            manifest.ReleaseId, manifest.AssemblyVersion, manifest.RunRoot, manifest.PackagePath,
            commands.FirstOrDefault()?.Slot, manifest.UpdatedUtc, manifest.Versions,
            manifest.PluginType, manifest.ApplicationClass, manifest.IconPath, manifest.Description, commands);
    }

    private static string CreateSlotsExhaustedMessage(int occupiedCount)
    {
        return $"All {CommandSlotCount} DevLoader slots are occupied ({occupiedCount}/{CommandSlotCount}). " +
            "Remove an unused plugin or rebuild DevLoader with more slots.";
    }
}
