using System.Reflection;

namespace Skua.Flash.Ruffle;

/// <summary>
/// Exports the embedded Ruffle web client for platforms that package web assets separately.
/// The Ruffle JavaScript bundle receives the same compatibility patches as the in-process host.
/// </summary>
public static class RuffleAssetExporter
{
    /// <summary>
    /// Exports every embedded resource under the web folder to the specified directory.
    /// </summary>
    /// <param name="outputDirectory">Destination directory for the exported web assets.</param>
    /// <returns>The number of exported files.</returns>
    public static int ExportWebAssets(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        string destinationRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(destinationRoot);

        Assembly assembly = typeof(RuffleAssetExporter).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("web/", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
            throw new InvalidOperationException("The Ruffle assembly contains no embedded web assets.");

        string rootPrefix = Path.TrimEndingDirectorySeparator(destinationRoot) + Path.DirectorySeparatorChar;

        foreach (string resourceName in resources)
        {
            string relativePath = resourceName[4..].Replace('/', Path.DirectorySeparatorChar);
            string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));

            if (!destinationPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Embedded resource path escaped the destination directory: {resourceName}");

            using Stream resourceStream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource could not be opened: {resourceName}");

            using MemoryStream memoryStream = new();
            resourceStream.CopyTo(memoryStream);
            byte[] data = memoryStream.ToArray();

            if (relativePath.Equals(Path.Combine("ruffle", "ruffle.js"), StringComparison.OrdinalIgnoreCase))
                data = RuffleHost.PatchRuffle(data);

            string? parentDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDirectory))
                Directory.CreateDirectory(parentDirectory);

            File.WriteAllBytes(destinationPath, data);
        }

        return resources.Length;
    }
}
