using System;
using System.IO;
using BepInEx;
using HarmonyLib;

namespace MoonlightPeaks.SaveInfo
{
    [BepInPlugin("com.moonlightpeaks.saveinfo", "Moonlight Peaks Save Info", "1.0.0")]
    public sealed class SaveInfoPlugin : BaseUnityPlugin
    {
        private const string Marker = "# Moonlight Peaks Save Info";
        private Harmony? harmony;
        internal static SaveInfoPlugin Instance { get; private set; } = null!;

        private void Awake()
        {
            Instance = this;
            harmony = new Harmony("com.moonlightpeaks.saveinfo");
            harmony.PatchAll();
            Logger.LogInfo("Save Info loaded.");
        }

        private void OnDestroy() => harmony?.UnpatchSelf();

        internal static void WriteInfo(GameMetaPersistence? meta)
        {
            if (meta == null || meta.Guid.IsEmpty || FileSystemService.Instance == null) return;
            try
            {
                var directory = FileSystemService.Instance.GetGamePersistenceDirectoryPath(meta.Guid);
                if (!Directory.Exists(directory)) return;
                var path = Path.Combine(directory, "save_info.txt");
                if (File.Exists(path) && !File.ReadAllText(path).StartsWith(Marker + "\n", StringComparison.Ordinal))
                {
                    Instance.Logger.LogWarning($"Preserving existing unmanaged file: {path}");
                    return;
                }
                string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value!.Replace('\r', ' ').Replace('\n', ' ').Trim();
                var text = $"{Marker}\nCharacter: {Clean(meta.PlayerName)}\nCreated: {Clean(meta.TimeCreated)}\nLast played: {Clean(meta.TimeLastPlayed)}\nSave ID: {meta.Guid}\n";
                if (!File.Exists(path) || File.ReadAllText(path) != text) File.WriteAllText(path, text);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogWarning($"Could not update save info: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(FileSystemPersistenceService), "LoadGameMetaPersistence")]
        private static class LoadPatch
        {
            private static void Postfix(GameMetaPersistence __result) => WriteInfo(__result);
        }

        [HarmonyPatch(typeof(FileSystemPersistenceService), "SaveGamePersistenceAsync")]
        private static class SavePatch
        {
            private static void Postfix(GameMetaPersistence __1) => WriteInfo(__1);
        }
    }
}
