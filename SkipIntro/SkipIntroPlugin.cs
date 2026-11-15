using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoonlightPeaks.SkipIntro
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SkipIntroPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.moonlightpeaks.skipintro";
        public const string PluginName = "Moonlight Peaks Skip Intro";
        public const string PluginVersion = "1.1.1";

        public static SkipIntroPlugin Instance { get; private set; } = null!;
        internal static new ManualLogSource Logger { get; private set; } = null!;

        public static ConfigEntry<bool> EnableHoldFToSkip { get; private set; } = null!;

        public static ConfigEntry<float> HoldDurationSeconds { get; private set; } = null!;

        private Harmony? _harmony;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            EnableHoldFToSkip = Config.Bind(
                "General",
                "EnableHoldFToSkip",
                true,
                "Enable holding F to skip the new game intro."
            );

            HoldDurationSeconds = Config.Bind(
                "General", "HoldDurationSeconds", 0.6f,
                "Seconds to hold F to skip the intro (vanilla hold buttons use 0.6 seconds)."
            );

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully.");
        }

        private void Update()
        {
            Patches.IntroSpeechPatch.UpdatePreTrainPrompt();
        }

        private void OnDestroy()
        {
            Patches.IntroSpeechPatch.Reset();
            _harmony?.UnpatchSelf();
        }
    }
}
