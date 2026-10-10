using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ToNSaveManager.Localization;
using ToNSaveManager.Models;

namespace ToNSaveManager
{
    internal static partial class AppCore
    {
        internal const string ProgramName = "ToNSaveManager";
        internal const string VRChatAppID = "438100";
        
        internal static readonly string ProgramDirectory = AppContext.BaseDirectory ?? string.Empty;
        
        // Windows: %AppData%\ToNSaveManager | Linux: ~/.config/ToNSaveManager
        internal static readonly string DataLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProgramName);
        // Legacy path is mostly for Windows users, or for linux users that managed to get it to run via proton
        internal static string LegacyDataLocation => Path.Combine(GetVRChatDataLocation(), ProgramName);

        /// <summary>
        /// True once the frontend finished starting up and the log watcher is running.
        /// Integrations (OSC, WebSocket, RGB, ...) stay quiet until then.
        /// </summary>
        internal static bool Started;

        /// <summary>
        /// Raised when Core failed to write one of its files (settings, saves, stats).
        /// The frontend decides how to tell the user.
        /// </summary>
        internal static event Action<string, Exception>? WriteFailed;

        internal static void ReportWriteFailed(string path, Exception e)
        {
            Logger.Error($"Failed to write '{path}': {e}");
            WriteFailed?.Invoke(path, e);
        }

        /// <summary>
        /// Opens a resource embedded in Core (files under Resources/).
        /// </summary>
        internal static Stream? GetEmbeddedResource(string name)
        {
            return Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"ToNSaveManager.Resources.{name}");
        }

        /// <summary>
        /// Initializes Core. Every frontend calls this first, before using any other Core API.
        /// </summary>
        internal static void Initialize(string[] args)
        {
            Args.SetArguments(args);

            Directory.CreateDirectory(DataLocation);
            if (Args.ContainsArg("--console")) Logger.AllowConsole();
            Logger.Log("Initializing Core.");

            Directory.SetCurrentDirectory(ProgramDirectory);
            Logger.Log("Program Directory: " + ProgramDirectory);
            
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            LANG.Initialize();
            LANG.LanguageChanged += Entry.LocalizeContent;
            Entry.LocalizeContent();
        }

        #region Find VRChat Location
        
        private static string? m_VRChatDataLocation;
        internal static string GetVRChatDataLocation()
        {
            if (m_VRChatDataLocation != null) return m_VRChatDataLocation;
            
            // Allow user to override location of VRChat
            string? custom = Args.GetArgValue("--vrchat-data") ?? Environment.GetEnvironmentVariable("TON_VRCHAT_DATA");
            if (!string.IsNullOrWhiteSpace(custom)) return m_VRChatDataLocation = custom;

            if (OperatingSystem.IsWindows())
            {
                return m_VRChatDataLocation =
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"Low\VRChat";
            }

            return m_VRChatDataLocation = FindProtonVRChatDataLocation();
        }
        
        // generate regex at compile time
        [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
        private static partial Regex LibraryPathPattern();
        
        private static string FindProtonVRChatDataLocation()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string vrchatInProtonDrive = Path.Combine("steamapps", "compatdata", VRChatAppID, "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "VRChat");

            List<string> libraries = new List<string>();
            // all known steam paths to check for library location
            string[] steamRoots =
            [
                Path.Combine(home, ".steam", "steam"), // old steam installation path
                Path.Combine(home, ".local", "share", "Steam"), // Native install (most distros)
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam") // Flatpak installs
            ];

            foreach (string root in steamRoots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                AddLibraryLocation(libraries, root);
                
                // check for additional steam libraries
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;

                try
                {
                    foreach (Match match in LibraryPathPattern().Matches(File.ReadAllText(vdf)))
                    {
                        AddLibraryLocation(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
                    }
                }
                catch (Exception e)
                {
                    Logger.Warning("Could not read Steam library folders: " + e.Message);
                }
            }

            foreach (string library in libraries)
            {
                string path = Path.Combine(library, vrchatInProtonDrive);
                if (Directory.Exists(path)) return path;
            }
            
            // Fallback default location
            return Path.Combine(libraries.Count > 0 ? libraries[0] : Path.Combine(home, ".local", "share", "Steam"), vrchatInProtonDrive);
        }

        private static void AddLibraryLocation(List<string> libraries, string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                
                // Resolve symlinks to prevent duplicates
                FileSystemInfo? target = new  DirectoryInfo(full).ResolveLinkTarget(true);
                if (target != null) full = target.FullName;
                if (!libraries.Contains(full)) libraries.Add(full);
            }
            catch (Exception e)
            {
                Logger.Warning("Failed to add Library - " + e.Message);
            }
        }
        
        #endregion

        internal static bool CreateFileBackup(string filePath)
        {
            Logger.Info("Creating Backup For: " + filePath);

            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    File.Copy(filePath, filePath + ".backup_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
