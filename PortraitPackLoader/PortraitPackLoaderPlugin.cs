using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoonlightPeaks.PortraitPackLoader
{
    /// <summary>
    /// Loads portrait packs in the Serena's Portrait Replacer layout (Portraits/&lt;Character&gt;/&lt;Key&gt;.png)
    /// and swaps them in when the game loads the matching addressable sprite.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PortraitPackLoaderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.moonlightpeaks.portraitpackloader";
        public const string PluginName = "Moonlight Peaks Portrait Pack Loader";
        public const string PluginVersion = "0.2.0";

        private static readonly string DefaultFolder = Path.Combine("PortraitPacks", "Portraits");
        // Where packs made for Serena's Portrait Replacer tell users to extract.
        private static readonly string LegacyFolder = Path.Combine("SerenaPortraitReplacer", "Portraits");

        internal static new ManualLogSource Logger { get; private set; } = null!;

        public static ConfigEntry<string> PortraitsFolder { get; private set; } = null!;
        public static ConfigEntry<bool> WriteDetectedKeys { get; private set; } = null!;

        private Harmony? _harmony;

        private void Awake()
        {
            Logger = base.Logger;

            PortraitsFolder = Config.Bind(
                "General", "PortraitsFolder",
                DefaultFolder,
                "Folder with portrait PNGs (<Character>/<Key>.png), relative to BepInEx/plugins (or absolute). " +
                "Packs extracted to the legacy SerenaPortraitReplacer/Portraits folder are also loaded.");

            WriteDetectedKeys = Config.Bind(
                "Debug", "WriteDetectedKeys", true,
                "Write DetectedPortraitKeys.txt next to the Portraits folder listing every key the game exposes " +
                "and which pack files matched.");

            // Move configs still on the old default to the new one; the legacy folder keeps loading below.
            if (SamePath(PortraitsFolder.Value, LegacyFolder)) PortraitsFolder.Value = DefaultFolder;

            var folder = Resolve(PortraitsFolder.Value);
            var legacy = Resolve(LegacyFolder);
            PortraitRegistry.Init(folder, legacy);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. {PortraitRegistry.FileCount} PNG(s) in {folder}" +
                           (Directory.Exists(legacy) ? $" and legacy {legacy}" : ""));
        }

        private static string Resolve(string folder) =>
            Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(Paths.PluginPath, folder));

        private static bool SamePath(string a, string b) =>
            string.Equals(a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'),
                System.StringComparison.OrdinalIgnoreCase);

        private void Update() => PortraitRegistry.Tick();

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
