# Moonlight Peaks Mods

BepInEx + Harmony mods for *Moonlight Peaks*. Each folder is one mod; projects target `netstandard2.1`. GameBridge, SaveInfo and SkipIntro copy the built DLL into `BepInEx/plugins/` after build.

## Set the game path

`Directory.Build.props` defines `GameDir` for every project from the gitignored `GameDir.local.props`.

Inside the modding workspace, don't edit it by hand: set `MP_GAME_DIR` in the workspace's `paths.local.env` and run `bash setup.sh`, which generates this file and configures the tools too.

Using this repo on its own:

```sh
cp GameDir.local.props.example GameDir.local.props   # then edit the path
```

`GameDir.local.props` is gitignored. Alternatives: `dotnet build -p:GameDir="/path/to/Moonlight Peaks"`, or rely on the default Steam location.

## Build

```sh
dotnet build SkipIntro/MoonlightPeaks.SkipIntro.csproj
```

## Mods

| Mod | Description |
| --- | --- |
| [`GameBridge`](./GameBridge/) | Local HTTP bridge used by the game-observer MCP |
| [`PortraitPackLoader`](./PortraitPackLoader/) | Loads Serena-format portrait packs (Serena's Portrait Replacer replacement) |
| [`OptInGenderCreation`](./OptInGenderCreation/) | Character creation style filter |
| [`SaveInfo`](./SaveInfo/) | Save file info plugin |
| [`SavePathOverride`](./SavePathOverride/) | macOS save path patch utility |
| [`SkipIntro`](./SkipIntro/) | Skips the intro sequence |

Only original mods are tracked. Third-party mods kept here for reference are gitignored.
