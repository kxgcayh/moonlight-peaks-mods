# Portrait Pack Loader

> AI-generated mod. See the [AI disclosure](../README.md#ai-disclosure).

Loads portrait packs made for the deleted *Serena's Portrait Replacer* (for example Ash's Portrait Set, Moonlight Bachelors, and Rain's Revamped Vanilla Portraits). Packs install unchanged into `BepInEx/plugins/SerenaPortraitReplacer/Portraits/<Character>/<Key>.png`.

How it works: the mod maps every NPC portrait sprite reference (all portrait styles) and loaded creature `PortraitAsset` to a Serena-style key (`AmbrosiaEvan_Angry`, `WebbFiona_Happy_Rose_Vampire`, `AmbrosiaEvan_small`, `Cowcula_black`), then swaps the sprite when `ImageForAddressable` finishes loading it. It replaces dialogue, relationship, calendar and small portraits regardless of the portrait style setting. Game internals are documented in `dll-info/game/vampire-runtime.md` §13 of the parent workspace.

Config `BepInEx/config/com.moonlightpeaks.portraitpackloader.cfg`:
- `General.PortraitsFolder`: pack folder, relative to `BepInEx/plugins` (default `SerenaPortraitReplacer/Portraits`).
- `Debug.WriteDetectedKeys`: writes `DetectedPortraitKeys.txt` next to the folder: every key the game exposes, which matched, and pack files with no match.

Build: `dotnet build PortraitPackLoader` (copies the DLL into `BepInEx/plugins`). Restart the game after adding PNGs.

Status: with Ash's Portrait Set v1.5, 437/471 files map at the main menu; most of the rest are creature portraits that map once they load in-game. Confirmed working in-game by the user (2026-11-16).
