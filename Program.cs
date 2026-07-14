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
            Console.WriteLine("FrostyCLI starting...");
            ILogger logger = new ConsoleLogger();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Config.Load("");

            TypeLibrary.Initialize(true);
            PluginManager pluginManager = new PluginManager(logger, PluginManagerType.Editor);
            ProfilesLibrary.Initialize(pluginManager.Profiles);

            string profileName = args.Length > 0 ? args[0] : "FIFA 17";
            
            if (string.IsNullOrEmpty(profileName))
            {
                logger.LogError("No profile specified and no DefaultProfile found in config.");
                Console.WriteLine("Usage: FrostyCli.exe [ProfileName]");
                return;
            }

            if (!ProfilesLibrary.Initialize(profileName))
            {
                logger.LogError($"There was an error when trying to load game using specified profile: {profileName}");
                return;
            }

            logger.Log($"Loading Profile For {ProfilesLibrary.DisplayName}");

            string gamePath = Config.Get<string>("GamePath", null, ConfigScope.Game, null);
            if (string.IsNullOrEmpty(gamePath))
            {
                logger.LogError("GamePath is not configured for this profile.");
                return;
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

            logger.Log("Frosty SDK Initialized Successfully.");
            logger.Log($"Total EBX loaded: {assetManager.EnumerateEbx().Count()}");

            Console.WriteLine("Done.");
        }
    }
}
