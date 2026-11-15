# Skip Intro

> AI-generated mod. See the [AI disclosure](../README.md#ai-disclosure).

Hold **F** for **0.6 seconds** at the **Run Away From Home** choice after character creation. The native purple hint uses the active game button as its template.

The plugin redirects the departure continuation to the shipped `Content_Intro_Director` skip branch, which reveals NPC names and invokes the normal external progression function. It does not fast-forward or load the train cinematic. Exact graph/node IDs are documented in the parent modding workspace (`dll-info/game/vampire-runtime.md`).

`BepInEx/config/com.moonlightpeaks.skipintro.cfg`: `General.EnableHoldFToSkip = true`.

Build and deploy with `dotnet build SkipIntro`; the project copies the DLL into the configured game’s BepInEx/plugins folder. Restart the game after building. Build compatibility is checked; the v1.1.0 bypass was confirmed working by the user; the v1.1.1 hold interaction still needs an in-game check.

`General.HoldDurationSeconds` adjusts the hold duration. Releasing F resets confirmation progress; the native highlight retracts over 0.2 seconds. Release F after character creation before starting a new hold.
