using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoonlightPeaks.PortraitPackLoader
{
    /// <summary>
    /// Loads portrait packs made for Serena's Portrait Replacer (Portraits/&lt;Character&gt;/&lt;Key&gt;.png)
    /// and swaps them in when the game loads the matching addressable sprite.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PortraitPackLoaderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.moonlightpeaks.portraitpackloader";
        public const string PluginName = "Moonlight Peaks Portrait Pack Loader";
        public const string PluginVersion = "0.1.0";

        internal static new ManualLogSource Logger { get; private set; } = null!;

        public static ConfigEntry<string> PortraitsFolder { get; private set; } = null!;
        public static ConfigEntry<bool> WriteDetectedKeys { get; private set; } = null!;

        private Harmony? _harmony;

        private void Awake()
        {
            Logger = base.Logger;

            PortraitsFolder = Config.Bind(
                "General", "PortraitsFolder",
                Path.Combine("SerenaPortraitReplacer", "Portraits"),
                "Folder with portrait PNGs, relative to BepInEx/plugins (or absolute). " +
                "Uses the Serena's Portrait Replacer layout, so existing packs install unchanged.");

            WriteDetectedKeys = Config.Bind(
                "Debug", "WriteDetectedKeys", true,
                "Write DetectedPortraitKeys.txt next to the Portraits folder listing every key the game exposes " +
                "and which pack files matched.");

            var folder = PortraitsFolder.Value;
            if (!Path.IsPathRooted(folder)) folder = Path.Combine(Paths.PluginPath, folder);
            PortraitRegistry.Init(folder);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. {PortraitRegistry.FileCount} PNG(s) in {folder}");
        }

        private void Update() => PortraitRegistry.Tick();

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
