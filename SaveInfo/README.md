# Save Info

> AI-generated mod. See the [AI disclosure](../README.md#ai-disclosure).

Creates `save_info.txt` in each save directory when the game reads its metadata or saves it. It contains the character name, creation time, the game's last-played time, and the save ID. Empty values are shown as `Unknown`; timestamps are kept as stored by the game.

The file is a readable sidecar. The game continues to use its own save data. Files without this mod's `# Moonlight Peaks Save Info` marker are left alone.

Build and install with `dotnet build SaveInfo`, then restart the game and open the save list. The project copies the DLL into the configured BepInEx plugins folder.
