using Skua.Flash.Ruffle;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: RuffleAssetPack <output-directory>");
    return 2;
}

try
{
    string outputDirectory = Path.GetFullPath(args[0]);
    int exportedCount = RuffleAssetExporter.ExportWebAssets(outputDirectory);
    Console.WriteLine($"Exported {exportedCount} patched Ruffle web assets to: {outputDirectory}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Ruffle asset export failed: {ex}");
    return 1;
}
