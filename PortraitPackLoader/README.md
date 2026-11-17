# Portrait Pack Loader

> AI-generated mod. See the [AI disclosure](../README.md#ai-disclosure).

Download: [Nexus Mods, mod 157](https://www.nexusmods.com/moonlightpeaks/mods/157).

Loads portrait packs made for the deleted *Serena's Portrait Replacer* (for example Ash's Portrait Set, Moonlight Bachelors, and Rain's Revamped Vanilla Portraits). Put pack files in `BepInEx/plugins/PortraitPacks/Portraits/<Character>/<Key>.png`. Packs whose instructions say to extract into `SerenaPortraitReplacer/Portraits` also work unchanged: that legacy folder is still loaded, and files in `PortraitPacks` win when both folders have the same key.

How it works: the mod maps every NPC portrait sprite reference (all portrait styles) and loaded creature `PortraitAsset` to a Serena-style key (`AmbrosiaEvan_Angry`, `WebbFiona_Happy_Rose_Vampire`, `AmbrosiaEvan_small`, `Cowcula_black`), then swaps the sprite when `ImageForAddressable` finishes loading it. It replaces dialogue, relationship, calendar and small portraits regardless of the portrait style setting. Game internals are documented in `dll-info/game/vampire-runtime.md` §13 of the parent workspace.

Config `BepInEx/config/com.moonlightpeaks.portraitpackloader.cfg`:
- `General.PortraitsFolder`: pack folder, relative to `BepInEx/plugins` (default `PortraitPacks/Portraits`). Configs still set to the old default `SerenaPortraitReplacer/Portraits` switch to the new default on first launch.
- `Debug.WriteDetectedKeys` (default `false`; turn on when making or debugging a pack): writes `DetectedPortraitKeys.txt` next to the folder: every key the game exposes, which matched, and pack files with no match.

Build: `dotnet build PortraitPackLoader` (copies the DLL into `BepInEx/plugins`). Restart the game after adding PNGs.

Status: confirmed working in-game (2026-11-16). With Ash's Portrait Set v1.5, 437/471 files map at the main menu. The 34 unmatched files (from `DetectedPortraitKeys.txt`):

- **Creature portraits, 18 files (load on demand):** cheeken (2), cowcula, draculamb (2), piggoat (3), plumpkin (2), rabbicula (3), stoney, vampster, plus amar, celine and medusa (not yet confirmed to be creatures). They match once the creature loads in-game. A plain `cowcula` file may never match, because the game only uses the coloured versions.
- **No matching game key, 16 files:**
  - `hosurei_*_neckless` (7): the pack's alternative look for Rei; the game has no such variant.
  - `webbfiona_mean*` (4): the game has an angry expression for Fiona but no mean one.
  - `albertus_small`: the game has no small portrait for Albertus.
  - `ambrosiasamael_romantic`, `draculaviktor_humanoid`, `draculaviktor_viktorstorytelling2`, `hendersonpenelope_postreunion`: not in the key dump. They are either from another game version or appear only in specific story scenes.

The expected total after creatures load is about 455/471.
