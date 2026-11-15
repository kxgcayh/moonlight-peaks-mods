# Moonlight Peaks Game Bridge

A BepInEx plugin that gives the local Game MCP server read-only access to the running game. It exposes status, an on-demand game state snapshot, selected game events, and an explicitly requested screenshot. It does not provide commands or input simulation. Selected events are written to the normal BepInEx log; no growing event buffer is kept in memory.

## Build and install

```sh
dotnet build GameBridge
```

The project copies the DLL into the configured game's `BepInEx/plugins/MoonlightPeaks.GameBridge` directory. Restart the game after building. The bridge creates `/tmp/moonlight-gamebridge-<uid>/bridge.sock`; the directory is mode 0700 and the socket is 0600. It handles Unity objects on the main thread, limits outstanding requests, and captures screenshots at the end of the frame. The MCP server writes requested screenshots and continuously mirrors the game logs under this plugin folder. Log copies reset when an MCP server starts.

The MCP server and agent setup are documented in the parent modding workspace (`tools/game-mcp/README.md`). If the bridge cannot start, check `BepInEx/LogOutput.log` for `Game Bridge`.
