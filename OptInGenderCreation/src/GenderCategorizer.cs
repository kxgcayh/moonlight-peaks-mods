using System;
using System.Collections.Generic;

namespace OptInGenderCreation
{
    public static class GenderCategorizer
    {
        // Explicitly classified male hairstyles (17 styles)
        // Note: Orlock_*_Drunk entries are NPC stance variants retained under Male pending in-game confirmation.
        public static readonly HashSet<string> MaleHairstyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Item_Customization_Character_Hair_Aras",
            "Item_Customization_Character_Hair_Balthazar",
            "Item_Customization_Character_Hair_Dracula",
            "Item_Customization_Character_Hair_Dracula_NoMullet",
            "Item_Customization_Character_Hair_Dragan",
            "Item_Customization_Character_Hair_Evan",
            "Item_Customization_Character_Hair_Jarvis",
            "Item_Customization_Character_Hair_ManRegular",
            "Item_Customization_Character_Hair_Noel",
            "Item_Customization_Character_Hair_Orlock",
            "Item_Customization_Character_Hair_Orlock_Drunk", // Ambiguous NPC stance variant
            "Item_Customization_Character_Hair_Orlock_NoMullet",
            "Item_Customization_Character_Hair_Orlock_NoMullet_Drunk", // Ambiguous NPC stance variant
            "Item_Customization_Character_Hair_Saga",
            "Item_Customization_Character_Hair_Samael",
            "Item_Customization_Character_Hair_Sungod",
            "Item_Customization_Character_Hair_Winston"
        };

        // Explicitly classified female hairstyles (11 styles)
        // Note: BunsAndBand and Skullgirl are retained under Female pending in-game visual confirmation.
        public static readonly HashSet<string> FemaleHairstyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Item_Customization_Character_Hair_Alina",
            "Item_Customization_Character_Hair_BunsAndBand", // Ambiguous: bun with headband, female pending visual check
            "Item_Customization_Character_Hair_Elvira",
            "Item_Customization_Character_Hair_Fiona",
            "Item_Customization_Character_Hair_Laveau",
            "Item_Customization_Character_Hair_LittleBuns",
            "Item_Customization_Character_Hair_Mina",
            "Item_Customization_Character_Hair_Moongoddess",
            "Item_Customization_Character_Hair_Sabrina",
            "Item_Customization_Character_Hair_Skullgirl", // Ambiguous: name feminine, visual pending confirmation
            "Item_Customization_Character_Hair_Yasmine"
        };

        // Explicitly classified unisex hairstyles (11 styles, gated by IncludeUnisex)
        // Note: Hair_01, 02, 03 (numbered presets) and Hair_Rei, Hair_Tae (Hosu siblings) are classified as Unisex pending in-game visual confirmation.
        public static readonly HashSet<string> UnisexHairstyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Item_Customization_Character_Hair_01", // Ambiguous preset: unisex pending visual check
            "Item_Customization_Character_Hair_02", // Ambiguous preset: unisex pending visual check
            "Item_Customization_Character_Hair_03", // Ambiguous preset: unisex pending visual check
            "Item_Customization_Character_Hair_Beanie",
            "Item_Customization_Character_Hair_CurlyWizard",
            "Item_Customization_Character_Hair_ElvishHair",
            "Item_Customization_Character_Hair_Ponytail",
            "Item_Customization_Character_Hair_Rei", // Ambiguous Hosu sibling: unisex pending visual check (resolves MaleHairstyles vs FemaleNpcs contradiction)
            "Item_Customization_Character_Hair_SpikeyCap",
            "Item_Customization_Character_Hair_Tae", // Ambiguous Hosu sibling: unisex pending visual check
            "Item_Customization_Character_Hair_TurbanDreads"
        };

        // Canonical male NPCs
        private static readonly string[] MaleNpcs =
        {
            "Aras", "Balthazar", "Brook", "Dracula", "Dragan", "Evan", "Jarvis", "Larry", "Logan", "Ludo", "Noel", "Orlock", "Ridge", "Saga", "Samael", "Viktor", "Vlad", "Winston"
        };

        // Canonical female NPCs
        private static readonly string[] FemaleNpcs =
        {
            "Alina", "Elvira", "Fiona", "Jada", "Kim", "Laveau", "Luna", "Mina", "Moongoddess", "Penelope", "Persephone", "Rei", "Sabrina", "Skullgirl", "Tae", "Yasmin", "Yasmine"
        };

        public static bool IsItemAllowed(string itemName, string categoryName, GenderMode mode, bool includeUnisex)
        {
            if (mode == GenderMode.Default || string.IsNullOrEmpty(itemName))
            {
                return true;
            }

            // Always allow "None" unequipped / bald option
            if (itemName.EndsWith("_None", StringComparison.OrdinalIgnoreCase) || itemName.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 1. Hairstyle filtering
            if (categoryName.IndexOf("HairStyle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("_Hair_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (mode == GenderMode.Male)
                {
                    if (MaleHairstyles.Contains(itemName)) return true;
                    if (FemaleHairstyles.Contains(itemName)) return false;
                    if (UnisexHairstyles.Contains(itemName)) return includeUnisex;
                    // Unrecognized hairstyles fall back to Unisex when enabled
                    return includeUnisex;
                }
                if (mode == GenderMode.Female)
                {
                    if (FemaleHairstyles.Contains(itemName)) return true;
                    if (MaleHairstyles.Contains(itemName)) return false;
                    if (UnisexHairstyles.Contains(itemName)) return includeUnisex;
                    // Unrecognized hairstyles fall back to Unisex when enabled
                    return includeUnisex;
                }
            }

            // 2. Beards & Facial hair
            if (categoryName.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                categoryName.IndexOf("FacialHair", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (mode == GenderMode.Female)
                {
                    // Suppress facial hair for female mode
                    return itemName.IndexOf("None", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return true; // Available in Male and Default modes
            }

            // 3. Skirts & Dresses are strictly Female
            if (itemName.IndexOf("Skirt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("Dress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("Gown", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return mode == GenderMode.Female;
            }

            // 4. Crop tops & Corsets are Female
            if (itemName.IndexOf("Croptop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("Corset", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return mode == GenderMode.Female;
            }

            // 5. Yoga suits are Female
            if (itemName.IndexOf("Yoga", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return mode == GenderMode.Female;
            }

            // 6. Suits, Tuxedos, Ties, Vests are Male
            if (itemName.IndexOf("Tuxedo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("Suit", StringComparison.OrdinalIgnoreCase) >= 0 && itemName.IndexOf("Yoga", StringComparison.OrdinalIgnoreCase) < 0 ||
                itemName.IndexOf("Tie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("Vest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return mode == GenderMode.Male;
            }

            // 7. Check NPC origin associations
            foreach (var npc in MaleNpcs)
            {
                if (itemName.IndexOf(npc, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return mode == GenderMode.Male;
                }
            }

            foreach (var npc in FemaleNpcs)
            {
                if (itemName.IndexOf(npc, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return mode == GenderMode.Female;
                }
            }

            // 8. General basics (T-shirts, hoodies, sweatpants, simple sneakers)
            return includeUnisex;
        }
    }
}
