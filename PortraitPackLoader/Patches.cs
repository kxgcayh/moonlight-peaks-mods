using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace MoonlightPeaks.PortraitPackLoader
{
    /// <summary>
    /// ImageForAddressable.LoadSpriteReference completes through this closure. Before it assigns the loaded
    /// sprite, record a swap for it when the reference maps to a pack file.
    /// </summary>
    [HarmonyPatch]
    internal static class LoadSpriteCompletedPatch
    {
        private static FieldInfo? _spriteReference;
        private static MethodBase? _target;

        // The closure and method names are compiler-generated and change when the game is rebuilt.
        // Harmony skips this patch when Prepare returns false, so a game update disables portrait
        // swapping instead of failing the whole plugin.
        private static bool Prepare()
        {
            var closure = AccessTools.Inner(typeof(ImageForAddressable), "<>c__DisplayClass5_0");
            _spriteReference = closure == null ? null : AccessTools.Field(closure, "spriteReference");
            _target = closure == null ? null : AccessTools.Method(closure, "<LoadSpriteReference>b__0");
            if (_target != null && _spriteReference != null) return true;

            PortraitPackLoaderPlugin.Logger.LogError(
                "ImageForAddressable sprite-load closure not found; portraits will not be replaced. " +
                "This game version is not supported yet - please report it with your game version.");
            return false;
        }

        private static MethodBase TargetMethod() => _target!;

        private static void Prefix(object __instance, AsyncOperationHandle<Sprite> result)
        {
            try
            {
                if (result.Status != AsyncOperationStatus.Succeeded) return;
                var reference = _spriteReference?.GetValue(__instance) as AssetReference;
                PortraitRegistry.OnSpriteLoaded(reference!, result.Result);
            }
            catch (Exception e)
            {
                PortraitPackLoaderPlugin.Logger.LogDebug($"Sprite swap lookup failed: {e.Message}");
            }
        }
    }

    /// <summary>Swaps recorded original sprites for pack sprites whenever an Image is given one.</summary>
    [HarmonyPatch(typeof(Image), nameof(Image.sprite), MethodType.Setter)]
    internal static class ImageSpriteSetterPatch
    {
        private static void Prefix(ref Sprite value)
        {
            if (value != null && PortraitRegistry.SpriteSwaps.Count > 0 &&
                PortraitRegistry.SpriteSwaps.TryGetValue(value, out var replacement) && replacement != null)
                value = replacement;
        }
    }
}
