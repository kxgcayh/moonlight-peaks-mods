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

        private static MethodBase TargetMethod()
        {
            var closure = AccessTools.Inner(typeof(ImageForAddressable), "<>c__DisplayClass5_0")
                ?? throw new InvalidOperationException("ImageForAddressable load closure not found");
            _spriteReference = AccessTools.Field(closure, "spriteReference");
            return AccessTools.Method(closure, "<LoadSpriteReference>b__0");
        }

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
