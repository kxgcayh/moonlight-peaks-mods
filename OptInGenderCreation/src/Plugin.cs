using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace OptInGenderCreation
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin? Instance { get; private set; }
        public static ManualLogSource? Log { get; private set; }

        public static ConfigEntry<GenderMode> DefaultGenderMode { get; private set; } = null!;
        public static ConfigEntry<bool> IncludeUnisex { get; private set; } = null!;

        private Harmony? harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            DefaultGenderMode = Config.Bind(
                "General",
                "DefaultGenderMode",
                GenderMode.Default,
                "Initial gender mode when opening Character Creation (Default, Male, Female)."
            );

            IncludeUnisex = Config.Bind(
                "Filtering",
                "IncludeUnisex",
                true,
                "Whether neutral/unisex items (t-shirts, hoodies, basic pants, sneakers, and unisex hairstyles) appear in both Male and Female modes."
            );

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded successfully.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.moonlightpeaks.mod.optingendercreation";
        public const string PLUGIN_NAME = "Opt-In Gender Creation";
        public const string PLUGIN_VERSION = "1.0.0";
    }
}
