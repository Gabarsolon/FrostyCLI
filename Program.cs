using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Frosty.Core;
using Frosty.Core.Mod;
using Frosty.ModSupport;
using FrostySdk;
using FrostySdk.Ebx;
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
            // Register pack URI scheme by instantiating a dummy WPF Application object
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            // Wire up assembly resolver immediately before anything else loads
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

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

            // Set up static app references before initialization
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            switch (command)
            {
                case "list-assets":
                    {
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string type = args.Length > 2 ? args[2].ToLowerInvariant() : "ebx";
                        ListAssets(type, logger);
                    }
                    break;

                case "export-asset":
                    {
                        if (args.Length < 5)
                        {
                            logger.LogError("Usage: export-asset <profile> <type> <asset_path_or_guid> <output_file>");
                            return;
                        }
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string type = args[2].ToLowerInvariant();
                        string assetIdent = args[3];
                        string outputFile = args[4];
                        ExportAsset(type, assetIdent, outputFile, logger);
                    }
                    break;

                case "create-project":
                    {
                        if (args.Length < 3)
                        {
                            logger.LogError("Usage: create-project <profile> <project_path>");
                            return;
                        }
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string projectPath = args[2];
                        CreateProject(projectPath, logger);
                    }
                    break;

                case "project-info":
                    {
                        if (args.Length < 3)
                        {
                            logger.LogError("Usage: project-info <profile> <project_path>");
                            return;
                        }
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string projectPath = args[2];
                        ShowProjectInfo(projectPath, logger);
                    }
                    break;

                case "import-asset":
                    {
                        if (args.Length < 7)
                        {
                            logger.LogError("Usage: import-asset <profile> <project_path> <type> <asset_path_or_guid> <input_file> <output_project_path>");
                            return;
                        }
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string projectPath = args[2];
                        string type = args[3].ToLowerInvariant();
                        string assetIdent = args[4];
                        string inputFile = args[5];
                        string outputProjectPath = args[6];
                        ImportAsset(projectPath, type, assetIdent, inputFile, outputProjectPath, logger);
                    }
                    break;

                case "build-mod":
                    {
                        if (args.Length < 4)
                        {
                            logger.LogError("Usage: build-mod <profile> <project_path> <output_mod_path> [title] [author] [category] [version]");
                            return;
                        }
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string projectPath = args[2];
                        string outputModPath = args[3];
                        
                        ModSettings settings = new ModSettings();
                        settings.Title = args.Length > 4 ? args[4] : "FrostyCLI Mod";
                        settings.Author = args.Length > 5 ? args[5] : "FrostyCLI";
                        settings.Category = args.Length > 6 ? args[6] : "Gameplay";
                        settings.Version = args.Length > 7 ? args[7] : "1.0";

                        BuildMod(projectPath, outputModPath, settings, logger);
                    }
                    break;

                case "apply-mods":
                    {
                        if (args.Length < 4)
                        {
                            logger.LogError("Usage: apply-mods <profile> <mod_pack_name> <mod1.fbmod> [mod2.fbmod ...]");
                            return;
                        }
                        string modPackName = args[2];
                        List<string> modPaths = new List<string>();
                        for (int i = 3; i < args.Length; i++)
                        {
                            modPaths.Add(args[i]);
                        }

                        ApplyMods(profileName, modPackName, modPaths, logger);
                    }
                    break;

                case "launch":
                    {
                        if (args.Length < 3)
                        {
                            logger.LogError("Usage: launch <profile> <mod_pack_name> [additional_args]");
                            return;
                        }
                        if (InitializeFrosty(profileName, logger) == null) return;
                        string modPackName = args[2];
                        string additionalArgs = args.Length > 3 ? string.Join(" ", args.Skip(3)) : "";
                        LaunchGame(modPackName, additionalArgs, logger);
                    }
                    break;

                default:
                    logger.LogError($"Unknown command: {command}");
                    PrintUsage();
                    break;
            }

            Console.WriteLine("Done.");
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string text = (args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            if (text.StartsWith("SharpDX") || text.StartsWith("Newtonsoft"))
            {
                return Assembly.LoadFile(Path.Combine(baseDir, "ThirdParty", text + ".dll"));
            }
            if (text.Equals("EbxClasses"))
            {
                return Assembly.LoadFile(Path.Combine(baseDir, "Profiles", ProfilesLibrary.SDKFilename + ".dll"));
            }
            if (App.PluginManager == null)
            {
                return null;
            }
            if (App.PluginManager.IsThirdPartyDll(text))
            {
                return Assembly.LoadFile(Path.Combine(baseDir, "ThirdParty", text + ".dll"));
            }
            return App.PluginManager.GetPluginAssembly(text);
        }

        static void PrintUsage()
        {
            Console.WriteLine("FrostyCLI - CLI Tool for Frosty Toolsuite");
            Console.WriteLine("Usage: FrostyCli.exe <command> <profile> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list-assets <profile> [type]                              Lists all assets (type: ebx, res, chunk)");
            Console.WriteLine("  export-asset <profile> <type> <ident> <output_file>       Exports an asset (ident is name or GUID)");
            Console.WriteLine("  import-asset <profile> <project> <type> <ident> <file> <out_project> Imports a file into a project");
            Console.WriteLine("  create-project <profile> <project_path>                   Creates an empty .fbproject");
            Console.WriteLine("  project-info <profile> <project_path>                     Prints information about a project");
            Console.WriteLine("  build-mod <profile> <project> <out_mod> [meta...]         Builds a .fbmod file from a project");
            Console.WriteLine("  apply-mods <profile> <pack_name> <mod1> [mod2...]        Compiles mods into a ModData pack");
            Console.WriteLine("  launch <profile> <pack_name> [args]                       Launches the game with a mod pack");
            Console.WriteLine("  help                                                      Shows this help message");
        }

        static AssetManager InitializeFrosty(string profileName, ILogger logger)
        {
            App.PluginManager = new PluginManager(logger, PluginManagerType.Editor);
            Config.Load("");
            ProfilesLibrary.Initialize(App.PluginManager.Profiles);

            if (!ProfilesLibrary.Initialize(profileName))
            {
                logger.LogError($"Failed to load profile: {profileName}");
                return null;
            }

            TypeLibrary.Initialize(true);

            string gamePath = Config.Get<string>("GamePath", null, ConfigScope.Game, null);
            if (string.IsNullOrEmpty(gamePath))
            {
                logger.LogError("GamePath is not configured for this profile.");
                return null;
            }

            App.FileSystem = new FileSystem(gamePath);
            foreach (FileSystemSource source in ProfilesLibrary.Sources)
            {
                App.FileSystem.AddSource(source.Path, source.SubDirs);
            }

            byte[] key = null;
            if (ProfilesLibrary.RequiresKey)
            {
                if (File.Exists(ProfilesLibrary.CacheName + ".key"))
                {
                    key = NativeReader.ReadInStream(new FileStream(ProfilesLibrary.CacheName + ".key", FileMode.Open, FileAccess.Read));
                }
            }

            App.FileSystem.Initialize(key);

            App.ResourceManager = new ResourceManager(App.FileSystem);
            App.ResourceManager.SetLogger(logger);
            App.ResourceManager.Initialize();

            App.AssetManager = new AssetManager(App.FileSystem, App.ResourceManager);
            App.PluginManager.Initialize();

            App.AssetManager.SetLogger(logger);
            AssetManagerImportResult result = new AssetManagerImportResult();
            App.AssetManager.Initialize(true, result);

            return App.AssetManager;
        }

        static void ListAssets(string type, ILogger logger)
        {
            if (type == "ebx")
            {
                logger.Log("--- EBX Assets ---");
                foreach (var ebx in App.AssetManager.EnumerateEbx())
                {
                    Console.WriteLine(ebx.Name);
                }
            }
            else if (type == "res")
            {
                logger.Log("--- RES Assets ---");
                foreach (var res in App.AssetManager.EnumerateRes())
                {
                    Console.WriteLine($"{res.Name} ({res.Type})");
                }
            }
            else if (type == "chunk")
            {
                logger.Log("--- Chunk Assets ---");
                foreach (var chunk in App.AssetManager.EnumerateChunks())
                {
                    Console.WriteLine(chunk.Id);
                }
            }
            else
            {
                logger.LogError($"Unsupported asset type: {type}");
            }
        }

        static void ExportAsset(string type, string assetIdent, string outputFile, ILogger logger)
        {
            try
            {
                Stream stream = null;
                string extension = "";

                if (type == "ebx")
                {
                    EbxAssetEntry entry = App.AssetManager.GetEbxEntry(assetIdent);
                    if (entry == null && Guid.TryParse(assetIdent, out Guid guid))
                    {
                        entry = App.AssetManager.GetEbxEntry(guid);
                    }
                    if (entry == null)
                    {
                        logger.LogError($"EBX asset not found: {assetIdent}");
                        return;
                    }
                    stream = App.AssetManager.GetEbxStream(entry);
                    extension = ".ebx";
                }
                else if (type == "res")
                {
                    ResAssetEntry entry = App.AssetManager.GetResEntry(assetIdent);
                    if (entry == null && ulong.TryParse(assetIdent, out ulong rid))
                    {
                        entry = App.AssetManager.GetResEntry(rid);
                    }
                    if (entry == null)
                    {
                        logger.LogError($"RES asset not found: {assetIdent}");
                        return;
                    }
                    stream = App.AssetManager.GetRes(entry);
                    extension = ".res";
                }
                else if (type == "chunk")
                {
                    if (Guid.TryParse(assetIdent, out Guid chunkId))
                    {
                        ChunkAssetEntry entry = App.AssetManager.GetChunkEntry(chunkId);
                        if (entry != null)
                        {
                            stream = App.AssetManager.GetChunk(entry);
                            extension = ".chunk";
                        }
                    }
                    if (stream == null)
                    {
                        logger.LogError($"Chunk asset not found or invalid Guid: {assetIdent}");
                        return;
                    }
                }
                else
                {
                    logger.LogError($"Unsupported export type: {type}");
                    return;
                }

                if (stream == null)
                {
                    logger.LogError("Failed to get asset stream.");
                    return;
                }

                string outPath = outputFile;
                if (!outPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    outPath += extension;
                }

                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }

                logger.Log($"Successfully exported asset to: {outPath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Export failed: {ex.Message}");
            }
        }

        static void CreateProject(string projectPath, ILogger logger)
        {
            try
            {
                FrostyProject project = new FrostyProject();
                project.Save(projectPath);
                logger.Log($"Successfully created project: {projectPath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to create project: {ex.Message}");
            }
        }

        static void ShowProjectInfo(string projectPath, ILogger logger)
        {
            try
            {
                FrostyProject project = new FrostyProject();
                if (!project.Load(projectPath))
                {
                    logger.LogError("Failed to load project file.");
                    return;
                }

                logger.Log($"Project Title: {project.ModSettings.Title}");
                logger.Log($"Project Author: {project.ModSettings.Author}");
                logger.Log($"Project Version: {project.ModSettings.Version}");
                logger.Log($"Dirty Assets: {App.AssetManager.GetDirtyCount()}");
                logger.Log($"Modified Assets: {App.AssetManager.GetModifiedCount()}");

                logger.Log("");
                logger.Log("--- Modified Assets ---");
                foreach (var entry in App.AssetManager.EnumerateEbx(modifiedOnly: true))
                {
                    logger.Log($"[EBX] {entry.Name} ({entry.Type})");
                }
                foreach (var entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
                {
                    logger.Log($"[RES] {entry.Name} (Type: {entry.ResType})");
                }
                foreach (var entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
                {
                    logger.Log($"[CHUNK] {entry.Id}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to load project: {ex.Message}");
            }
        }

        static void ImportAsset(string projectPath, string type, string assetIdent, string inputFile, string outputProjectPath, ILogger logger)
        {
            try
            {
                FrostyProject project = new FrostyProject();
                if (!project.Load(projectPath))
                {
                    logger.LogError("Failed to load base project.");
                    return;
                }

                if (!File.Exists(inputFile))
                {
                    logger.LogError($"Input file not found: {inputFile}");
                    return;
                }

                if (type == "ebx")
                {
                    EbxAssetEntry entry = App.AssetManager.GetEbxEntry(assetIdent);
                    if (entry == null && Guid.TryParse(assetIdent, out Guid guid))
                    {
                        entry = App.AssetManager.GetEbxEntry(guid);
                    }
                    if (entry == null)
                    {
                        logger.LogError($"EBX asset not found: {assetIdent}");
                        return;
                    }

                    using (EbxReader reader = EbxReader.CreateReader(new FileStream(inputFile, FileMode.Open, FileAccess.Read), App.FileSystem, true))
                    {
                        EbxAsset newAsset = reader.ReadAsset<EbxAsset>();
                        
                        EbxAsset origAsset = App.AssetManager.GetEbx(entry);
                        newAsset.SetFileGuid(origAsset.FileGuid);
                        dynamic rootObj = newAsset.RootObject;
                        rootObj.SetInstanceGuid(new AssetClassGuid(origAsset.RootInstanceGuid, -1));

                        App.AssetManager.ModifyEbx(entry.Name, newAsset);
                    }
                }
                else if (type == "res")
                {
                    ResAssetEntry entry = App.AssetManager.GetResEntry(assetIdent);
                    if (entry == null && ulong.TryParse(assetIdent, out ulong rid))
                    {
                        entry = App.AssetManager.GetResEntry(rid);
                    }
                    if (entry == null)
                    {
                        logger.LogError($"RES asset not found: {assetIdent}");
                        return;
                    }
                    byte[] buffer = File.ReadAllBytes(inputFile);
                    App.AssetManager.ModifyRes(entry.Name, buffer);
                }
                else if (type == "chunk")
                {
                    if (!Guid.TryParse(assetIdent, out Guid chunkId))
                    {
                        logger.LogError($"Invalid Chunk Guid: {assetIdent}");
                        return;
                    }
                    ChunkAssetEntry entry = App.AssetManager.GetChunkEntry(chunkId);
                    if (entry == null)
                    {
                        logger.LogError($"Chunk not found: {chunkId}");
                        return;
                    }
                    byte[] buffer = File.ReadAllBytes(inputFile);
                    App.AssetManager.ModifyChunk(chunkId, buffer);
                }
                else
                {
                    logger.LogError($"Unsupported import type: {type}");
                    return;
                }

                project.Save(outputProjectPath);
                logger.Log($"Successfully imported asset and saved project to: {outputProjectPath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Import failed: {ex.Message}");
            }
        }

        static void BuildMod(string projectPath, string outputModPath, ModSettings settings, ILogger logger)
        {
            try
            {
                FrostyProject project = new FrostyProject();
                if (!project.Load(projectPath))
                {
                    logger.LogError("Failed to load project.");
                    return;
                }

                CancellationTokenSource cts = new CancellationTokenSource();
                project.WriteToMod(outputModPath, settings, true, cts.Token);
                logger.Log($"Successfully built mod: {outputModPath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to build mod: {ex.Message}");
            }
        }

        static void ApplyMods(string profileName, string modPackName, List<string> modPaths, ILogger logger)
        {
            try
            {
                App.PluginManager = new PluginManager(logger, PluginManagerType.ModManager);
                Config.Load("");
                TypeLibrary.Initialize(true);
                ProfilesLibrary.Initialize(App.PluginManager.Profiles);

                if (!ProfilesLibrary.Initialize(profileName))
                {
                    logger.LogError($"Failed to load profile: {profileName}");
                    return;
                }

                string gamePath = Config.Get<string>("GamePath", null, ConfigScope.Game, null);
                App.FileSystem = new FileSystem(gamePath);
                foreach (FileSystemSource source in ProfilesLibrary.Sources)
                {
                    App.FileSystem.AddSource(source.Path, source.SubDirs);
                }

                byte[] key = null;
                if (ProfilesLibrary.RequiresKey && File.Exists(ProfilesLibrary.CacheName + ".key"))
                {
                    key = NativeReader.ReadInStream(new FileStream(ProfilesLibrary.CacheName + ".key", FileMode.Open, FileAccess.Read));
                }
                App.FileSystem.Initialize(key);

                App.PluginManager.Initialize();

                FrostyModExecutor executor = new FrostyModExecutor();
                CancellationTokenSource cts = new CancellationTokenSource();
                
                string rootPath = AppDomain.CurrentDomain.BaseDirectory + $"Mods/{profileName}/";
                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                List<string> modFileNames = new List<string>();
                foreach (var path in modPaths)
                {
                    string dest = Path.Combine(rootPath, Path.GetFileName(path));
                    if (File.Exists(path) && path != dest)
                    {
                        File.Copy(path, dest, true);
                    }
                    modFileNames.Add(Path.GetFileName(path));
                }

                string additionalArgs = Config.Get<string>("CommandLineArgs", "", ConfigScope.Game);

                logger.Log("Running Mod Executor...");
                int res = executor.Run(App.FileSystem, cts.Token, logger, rootPath, modPackName, additionalArgs, modFileNames.ToArray());
                if (res == 0)
                {
                    logger.Log("Successfully applied mods.");
                }
                else
                {
                    logger.LogError($"Mod executor exited with code: {res}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to apply mods: {ex.Message}");
            }
        }

        static void LaunchGame(string modPackName, string additionalArgs, ILogger logger)
        {
            try
            {
                string modDirName = "ModData\\" + modPackName;
                string modDataPath = App.FileSystem.BasePath + modDirName + "\\";

                logger.Log($"Launching game via ModData pack: {modPackName}");
                FrostyModExecutor.LaunchGame(App.FileSystem.BasePath, modDirName, modDataPath, additionalArgs);
                logger.Log("Launch sequence triggered.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to launch game: {ex.Message}");
            }
        }
    }
}
