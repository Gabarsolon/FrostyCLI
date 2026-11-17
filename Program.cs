using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using Frosty.Core;
using Frosty.Core.Mod;
using Frosty.Core.Viewport;
using Frosty.ModSupport;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;

namespace FrostyCli
{
    class ConsoleLogger : ILogger
    {
        public void Log(string text, params object[] vars)
        {
            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "]: " + (vars.Length > 0 ? string.Format(text, vars) : text));
        }

        public void LogWarning(string text, params object[] vars)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "]: WARNING: " + (vars.Length > 0 ? string.Format(text, vars) : text));
            Console.ResetColor();
        }

        public void LogError(string text, params object[] vars)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "]: ERROR: " + (vars.Length > 0 ? string.Format(text, vars) : text));
            Console.ResetColor();
        }
    }

    class Program
    {
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllname = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name;
            FileInfo fi = new FileInfo(Assembly.GetExecutingAssembly().Location);
            string baseDir = fi.DirectoryName;
            string fallbackDir = @"C:\Users\remus\dnSpy-net-win64\FrostyToolsuite\FrostyEditor\bin\Developer\Debug";

            if (dllname.Equals("EbxClasses"))
            {
                string path = Path.Combine(baseDir, "Profiles", ProfilesLibrary.SDKFilename + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
                path = Path.Combine(fallbackDir, "Profiles", ProfilesLibrary.SDKFilename + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
            }
            if (dllname.StartsWith("SharpDX") || dllname.StartsWith("Newtonsoft"))
            {
                string path = Path.Combine(baseDir, "ThirdParty", dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
                path = Path.Combine(baseDir, dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
                path = Path.Combine(baseDir, "lib", dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
                path = Path.Combine(fallbackDir, "ThirdParty", dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
                path = Path.Combine(fallbackDir, dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
            }

            if (App.PluginManager == null) return null;
            if (App.PluginManager.IsThirdPartyDll(dllname))
            {
                string path = Path.Combine(baseDir, "ThirdParty", dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
                path = Path.Combine(fallbackDir, "ThirdParty", dllname + ".dll");
                if (File.Exists(path)) return Assembly.LoadFile(path);
            }

            return App.PluginManager.GetPluginAssembly(dllname);
        }

        static void Main(string[] args)
        {
            // Register dummy WPF application if needed
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            // Determine if args[0] is profile or command
            string profileName;
            string command;
            string[] cmdArgs;

            var knownCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "help", "profile", "list-assets", "export-asset", "import-asset",
                "create-project", "project-info", "build-mod", "apply-mods", "launch",
                "import-legacy", "inject-legacy", "export-legacy", "import-team",
                "launch-project", "dump-textures", "dump-ebx", "dump-legacy", "read-project"
            };

            if (args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                return;
            }

            if (knownCommands.Contains(args[0]))
            {
                // Command-first syntax: <command> <profile> [options...]
                command = args[0].ToLowerInvariant();
                if (args.Length < 2)
                {
                    Console.WriteLine("Error: Missing profile argument.");
                    PrintUsage();
                    Environment.Exit(1);
                    return;
                }
                profileName = args[1];
                cmdArgs = args.Skip(2).ToArray();
            }
            else
            {
                // Profile-first syntax: <profile> <command> [options...]
                profileName = args[0];
                if (args.Length < 2)
                {
                    command = "profile";
                    cmdArgs = new string[0];
                }
                else
                {
                    command = args[1].ToLowerInvariant();
                    cmdArgs = args.Skip(2).ToArray();
                }
            }

            ILogger logger = new ConsoleLogger();

            if (!InitializeFrosty(profileName, logger))
            {
                Environment.Exit(1);
                return;
            }

            try
            {
                switch (command)
                {
                    case "profile":
                        logger.Log("Profile Loaded: " + profileName);
                        break;

                    case "import-legacy":
                    case "inject-legacy":
                        if (cmdArgs.Length == 2 && Directory.Exists(cmdArgs[1]))
                        {
                            // Directory injection mode: <fbproject> <assets_dir>
                            InjectLegacyDirectory(cmdArgs[0], cmdArgs[1], logger);
                        }
                        else if (cmdArgs.Length >= 3)
                        {
                            // Single asset mode: <fbproject> <legacy_path> <payload_file>
                            ImportLegacy(cmdArgs[0], cmdArgs[1], cmdArgs[2], logger);
                        }
                        else
                        {
                            logger.LogError("Usage: FrostyCmd <profile> " + command + " <project_path> <legacy_name> <payload_file>");
                            logger.LogError("       FrostyCmd <profile> inject-legacy <project_path> <assets_dir>");
                            Environment.Exit(1);
                        }
                        break;

                    case "export-legacy":
                        if (cmdArgs.Length >= 3)
                        {
                            // <fbproject> <legacy_name> <output_file>
                            ExportLegacy(cmdArgs[0], cmdArgs[1], cmdArgs[2], logger);
                        }
                        else if (cmdArgs.Length == 2)
                        {
                            // <legacy_name> <output_file> from base game
                            ExportLegacy(null, cmdArgs[0], cmdArgs[1], logger);
                        }
                        else
                        {
                            logger.LogError("Usage: FrostyCmd <profile> export-legacy [<project_path>] <legacy_name> <output_file>");
                            Environment.Exit(1);
                        }
                        break;

                    case "import-team":
                        if (cmdArgs.Length < 4)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> import-team <OldId> <NewId> <AssetsDir> <OutputProject>");
                            Environment.Exit(1);
                            return;
                        }
                        ImportTeam(cmdArgs[0], cmdArgs[1], cmdArgs[2], cmdArgs[3], logger);
                        break;

                    case "launch-project":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> launch-project <project_path>");
                            Environment.Exit(1);
                            return;
                        }
                        LaunchProject(profileName, cmdArgs[0], logger);
                        break;

                    case "list-assets":
                        {
                            string type = cmdArgs.Length > 0 ? cmdArgs[0].ToLowerInvariant() : "ebx";
                            ListAssets(type, logger);
                        }
                        break;

                    case "export-asset":
                        if (cmdArgs.Length < 3)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> export-asset <type> <asset_path_or_guid> <output_file>");
                            Environment.Exit(1);
                            return;
                        }
                        ExportAsset(cmdArgs[0].ToLowerInvariant(), cmdArgs[1], cmdArgs[2], logger);
                        break;

                    case "import-asset":
                        if (cmdArgs.Length < 5)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> import-asset <project_path> <type> <asset_path_or_guid> <input_file> <output_project_path>");
                            Environment.Exit(1);
                            return;
                        }
                        ImportAsset(cmdArgs[0], cmdArgs[1].ToLowerInvariant(), cmdArgs[2], cmdArgs[3], cmdArgs[4], logger);
                        break;

                    case "create-project":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> create-project <project_path>");
                            Environment.Exit(1);
                            return;
                        }
                        CreateProject(cmdArgs[0], logger);
                        break;

                    case "project-info":
                    case "read-project":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> project-info <project_path>");
                            Environment.Exit(1);
                            return;
                        }
                        ProjectInfo(cmdArgs[0], logger);
                        break;

                    case "dump-textures":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> dump-textures <output_file>");
                            Environment.Exit(1);
                            return;
                        }
                        DumpTextures(cmdArgs[0], logger);
                        break;

                    case "dump-ebx":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> dump-ebx <output_file>");
                            Environment.Exit(1);
                            return;
                        }
                        DumpEbx(cmdArgs[0], logger);
                        break;

                    case "dump-legacy":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> dump-legacy <output_file>");
                            Environment.Exit(1);
                            return;
                        }
                        DumpLegacy(cmdArgs[0], logger);
                        break;

                    case "build-mod":
                        if (cmdArgs.Length < 2)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> build-mod <project_path> <output_mod_path> [title] [author] [category] [version]");
                            Environment.Exit(1);
                            return;
                        }
                        BuildMod(cmdArgs, logger);
                        break;

                    case "apply-mods":
                        if (cmdArgs.Length < 2)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> apply-mods <mod_pack_name> <mod1.fbmod> [mod2.fbmod ...]");
                            Environment.Exit(1);
                            return;
                        }
                        ApplyMods(profileName, cmdArgs[0], cmdArgs.Skip(1).ToArray(), logger);
                        break;

                    case "launch":
                        if (cmdArgs.Length < 1)
                        {
                            logger.LogError("Usage: FrostyCmd <profile> launch <mod_pack_name> [args]");
                            Environment.Exit(1);
                            return;
                        }
                        LaunchGame(profileName, cmdArgs[0], cmdArgs.Length > 1 ? string.Join(" ", cmdArgs.Skip(1)) : "", logger);
                        break;

                    default:
                        logger.LogError("Unknown command: " + command);
                        PrintUsage();
                        Environment.Exit(1);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Unhandled exception in command " + command + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("FrostyCmd / FrostyCLI - Command Line Interface for Frosty Toolsuite");
            Console.WriteLine("Usage: FrostyCmd.exe <profile> <command> [options...]");
            Console.WriteLine("       FrostyCmd.exe <command> <profile> [options...]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  import-legacy <project> <legacy_path> <file>     Inject legacy asset (DB, XML, DDS) into project");
            Console.WriteLine("  inject-legacy <project> <legacy_path> <file>     Alias for import-legacy");
            Console.WriteLine("  inject-legacy <project> <assets_dir>             Batch inject assets from directory into project");
            Console.WriteLine("  export-legacy [<project>] <legacy_path> <file>   Export legacy asset to file");
            Console.WriteLine("  import-team <oldId> <newId> <assetsDir> <project> Batch inject team kits/crests DDS into project");
            Console.WriteLine("  launch-project <project>                         Build mod from project and launch game headless");
            Console.WriteLine("  list-assets [type]                               List assets in game (ebx, res, chunk)");
            Console.WriteLine("  export-asset <type> <ident> <file>               Export EBX, RES, or Chunk asset");
            Console.WriteLine("  import-asset <project> <type> <ident> <in> <out> Import raw asset into project");
            Console.WriteLine("  create-project <project>                         Create new empty project");
            Console.WriteLine("  project-info <project>                           Print modified assets in project");
            Console.WriteLine("  dump-textures <file>                             Dump all texture names");
            Console.WriteLine("  dump-ebx <file>                                  Dump all EBX names and types");
            Console.WriteLine("  dump-legacy <file>                               Dump all legacy asset names");
            Console.WriteLine("  build-mod <project> <mod> [meta...]              Compile project into fbmod");
            Console.WriteLine("  apply-mods <pack_name> <mod1> [mod2...]          Apply mods to game ModData folder");
            Console.WriteLine("  launch <pack_name> [args]                        Launch game with mod pack");
        }

        private static bool InitializeFrosty(string profileName, ILogger logger)
        {
            Frosty.Core.App.Logger = logger;
            Frosty.Core.App.PluginManager = new PluginManager(logger, PluginManagerType.Editor);
            
            try
            {
                string configDir = App.GlobalSettingsPath;
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                string editorConfigFile = Path.Combine(configDir, "editor_config.json");
                if (!File.Exists(editorConfigFile))
                {
                    string realAppdataConfig = @"C:\Users\remus\AppData\Local\Frosty\editor_config.json";
                    if (File.Exists(realAppdataConfig))
                    {
                        File.Copy(realAppdataConfig, editorConfigFile, true);
                    }
                    else
                    {
                        File.WriteAllText(editorConfigFile, "{}");
                    }
                }
            }
            catch { }

            Config.Load("");
            ProfilesLibrary.Initialize(Frosty.Core.App.PluginManager.Profiles);
            TypeLibrary.Initialize();

            if (!ProfilesLibrary.Initialize(profileName))
            {
                logger.LogError("Failed to load profile: " + profileName);
                return false;
            }

            TypeLibrary.Initialize(true);

            string gamePath = Environment.GetEnvironmentVariable("FIFA17_DIR");
            if (string.IsNullOrEmpty(gamePath))
            {
                try { gamePath = Config.Get<string>("GamePath", null, ConfigScope.Game, profileName); } catch { }
            }
            if (string.IsNullOrEmpty(gamePath))
            {
                logger.LogError("GamePath is not configured for profile " + profileName + ". Set FIFA17_DIR environment variable.");
                return false;
            }

            App.FileSystem = new FileSystem(gamePath);
            if (ProfilesLibrary.Sources != null)
            {
                foreach (FileSystemSource source in ProfilesLibrary.Sources)
                    App.FileSystem.AddSource(source.Path, source.SubDirs);
            }

            byte[] key = null;
            if (ProfilesLibrary.RequiresKey)
            {
                if (File.Exists(ProfilesLibrary.CacheName + ".key"))
                    key = NativeReader.ReadInStream(new FileStream(ProfilesLibrary.CacheName + ".key", FileMode.Open, FileAccess.Read));
                else
                    logger.LogWarning("Profile requires a key, but no .key file found in cache.");
            }

            App.FileSystem.Initialize(key);
            App.ResourceManager = new ResourceManager(App.FileSystem);
            App.ResourceManager.SetLogger(logger);
            App.ResourceManager.Initialize();
            App.AssetManager = new AssetManager(App.FileSystem, App.ResourceManager);

            App.PluginManager.Initialize();
            App.AssetManager.RegisterCustomAssetManager("legacy", typeof(Frosty.Core.Legacy.LegacyFileManager));
            App.AssetManager.SetLogger(logger);
            App.AssetManager.Initialize(true, new AssetManagerImportResult());

            logger.Log("Frosty SDK Initialized Successfully for " + profileName);
            return true;
        }

        private static void ImportLegacy(string projFile, string legacyName, string payloadFile, ILogger logger)
        {
            if (!File.Exists(projFile))
            {
                logger.LogError("Project not found: " + projFile);
                Environment.Exit(1);
                return;
            }
            if (!File.Exists(payloadFile))
            {
                logger.LogError("Payload file not found: " + payloadFile);
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            logger.Log("Loading existing project " + projFile + "...");
            project.Load(projFile);

            byte[] data = File.ReadAllBytes(payloadFile);
            App.AssetManager.ModifyCustomAsset("legacy", legacyName, data);

            logger.Log("Injected " + legacyName + " into project. Saving...");
            project.Save(projFile);
            logger.Log("Project saved successfully.");
        }

        private static void InjectLegacyDirectory(string projFile, string assetsDir, ILogger logger)
        {
            if (!File.Exists(projFile))
            {
                logger.LogError("Project not found: " + projFile);
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            logger.Log("Loading existing project " + projFile + "...");
            project.Load(projFile);

            int injected = 0;
            string[] files = Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string relPath = file.Substring(assetsDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                AssetEntry entry = App.AssetManager.GetCustomAssetEntry("legacy", relPath);
                if (entry != null)
                {
                    byte[] data = File.ReadAllBytes(file);
                    App.AssetManager.ModifyCustomAsset("legacy", relPath, data);
                    logger.Log("Injected: " + relPath);
                    injected++;
                }
            }

            if (injected > 0)
            {
                logger.Log("Injected " + injected + " legacy assets. Saving project...");
                project.Save(projFile);
                logger.Log("Project saved successfully.");
            }
            else
            {
                logger.LogWarning("No matching legacy assets found in " + assetsDir);
            }
        }

        private static void ExportLegacy(string projFile, string legacyName, string outFile, ILogger logger)
        {
            if (!string.IsNullOrEmpty(projFile) && File.Exists(projFile))
            {
                FrostyProject project = new FrostyProject();
                logger.Log("Loading existing project " + projFile + "...");
                project.Load(projFile);
            }

            AssetEntry entry = App.AssetManager.GetCustomAssetEntry("legacy", legacyName);
            if (entry == null)
            {
                logger.LogError("Legacy asset not found: " + legacyName);
                Environment.Exit(1);
                return;
            }

            string outDir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using (NativeWriter writer = new NativeWriter(new FileStream(outFile, FileMode.Create)))
            {
                writer.Write(NativeReader.ReadInStream(App.AssetManager.GetCustomAsset("legacy", entry)));
            }
            logger.Log("Exported " + legacyName + " to " + outFile);
        }

        private static void ImportTeam(string oldId, string newId, string assetsDir, string outProject, ILogger logger)
        {
            if (!Directory.Exists(assetsDir))
            {
                logger.LogError("Assets directory not found: " + assetsDir);
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            if (File.Exists(outProject))
            {
                logger.Log("Loading existing project " + outProject + "...");
                project.Load(outProject);
            }
            int modifiedCount = 0;

            foreach (string file in Directory.GetFiles(assetsDir, "*.dds"))
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string targetName = filename.Replace(oldId, newId);

                ResAssetEntry targetEntry = null;
                foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes((uint)ResourceType.Texture))
                {
                    if (entry.Name.EndsWith(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetEntry = entry;
                        break;
                    }
                }

                if (targetEntry == null)
                {
                    logger.LogWarning("Could not find target resource for " + targetName);
                    continue;
                }

                logger.Log("Injecting " + filename + ".dds into " + targetEntry.Name);

                try
                {
                    using (NativeReader reader = new NativeReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
                    {
                        Texture textureAsset = App.AssetManager.GetResAs<Texture>(targetEntry);
                        var header = new TextureUtils.DDSHeader();
                        if (!header.Read(reader))
                        {
                            logger.LogError("Invalid DDS header in " + file);
                            continue;
                        }

                        string pixelFormat;
                        TextureFlags baseFlags;
                        GetPixelFormat(textureAsset, header, out pixelFormat, out baseFlags);

                        byte[] buffer = new byte[reader.Length - reader.Position];
                        reader.Read(buffer, 0, (int)(reader.Length - reader.Position));

                        ushort depth = 1;
                        if ((header.dwCaps2 & TextureUtils.DDSCaps2.CubeMap) != 0) depth = 6;
                        if ((header.dwCaps2 & TextureUtils.DDSCaps2.Volume) != 0) depth = (ushort)header.dwDepth;

                        Texture newTextureAsset = new Texture(textureAsset.Type, pixelFormat, (ushort)header.dwWidth, (ushort)header.dwHeight, depth) { FirstMip = textureAsset.FirstMip };
                        if (header.dwMipMapCount <= textureAsset.FirstMip)
                            newTextureAsset.FirstMip = 0;

                        newTextureAsset.TextureGroup = textureAsset.TextureGroup;
                        newTextureAsset.CalculateMipData((byte)header.dwMipMapCount, TextureUtils.GetFormatBlockSize(pixelFormat), TextureUtils.IsCompressedFormat(pixelFormat), (uint)buffer.Length);
                        newTextureAsset.Flags = baseFlags;

                        TextureFlags oldFlags = textureAsset.Flags & ~(TextureFlags.SrgbGamma);
                        newTextureAsset.Flags |= oldFlags;

                        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(textureAsset.ChunkId);

                        if (ProfilesLibrary.MustAddChunks && chunkEntry.Bundles.Count == 0 && !chunkEntry.IsAdded)
                        {
                            textureAsset.ChunkId = App.AssetManager.AddChunk(buffer, null, (newTextureAsset.Flags & TextureFlags.OnDemandLoaded) != 0 ? null : newTextureAsset);
                            chunkEntry = App.AssetManager.GetChunkEntry(textureAsset.ChunkId);
                        }
                        else
                        {
                            App.AssetManager.ModifyChunk(textureAsset.ChunkId, buffer, ((newTextureAsset.Flags & TextureFlags.OnDemandLoaded) != 0 || newTextureAsset.Type != TextureType.TT_2d) ? null : newTextureAsset);
                        }

                        for (int i = 0; i < 4; i++)
                            newTextureAsset.Unknown3[i] = textureAsset.Unknown3[i];
                        newTextureAsset.SetData(textureAsset.ChunkId, App.AssetManager);
                        newTextureAsset.AssetNameHash = (uint)Frosty.Hash.Fnv1.HashString(targetEntry.Name);

                        App.AssetManager.ModifyRes(targetEntry.ResRid, newTextureAsset);
                        targetEntry.LinkAsset(chunkEntry);

                        modifiedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to inject " + file + ": " + ex.Message);
                }
            }

            if (modifiedCount > 0)
            {
                logger.Log("Saving project to " + outProject + "...");
                project.Save(outProject);
                logger.Log("Project saved successfully.");
            }
            else
            {
                logger.Log("No textures were modified. Project not saved.");
            }
        }

        private static void GetPixelFormat(Texture textureAsset, TextureUtils.DDSHeader header, out string pixelFormat, out TextureFlags flags)
        {
            pixelFormat = "Unknown";
            flags = 0;
            if (header.ddspf.dwFourCC == 0x35545844)
            {
                pixelFormat = "BC3_UNORM";
            }
            else if (header.ddspf.dwFourCC == 0x31545844)
            {
                pixelFormat = "BC1_UNORM";
            }
            else if (header.HasExtendedHeader && header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC3_UNorm)
            {
                pixelFormat = "BC3_UNORM";
            }
            else if (header.HasExtendedHeader && header.ExtendedHeader.dxgiFormat == SharpDX.DXGI.Format.BC1_UNorm)
            {
                pixelFormat = "BC1_UNORM";
            }
            else
            {
                pixelFormat = "BC3_UNORM";
            }
        }

        private static void LaunchProject(string profileName, string projFile, ILogger logger)
        {
            if (!File.Exists(projFile))
            {
                logger.LogError("Project not found: " + projFile);
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            logger.Log("Loading existing project " + projFile + "...");
            project.Load(projFile);

            logger.Log("Exporting project to mod...");
            ModSettings editorSettings = new ModSettings { Title = "FrostyCmdMod", Author = "FrostyCmd", Version = "1.0", Category = "CLI" };
            string fbmodPath = "Mods/" + profileName + "/FrostyCmdMod.fbmod";
            Directory.CreateDirectory("Mods/" + profileName);
            if (File.Exists(fbmodPath)) File.Delete(fbmodPath);

            project.WriteToMod(fbmodPath, editorSettings, false, CancellationToken.None);

            logger.Log("Applying mod and launching game...");

            string gamePatchPath = "Update/Patch/Data";
            string modsJsonPath = Path.Combine(App.FileSystem.BasePath, "ModData", "Editor", gamePatchPath, "mods.json");
            if (File.Exists(modsJsonPath)) File.Delete(modsJsonPath);

            FrostyModExecutor executor = new FrostyModExecutor();
            List<string> modPaths = new List<string> { "FrostyCmdMod.fbmod" };

            executor.Run(App.FileSystem, CancellationToken.None, logger, "Mods/" + profileName + "/", "Editor", "", modPaths.ToArray());
            logger.Log("Game launched successfully!");
        }

        private static void ListAssets(string type, ILogger logger)
        {
            switch (type)
            {
                case "ebx":
                    foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        Console.WriteLine($"[EBX] {entry.Name} ({entry.Type})");
                    break;
                case "res":
                    foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes())
                        Console.WriteLine($"[RES] {entry.Name} (Type: {entry.ResType})");
                    break;
                case "chunk":
                    foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks())
                        Console.WriteLine($"[CHUNK] {entry.Id}");
                    break;
                case "legacy":
                    foreach (AssetEntry entry in App.AssetManager.EnumerateCustomAssets("legacy"))
                        Console.WriteLine($"[LEGACY] {entry.Name}");
                    break;
                default:
                    logger.LogError("Unknown asset type: " + type + ". Must be ebx, res, chunk, or legacy.");
                    break;
            }
        }

        private static void ExportAsset(string type, string assetIdent, string outputFile, ILogger logger)
        {
            string outDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            using (NativeWriter writer = new NativeWriter(new FileStream(outputFile, FileMode.Create)))
            {
                switch (type)
                {
                    case "ebx":
                        EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(assetIdent);
                        if (ebxEntry == null)
                        {
                            logger.LogError($"EBX asset not found: {assetIdent}");
                            Environment.Exit(1);
                            return;
                        }
                        writer.Write(NativeReader.ReadInStream(App.AssetManager.GetEbxStream(ebxEntry)));
                        break;
                    case "res":
                        ResAssetEntry resEntry = App.AssetManager.GetResEntry(assetIdent);
                        if (resEntry == null)
                        {
                            logger.LogError($"RES asset not found: {assetIdent}");
                            Environment.Exit(1);
                            return;
                        }
                        writer.Write(NativeReader.ReadInStream(App.AssetManager.GetRes(resEntry)));
                        break;
                    case "chunk":
                        if (!Guid.TryParse(assetIdent, out Guid chunkGuid))
                        {
                            logger.LogError($"Invalid Chunk GUID: {assetIdent}");
                            Environment.Exit(1);
                            return;
                        }
                        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(chunkGuid);
                        if (chunkEntry == null)
                        {
                            logger.LogError($"Chunk not found: {assetIdent}");
                            Environment.Exit(1);
                            return;
                        }
                        writer.Write(NativeReader.ReadInStream(App.AssetManager.GetChunk(chunkEntry)));
                        break;
                    case "legacy":
                        ExportLegacy(null, assetIdent, outputFile, logger);
                        return;
                    default:
                        logger.LogError($"Unknown asset type: {type}");
                        Environment.Exit(1);
                        return;
                }
            }
            logger.Log($"Successfully exported {type} asset '{assetIdent}' to '{outputFile}'.");
        }

        private static void ImportAsset(string projectPath, string type, string assetIdent, string inputFile, string outputProjectPath, ILogger logger)
        {
            if (!File.Exists(inputFile))
            {
                logger.LogError($"Input file not found: {inputFile}");
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            if (File.Exists(projectPath))
            {
                project.Load(projectPath);
            }

            byte[] data = File.ReadAllBytes(inputFile);

            switch (type)
            {
                case "ebx":
                    EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(assetIdent);
                    if (ebxEntry == null)
                    {
                        logger.LogError($"EBX asset not found: {assetIdent}");
                        Environment.Exit(1);
                        return;
                    }
                    using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(data), App.FileSystem))
                    {
                        EbxAsset asset = reader.ReadAsset<EbxAsset>();
                        App.AssetManager.ModifyEbx(assetIdent, asset);
                    }
                    break;
                case "chunk":
                    if (!Guid.TryParse(assetIdent, out Guid chunkGuid))
                    {
                        logger.LogError($"Invalid Chunk GUID: {assetIdent}");
                        Environment.Exit(1);
                        return;
                    }
                    App.AssetManager.ModifyChunk(chunkGuid, data);
                    break;
                case "legacy":
                    App.AssetManager.ModifyCustomAsset("legacy", assetIdent, data);
                    break;
                default:
                    logger.LogError($"Import for type '{type}' is not supported directly via raw file.");
                    Environment.Exit(1);
                    return;
            }

            project.Save(outputProjectPath);
            logger.Log($"Successfully imported {type} asset '{assetIdent}' and saved project to '{outputProjectPath}'.");
        }

        private static void CreateProject(string projectPath, ILogger logger)
        {
            string dir = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FrostyProject project = new FrostyProject();
            project.Save(projectPath);
            logger.Log($"Successfully created project at '{projectPath}'.");
        }

        private static void ProjectInfo(string projectPath, ILogger logger)
        {
            if (!File.Exists(projectPath))
            {
                logger.LogError($"Project file not found: {projectPath}");
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            project.Load(projectPath);

            logger.Log($"=== Project Info: {project.Filename} ===");
            logger.Log($"Title:       {project.ModSettings.Title}");
            logger.Log($"Author:      {project.ModSettings.Author}");
            logger.Log($"Category:    {project.ModSettings.Category}");
            logger.Log($"Version:     {project.ModSettings.Version}");
            logger.Log($"Description: {project.ModSettings.Description}");
            logger.Log("");

            logger.Log("Modified EBX:");
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
            {
                if (entry.HasModifiedData)
                    logger.Log("  " + entry.Name);
            }
            logger.Log("Modified RES:");
            foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes())
            {
                if (entry.HasModifiedData)
                    logger.Log("  " + entry.Name);
            }
            logger.Log("Modified CHUNKS:");
            foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks())
            {
                if (entry.HasModifiedData)
                    logger.Log("  " + entry.Id.ToString());
            }
            logger.Log("Modified LEGACY:");
            foreach (Frosty.Core.Legacy.LegacyFileEntry entry in App.AssetManager.EnumerateCustomAssets("legacy"))
            {
                if (entry.IsModified)
                    logger.Log("  " + entry.Name);
            }
        }

        private static void DumpTextures(string outFile, ILogger logger)
        {
            using (StreamWriter sw = new StreamWriter(outFile))
            {
                foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes((uint)ResourceType.Texture))
                {
                    sw.WriteLine(entry.Name);
                }
            }
            logger.Log("Dumped all texture names to " + outFile);
        }

        private static void DumpEbx(string outFile, ILogger logger)
        {
            using (StreamWriter sw = new StreamWriter(outFile))
            {
                foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                {
                    sw.WriteLine(entry.Name + " | " + entry.Type);
                }
            }
            logger.Log("Dumped all ebx names to " + outFile);
        }

        private static void DumpLegacy(string outFile, ILogger logger)
        {
            using (StreamWriter sw = new StreamWriter(outFile))
            {
                foreach (AssetEntry entry in App.AssetManager.EnumerateCustomAssets("legacy"))
                {
                    sw.WriteLine(entry.Name);
                }
            }
            logger.Log("Dumped all legacy names to " + outFile);
        }

        private static void BuildMod(string[] args, ILogger logger)
        {
            string projectPath = args[0];
            string outputModPath = args[1];

            if (!File.Exists(projectPath))
            {
                logger.LogError($"Project file not found: {projectPath}");
                Environment.Exit(1);
                return;
            }

            FrostyProject project = new FrostyProject();
            project.Load(projectPath);

            ModSettings settings = new ModSettings();
            settings.Title = args.Length > 2 ? args[2] : (string.IsNullOrEmpty(project.ModSettings.Title) ? Path.GetFileNameWithoutExtension(outputModPath) : project.ModSettings.Title);
            settings.Author = args.Length > 3 ? args[3] : (string.IsNullOrEmpty(project.ModSettings.Author) ? "FrostyCLI" : project.ModSettings.Author);
            settings.Category = args.Length > 4 ? args[4] : (string.IsNullOrEmpty(project.ModSettings.Category) ? "Gameplay" : project.ModSettings.Category);
            settings.Version = args.Length > 5 ? args[5] : (string.IsNullOrEmpty(project.ModSettings.Version) ? "1.0" : project.ModSettings.Version);
            settings.Description = project.ModSettings.Description ?? "";

            string outDir = Path.GetDirectoryName(outputModPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            logger.Log($"Building mod package: '{outputModPath}'...");
            project.WriteToMod(outputModPath, settings, false, CancellationToken.None);
            logger.Log("Mod built successfully.");
        }

        private static void ApplyMods(string profileName, string modPackName, string[] modPaths, ILogger logger)
        {
            List<string> fullModPaths = new List<string>();
            foreach (string m in modPaths)
            {
                if (!File.Exists(m))
                {
                    logger.LogError($"Mod file not found: {m}");
                    Environment.Exit(1);
                    return;
                }
                fullModPaths.Add(Path.GetFullPath(m));
            }

            logger.Log($"Applying {fullModPaths.Count} mods for configuration '{modPackName}'...");
            FrostyModExecutor executor = new FrostyModExecutor();
            executor.Run(App.FileSystem, CancellationToken.None, logger, Path.GetDirectoryName(fullModPaths[0]), modPackName, "", fullModPaths.Select(Path.GetFileName).ToArray());
            logger.Log("Mods successfully applied!");
        }

        private static void LaunchGame(string profileName, string modPackName, string launchArgs, ILogger logger)
        {
            logger.Log($"Launching game profile '{profileName}' with pack '{modPackName}'...");
            FrostyModExecutor executor = new FrostyModExecutor();
            executor.Run(App.FileSystem, CancellationToken.None, logger, "", modPackName, launchArgs, new string[0]);
            logger.Log("Launch initiated.");
        }
    }
}
