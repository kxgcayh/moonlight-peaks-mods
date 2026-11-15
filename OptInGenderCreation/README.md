# Opt-In Gender Creation

> AI-generated mod. See the [AI disclosure](../README.md#ai-disclosure).

**Opt-In Gender Creation** is a character creation style filter for *Moonlight Peaks* v1.3.9. A selector at the top left of the screen offers `Default`, `Male`, and `Female`. These are appearance presets, not a gender field saved to the character.

## Features
- **Default**: Leaves the game's item lists and randomizer unrestricted.
- **Male**: Filters hairstyles and clothing toward masculine styles; facial hair remains available.
- **Female**: Filters hairstyles and clothing toward feminine styles and hides facial hair.
- **Selector**: Three pointed purple banners sit at the top left of Character Creation, matching the shape, gold text, and square indicators of the bottom-left action controls. One mode is active at a time. The custom widget uses the game's font with standard Unity UI buttons and hover/press colors.
- **Mode changes**: Selecting a mode re-randomizes the preview and refreshes the open customization list. Pressing `R` randomizes from the selected mode's available items.

The style classifications are curated rules. Some items may need reclassification after in-game review.

## Configuration (`BepInEx/config/com.moonlightpeaks.mod.optingendercreation.cfg`)
- `DefaultGenderMode`: Initial mode on opening character creation (`Default`, `Male`, `Female`). Default: `Default`.
- `IncludeUnisex`: Whether neutral items (t-shirts, hoodies, basic pants, sneakers, and unisex hairstyles) appear in both Male and Female modes. Default: `true`.
