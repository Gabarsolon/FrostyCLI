using System;
using System.IO;
using System.Linq;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace FrostyCli
{
    class ConsoleLogger : ILogger
    {
        public void Log(string text, params object[] vars)
        {
            Console.WriteLine(string.Format(text, vars));
        }

        public void LogWarning(string text, params object[] vars)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING: " + string.Format(text, vars));
            Console.ResetColor();
        }

        public void LogError(string text, params object[] vars)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + string.Format(text, vars));
            Console.ResetColor();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string command = args[0].ToLowerInvariant();
            
            if (command == "help")
            {
                PrintUsage();
                return;
            }
            
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Missing profile argument.");
                PrintUsage();
                return;
            }

            string profileName = args[1];
            ILogger logger = new ConsoleLogger();

            AssetManager assetManager = InitializeFrosty(profileName, logger);
            if (assetManager == null)
            {
                return;
            }

            switch (command)
            {
                case "list":
                    ListAssets(assetManager, logger);
                    break;
                case "export":
                    if (args.Length < 4)
                    {
                        logger.LogError("Usage: export <profile> <asset_path> <output_dir>");
                        return;
                    }
                    string assetPath = args[2];
                    string outputDir = args[3];
                    ExportAsset(assetManager, assetPath, outputDir, logger);
                    break;
                default:
                    logger.LogError($"Unknown command: {command}");
                    PrintUsage();
                    break;
            }

            Console.WriteLine("Done.");
        }

        static void PrintUsage()
        {
            Console.WriteLine("FrostyCLI - Command Line Interface for Frosty Toolsuite");
            Console.WriteLine("Usage: FrostyCli.exe <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list <profile>                                  Lists all EBX assets in the game");
            Console.WriteLine("  export <profile> <asset_path> <output_dir>      Exports an asset to a directory");
            Console.WriteLine("  help                                            Shows this help message");
        }

        static AssetManager InitializeFrosty(string profileName, ILogger logger)
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Config.Load("");

            TypeLibrary.Initialize(true);
            PluginManager pluginManager = new PluginManager(logger, PluginManagerType.Editor);
            ProfilesLibrary.Initialize(pluginManager.Profiles);

            if (!ProfilesLibrary.Initialize(profileName))
            {
                logger.LogError($"There was an error when trying to load game using specified profile: {profileName}");
                return null;
            }

            string gamePath = Config.Get<string>("GamePath", null, ConfigScope.Game, null);
            if (string.IsNullOrEmpty(gamePath))
            {
                logger.LogError("GamePath is not configured for this profile.");
                return null;
            }

            FileSystem fileSystem = new FileSystem(gamePath);
            foreach (FileSystemSource source in ProfilesLibrary.Sources)
            {
                fileSystem.AddSource(source.Path, source.SubDirs);
            }

            byte[] key = null;
            if (ProfilesLibrary.RequiresKey)
            {
                if (File.Exists(ProfilesLibrary.CacheName + ".key"))
                {
                    key = NativeReader.ReadInStream(new FileStream(ProfilesLibrary.CacheName + ".key", FileMode.Open, FileAccess.Read));
                }
                else
                {
                    logger.LogWarning("Profile requires a key, but no .key file found in cache.");
                }
            }

            fileSystem.Initialize(key);

            ResourceManager resourceManager = new ResourceManager(fileSystem);
            resourceManager.SetLogger(logger);
            resourceManager.Initialize();

            AssetManager assetManager = new AssetManager(fileSystem, resourceManager);
            pluginManager.Initialize();

            assetManager.SetLogger(logger);
            AssetManagerImportResult result = new AssetManagerImportResult();
            assetManager.Initialize(true, result);

            return assetManager;
        }

        static void ListAssets(AssetManager assetManager, ILogger logger)
        {
            logger.Log("--- EBX Assets ---");
            foreach (var ebx in assetManager.EnumerateEbx())
            {
                Console.WriteLine(ebx.Name);
            }
            logger.Log($"Total EBX loaded: {assetManager.EnumerateEbx().Count()}");
        }

        static void ExportAsset(AssetManager assetManager, string assetPath, string outputDir, ILogger logger)
        {
            EbxAssetEntry entry = assetManager.GetEbxEntry(assetPath);
            if (entry == null)
            {
                logger.LogError($"Asset not found: {assetPath}");
                return;
            }

            try
            {
                Stream stream = assetManager.GetEbxStream(entry);
                if (stream == null)
                {
                    logger.LogError("Failed to get stream for asset.");
                    return;
                }

                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                
                string outFileName = Path.Combine(outputDir, entry.Filename + ".ebx");
                string outDir = Path.GetDirectoryName(outFileName);
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                using (FileStream fs = new FileStream(outFileName, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }

                logger.Log($"Successfully exported to: {outFileName}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Export failed: {ex.Message}");
            }
        }
    }
}
