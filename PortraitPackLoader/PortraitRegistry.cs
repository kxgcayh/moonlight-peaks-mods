using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MoonlightPeaks.PortraitPackLoader
{
    /// <summary>
    /// Maps addressable sprite references (by GUID) to PNG files in a Serena-style portrait pack.
    /// Keys follow Serena's naming: Character[_Variant][_Context][_Stance], Character_small, CreaturePortrait.
    /// </summary>
    internal static class PortraitRegistry
    {
        private static string _folder = "";
        // normalized key -> file path
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        // sprite reference id -> normalized key of the matched file
        private static readonly Dictionary<string, string> RefToKey = new Dictionary<string, string>();
        // every key the game exposes -> description (for the dump)
        private static readonly SortedDictionary<string, string> Detected = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SeenRefs = new HashSet<string>();
        private static readonly HashSet<string> MatchedFiles = new HashSet<string>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        // original sprite -> replacement sprite
        internal static readonly Dictionary<Sprite, Sprite> SpriteSwaps = new Dictionary<Sprite, Sprite>();

        private static bool _npcsScanned;
        private static float _nextScan;
        private static float _lastPortraitAssetScan = -100f;
        private static bool _dumpDirty;
        private static float _dumpAt;

        internal static int FileCount => Files.Count;

        internal static void Init(string folder)
        {
            _folder = folder;
            if (!Directory.Exists(folder)) return;
            foreach (var path in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
            {
                var key = Norm(Path.GetFileNameWithoutExtension(path));
                if (Files.ContainsKey(key))
                    PortraitPackLoaderPlugin.Logger.LogWarning($"Duplicate portrait key '{key}': {path}");
                Files[key] = path;
            }
        }

        internal static void Tick()
        {
            if (Files.Count == 0) return;
            var now = Time.realtimeSinceStartup;
            if (!_npcsScanned && now >= _nextScan)
            {
                _nextScan = now + 2f;
                TryScanNpcs();
            }
            if (_dumpDirty && now >= _dumpAt) WriteDump();
        }

        /// <summary>Called when the game finished loading a sprite reference.</summary>
        internal static void OnSpriteLoaded(AssetReference reference, Sprite original)
        {
            if (Files.Count == 0 || reference == null || original == null) return;
            if (SpriteSwaps.ContainsKey(original)) return;

            var id = RefId(reference);
            if (!RefToKey.TryGetValue(id, out var key))
            {
                if (!_npcsScanned) TryScanNpcs();
                // Creature avatars are PortraitAssets loaded on demand; rescan, throttled.
                if (!RefToKey.ContainsKey(id) && Time.realtimeSinceStartup - _lastPortraitAssetScan > 3f)
                    ScanPortraitAssets();
                if (!RefToKey.TryGetValue(id, out key)) return;
            }

            var replacement = MakeSprite(key, original);
            if (replacement != null) SpriteSwaps[original] = replacement;
        }

        private static void TryScanNpcs()
        {
            try
            {
                if (!AddressableLibrary<NpcLibrary>.Exists) return;
                var npcs = NpcLibrary.NpcConfigs;
                if (npcs == null || npcs.Count == 0) return;

                PortraitVariantAsset? defaultVariant = null;
                try { defaultVariant = AddressableLibrary<AssetLibrary>.Instance.DefaultPortraitVariantAsset; } catch { }

                foreach (var npc in npcs)
                {
                    if (npc == null) continue;
                    var npcNames = NameForms(npc.name);

                    foreach (var collection in npc.portraitVariants ?? new List<PortraitVariantCollection>())
                    {
                        if (collection?.Variants == null) continue;
                        foreach (var data in collection.Variants)
                        {
                            if (data == null) continue;
                            var variant = data.Variant;
                            bool isDefault = variant == null || variant == defaultVariant || IsDefaultName(variant.name);
                            var variantForms = isDefault ? new List<string> { "", "neutral" } : NameForms(variant!.name);
                            var ctx = QualifierForms(collection.RelationshipContext, npcNames);
                            var stance = QualifierForms(collection.Stance, npcNames);

                            var aliases = new List<string>();
                            foreach (var n in npcNames)
                            foreach (var v in variantForms)
                            foreach (var c in ctx)
                            foreach (var s in stance)
                            {
                                aliases.Add(Join(n, v, c, s));
                                aliases.Add(Join(n, v, s, c));
                            }
                            var desc = $"npc={npc.name} variant={variant?.name ?? "-"} context={collection.RelationshipContext?.name ?? "-"} stance={collection.Stance?.name ?? "-"}";
                            Register(data.LocalizedPortrait, aliases, desc);
                        }
                    }

                    var small = Traverse.Create(npc).Property("PortaitIcon").GetValue<AssetReference>();
                    Register(small, npcNames.Select(n => n + "_small").ToList(), $"npc={npc.name} small");

                    foreach (var o in npc.PortraitIconOverrides ?? new List<NpcConfigAsset.SpriteOverrideEntry>())
                        Register(o?.SpriteReference, OverrideAliases(npcNames, o?.EventAsset, "_small"), $"npc={npc.name} small override event={o?.EventAsset?.name}");
                    foreach (var o in npc.PortraitOverrides ?? new List<NpcConfigAsset.LocalizedSpriteOverrideEntry>())
                        Register(o?.SpriteReference, OverrideAliases(npcNames, o?.EventAsset, ""), $"npc={npc.name} override event={o?.EventAsset?.name}");
                }

                _npcsScanned = true;
                ScanPortraitAssets();
                PortraitPackLoaderPlugin.Logger.LogInfo($"Mapped {RefToKey.Count} sprite reference(s) to pack files from {npcs.Count} NPC config(s).");
            }
            catch (Exception e)
            {
                PortraitPackLoaderPlugin.Logger.LogWarning($"NPC portrait scan failed, will retry: {e.Message}");
            }
        }

        private static void ScanPortraitAssets()
        {
            _lastPortraitAssetScan = Time.realtimeSinceStartup;
            try
            {
                foreach (var asset in Resources.FindObjectsOfTypeAll<PortraitAsset>())
                {
                    if (asset == null) continue;
                    Register(asset.LocalizedSprite, NameForms(asset.name), $"portraitAsset={asset.name}");
                }
            }
            catch (Exception e)
            {
                PortraitPackLoaderPlugin.Logger.LogWarning($"PortraitAsset scan failed: {e.Message}");
            }
        }

        private static void Register(LocalizedSpriteReference? localized, List<string> aliases, string desc)
        {
            if (localized?.Entries == null) return;
            foreach (var entry in localized.Entries)
                Register(entry?.SpriteReference, aliases, desc + $" style={entry?.PortraitStyle?.name ?? "-"}");
        }

        private static void Register(AssetReference? reference, List<string> aliases, string desc)
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID)) return;
            var id = RefId(reference);
            var primary = aliases.Count > 0 ? aliases[0] : "?";
            if (!SeenRefs.Add(id + "|" + primary)) return;

            string? match = aliases.FirstOrDefault(Files.ContainsKey);
            if (match != null)
            {
                MatchedFiles.Add(match);
                if (!RefToKey.ContainsKey(id)) RefToKey[id] = match;
            }

            Detected[primary + (match != null && match != primary ? $" (matched {match})" : "")] =
                (match != null ? "MATCH " : "      ") + desc;
            MarkDumpDirty();
        }

        private static Sprite? MakeSprite(string key, Sprite original)
        {
            try
            {
                if (!Textures.TryGetValue(key, out var tex))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(Files[key]))) return null;
                    tex.name = "PortraitPack_" + key;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    Textures[key] = tex;
                }
                var rect = original.rect;
                var pivot = new Vector2(original.pivot.x / rect.width, original.pivot.y / rect.height);
                var ppu = original.pixelsPerUnit * (tex.width / rect.width);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprite.name = original.name;
                return sprite;
            }
            catch (Exception e)
            {
                PortraitPackLoaderPlugin.Logger.LogWarning($"Could not load portrait '{key}': {e.Message}");
                return null;
            }
        }

        private static List<string> OverrideAliases(List<string> npcNames, UnityEngine.Object? evt, string suffix)
        {
            var result = new List<string>();
            var forms = evt != null ? NameForms(evt.name) : new List<string>();
            foreach (var n in npcNames)
                foreach (var e in forms)
                    result.Add(Join(n, e) + suffix);
            if (result.Count == 0) result.AddRange(npcNames.Select(n => n + "_override" + suffix));
            return result;
        }

        /// <summary>
        /// Stance/context names. Serena drops the character's first name from stances,
        /// e.g. Stance_FionaRose on WebbFiona -> "rose", Stance_OrlockHungover -> "hungover".
        /// </summary>
        private static List<string> QualifierForms(UnityEngine.Object? asset, List<string> npcNames)
        {
            if (asset == null || IsDefaultName(asset.name)) return new List<string> { "" };
            var forms = NameForms(asset.name);
            var result = new List<string>();
            foreach (var f in forms)
            {
                foreach (var npc in npcNames)
                {
                    var flat = npc.Replace("_", "");
                    for (int len = flat.Length; len >= 3; len--)
                    {
                        var tail = flat.Substring(flat.Length - len);
                        if (f.StartsWith(tail) && f.Length > len)
                        {
                            var rest = f.Substring(len).TrimStart('_');
                            if (!result.Contains(rest)) result.Add(rest);
                            break;
                        }
                    }
                }
            }
            foreach (var f in forms) if (!result.Contains(f)) result.Add(f);
            return result;
        }

        private static readonly string[] Prefixes =
        {
            "npcconfigasset", "npcconfig", "npc", "config", "portraitvariantasset", "portraitvariant", "variant",
            "portraitasset", "portrait", "stanceasset", "stance", "relationshipcontextasset", "relationshipcontext",
            "context", "event", "asset", "pv"
        };

        /// <summary>Normalized name candidates, most literal first.</summary>
        private static List<string> NameForms(string raw)
        {
            var forms = new List<string>();
            void Add(string s) { s = Norm(s); if (s.Length > 0 && !forms.Contains(s)) forms.Add(s); }
            Add(raw);
            var stripped = Norm(raw);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var p in Prefixes)
                {
                    if (stripped.StartsWith(p + "_") || (stripped.StartsWith(p) && stripped.Length > p.Length && p.Length > 3))
                    {
                        stripped = stripped.Substring(p.Length).TrimStart('_');
                        changed = true;
                        break;
                    }
                    if (stripped.EndsWith("_" + p))
                    {
                        stripped = stripped.Substring(0, stripped.Length - p.Length - 1);
                        changed = true;
                        break;
                    }
                }
            }
            if (stripped.StartsWith("deprecated_")) stripped = stripped.Substring("deprecated_".Length);
            // Creature avatars: Cowcula_Portrait_Black -> cowcula_black
            stripped = stripped.Replace("_portrait_", "_");
            if (stripped.EndsWith("_portrait")) stripped = stripped.Substring(0, stripped.Length - "_portrait".Length);
            Add(stripped);
            Add(stripped.Replace("_", ""));
            return forms;
        }

        private static bool IsDefaultName(string name)
        {
            var n = Norm(name);
            return n.EndsWith("default") || n.EndsWith("neutral") || n.EndsWith("none") || n.EndsWith("normal");
        }

        private static string Join(params string[] parts) => string.Join("_", parts.Where(p => !string.IsNullOrEmpty(p)));

        private static string Norm(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s.Trim())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                else if (ch == '_' || ch == ' ' || ch == '-') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        private static string RefId(AssetReference r) => r.AssetGUID + ":" + r.SubObjectName;

        private static void MarkDumpDirty()
        {
            if (!PortraitPackLoaderPlugin.WriteDetectedKeys.Value) return;
            _dumpDirty = true;
            _dumpAt = Time.realtimeSinceStartup + 5f;
        }

        private static void WriteDump()
        {
            _dumpDirty = false;
            try
            {
                var matchedFiles = MatchedFiles;
                var sb = new StringBuilder();
                sb.AppendLine("# Keys exposed by the game. MATCH = a pack file replaces it.");
                foreach (var kv in Detected) sb.AppendLine($"{kv.Value}\t{kv.Key}");
                sb.AppendLine();
                sb.AppendLine("# Pack files with no matching game sprite (yet; creature portraits appear once loaded):");
                foreach (var kv in Files.OrderBy(k => k.Key))
                    if (!matchedFiles.Contains(kv.Key)) sb.AppendLine(kv.Key);
                var path = Path.Combine(Path.GetDirectoryName(_folder.TrimEnd('/', '\\')) ?? _folder, "DetectedPortraitKeys.txt");
                File.WriteAllText(path, sb.ToString());
                PortraitPackLoaderPlugin.Logger.LogInfo($"Wrote {Detected.Count} detected key(s), {matchedFiles.Count}/{Files.Count} pack file(s) matched -> {path}");
            }
            catch (Exception e)
            {
                PortraitPackLoaderPlugin.Logger.LogWarning($"Could not write key dump: {e.Message}");
            }
        }
    }
}
