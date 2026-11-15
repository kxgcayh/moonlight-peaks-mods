using System;
using System.Collections.Generic;
using Chicken.UI;
using HarmonyLib;
using OptInGenderCreation.UI;
using TMPro;
using UnityEngine;

namespace OptInGenderCreation.Patches
{
    [HarmonyPatch]
    public static class CharacterCustomizationPatches
    {
        private static GenderSelectorWidget? selectorWidget;

        [HarmonyPatch(typeof(MenuCharacterCustomizationScreen), "OnInitialize")]
        [HarmonyPostfix]
        public static void OnInitialize_Postfix(MenuCharacterCustomizationScreen __instance)
        {
            try
            {
                if (__instance == null) return;
                var trav = Traverse.Create(__instance);

                var startGameButton = trav.Field("startGameButton").GetValue<AnimatedButton>();
                TMP_Text? fontTemplate = startGameButton != null ? startGameButton.GetComponentInChildren<TMP_Text>(true) : null;

                // Instantiate Checkbox List at Top-Left of screen root
                GameObject widgetObj = new GameObject("GenderSelectorCheckboxList");
                selectorWidget = widgetObj.AddComponent<GenderSelectorWidget>();
                selectorWidget.Initialize(Plugin.DefaultGenderMode.Value, __instance.transform, fontTemplate);
                selectorWidget.OnGenderChanged += (mode) => HandleGenderChanged(__instance, mode);

                Plugin.Log?.LogInfo("Opt-In Gender Creation checkbox list initialized at top-left screen.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to initialize GenderSelectorWidget: {ex}");
            }
        }

        private static void HandleGenderChanged(MenuCharacterCustomizationScreen screen, GenderMode mode)
        {
            try
            {
                if (screen == null) return;
                var trav = Traverse.Create(screen);

                var preview = CharacterCustomizer.Instance?.CharacterPreview;
                var config = CustomizationLibrary.Instance?.MenuUIConfig;

                // 1. Immediately re-randomize preview to adapt to newly selected gender
                if (preview != null && config != null)
                {
                    preview.Randomize(config);
                }

                // 2. If currently viewing customization tabs, refresh active list
                var customizationTabContainer = trav.Field("customizationTabContainer").GetValue<GameObject>();
                var categoryListWidget = trav.Field("categoryListWidget").GetValue<CustomizationCategoryListWidget>();
                var customizationAssets = trav.Field("customizationAssets").GetValue<List<ItemAsset>>();
                var bumperMenuWidget = trav.Field("bumperMenuWidget").GetValue<BumperMenuWidget>();

                if (customizationTabContainer != null && customizationTabContainer.activeInHierarchy && categoryListWidget != null)
                {
                    int selectedTabIdx = bumperMenuWidget != null ? bumperMenuWidget.SelectedIndex : 1;
                    if (config != null && config.Tabs != null && selectedTabIdx >= 0 && selectedTabIdx < config.Tabs.Count)
                    {
                        var tab = config.Tabs[selectedTabIdx];
                        if (preview != null && customizationAssets != null)
                        {
                            trav.Method("SetupCategoryListWidget", tab, categoryListWidget, preview, customizationAssets).GetValue();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error during HandleGenderChanged: {ex}");
            }
        }

        // Method signature: SetupCategoryListWidget(Tab tab, CustomizationCategoryListWidget listWidget, CustomizationCharacterPreview preview, List<ItemAsset> customizations)
        [HarmonyPatch(typeof(MenuCharacterCustomizationScreen), "SetupCategoryListWidget")]
        [HarmonyPrefix]
        public static void SetupCategoryListWidget_Prefix(
            CustomizationUIConfigAsset.Tab tab,
            CustomizationCategoryListWidget listWidget,
            CustomizationCharacterPreview preview,
            ref List<ItemAsset> customizations)
        {
            try
            {
                GenderMode currentMode = GenderSelectorWidget.Instance?.CurrentMode ?? Plugin.DefaultGenderMode.Value;
                if (currentMode == GenderMode.Default || customizations == null)
                {
                    return; // 100% vanilla
                }

                bool includeUnisex = Plugin.IncludeUnisex.Value;
                var filtered = new List<ItemAsset>(customizations.Count);

                foreach (var item in customizations)
                {
                    if (item == null) continue;

                    string catName = string.Empty;
                    try
                    {
                        if (item.CustomizationAddon?.CategoryAsset != null)
                        {
                            catName = item.CustomizationAddon.CategoryAsset.name;
                        }
                    }
                    catch { }

                    if (GenderCategorizer.IsItemAllowed(item.name, catName, currentMode, includeUnisex))
                    {
                        filtered.Add(item);
                    }
                }

                customizations = filtered;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in SetupCategoryListWidget_Prefix: {ex}");
            }
        }

        // Method signature: GetRandomCharacterCustomizationAssets(List<ItemAsset> availableCustomizations, CustomizationUIConfigAsset configAsset)
        [HarmonyPatch(typeof(CustomizationLibrary), "GetRandomCharacterCustomizationAssets")]
        [HarmonyPrefix]
        public static void GetRandomCharacterCustomizationAssets_Prefix(
            ref List<ItemAsset> availableCustomizations,
            CustomizationUIConfigAsset configAsset)
        {
            try
            {
                GenderMode currentMode = GenderSelectorWidget.Instance?.CurrentMode ?? Plugin.DefaultGenderMode.Value;
                if (currentMode == GenderMode.Default || availableCustomizations == null)
                {
                    return;
                }

                bool includeUnisex = Plugin.IncludeUnisex.Value;
                var filtered = new List<ItemAsset>(availableCustomizations.Count);

                foreach (var item in availableCustomizations)
                {
                    if (item == null) continue;

                    string catName = string.Empty;
                    try
                    {
                        if (item.CustomizationAddon?.CategoryAsset != null)
                        {
                            catName = item.CustomizationAddon.CategoryAsset.name;
                        }
                    }
                    catch { }

                    if (GenderCategorizer.IsItemAllowed(item.name, catName, currentMode, includeUnisex))
                    {
                        filtered.Add(item);
                    }
                }

                if (filtered.Count > 0)
                {
                    availableCustomizations = filtered;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in GetRandomCharacterCustomizationAssets_Prefix: {ex}");
            }
        }
    }
}
