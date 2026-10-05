# Saleblazers JEI (Just Enough Items)

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/romrem30-eng/Saleblazers.JEI/releases)
[![Game](https://img.shields.io/badge/game-Saleblazers-informational.svg)](https://store.steampowered.com/app/1416960/Saleblazers/)
[![Mod Loader](https://img.shields.io/badge/loader-BepInEx%206%20IL2CPP-purple.svg)](https://github.com/BepInEx/BepInEx)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Saleblazers has hundreds of items, dozens of workbenches, cooking pots, and a massive research tree. Having to constantly Alt-Tab to an incomplete wiki just to see what workbench crafts an item or what ingredients you need gets old fast.

This mod adds an in-game recipe browser and item catalog directly into Saleblazers, inspired by Minecraft's Just Enough Items (JEI). It reads the game's actual internal databases on launch, so all recipes, stations, and stats are 100% accurate to your game version.

---

## What It Does

- **In-Game Item & Recipe Search:** Fast, searchable grid of every item in the game with category filters (**Weapons**, **Armor**, **Food**, **Materials**, **Building**, **Workstations**, **Consumables**).
- **Recipes (`R`) & Usages (`U`):** Hover over any item in the catalog or inventory and press `R` to see how to craft it, or `U` to see what recipes it's used in.
- **Clickable Workstations:** Click any workbench badge in a recipe card to instantly see everything that can be crafted at that station.
- **Clickable Ingredients:** Click any ingredient or product in a recipe to jump straight to its catalog entry.
- **Cooking & Food Mechanics:** Shows cooking pot requirements, satiation, hydration, and bonus stat buffs for meals.
- **Drop Tables:** Shows drop rates and amounts for bosses (Bellstalker, Zena, Boar Leader, Sifu, Karrax), fishing, enemy factions, mining, and harvesting.
- **Research & Skill Tree Viewer:** See required research tables, prerequisite items, and unlock conditions without running back to your base.
- **Bilingual (English / Russian):** Includes an on-the-fly language toggle (`Lang: RU/EN`) in the top bar. Search works seamlessly in both English and Russian.
- **Optional Spawner (Host Only):** If you're the lobby host, you can optionally spawn items for testing. Can be completely disabled in the config file.
- **Smooth Cursor Handling:** Automatically unlocks mouse look when opening the catalog and restores camera control when closed.

---

## Controls

| Key / Action | What it does |
|---|---|
| `J` or `F8` | Open / close the JEI catalog |
| `R` (hovering over item) | View recipes (how to craft this item) |
| `U` (hovering over item) | View usages (what this item crafts) |
| `Mouse Wheel` (over recipe) | Scroll through available recipes |
| `Mouse Wheel` (over grid) | Flip catalog pages |
| `Left Click` on station | Jump to that workbench's recipes |
| `Left Click` on ingredient | Jump directly to that item's page |
| `Lang: RU/EN` (top right) | Switch between English and Russian |
| `ESC` | Close the catalog |

---

## Requirements

This mod requires **BepInEx 6 IL2CPP** and our pre-configured mod loader environment.
Download and install the **[Saleblazers Modding Starter Kit](https://github.com/romrem30-eng/Saleblazers.ModdingStarterKit)**:
- Pre-configured BepInEx 6 IL2CPP build with Doorstop loader and dumped interop assemblies.
- Built-in In-Game Mod Manager (`Saleblazers.ModMenu`), enabling in-game mod toggles and live `.cfg` configuration editing.

---

## Installation

### Recommended Installation
1. Download `Saleblazers.BepInExPack-v6.0.0.zip` from the [Starter Kit Releases](https://github.com/romrem30-eng/Saleblazers.ModdingStarterKit/releases).
2. Extract the archive contents into your game directory (`Saleblazers/Default/`).
3. Download `Saleblazers.JEI.dll` from [JEI Releases](https://github.com/romrem30-eng/Saleblazers.JEI/releases).
4. Place `Saleblazers.JEI.dll` into `Saleblazers/Default/BepInEx/plugins/`.
5. Launch the game through Steam. The main menu will show `[Modded]` and a `MODS` button. Press `J` in-game to open JEI!

### Automated Script (if using zip bundle)
1. Download `Saleblazers.JEI-v1.0.0.zip` from [Releases](https://github.com/romrem30-eng/Saleblazers.JEI/releases).
2. Extract the zip into any folder.
3. Run `install.bat`.
   - Locates your Saleblazers installation automatically.
   - Installs BepInEx 6 IL2CPP if not present.
   - Copies `Saleblazers.JEI.dll` into `BepInEx\plugins\`.
4. Launch the game and press `J` or `F8` in-game.

### Uninstallation
- **Automated:** Run `uninstall.bat` from the extracted folder.
- **Manual:** Delete `Saleblazers.JEI.dll` from `Saleblazers/Default/BepInEx/plugins/`.

---

## Multiplayer Safety

- **100% Safe for Co-op:** The recipe browser, search, drop tables, and research tree work completely client-side. Other players in your lobby do not need the mod installed for you to use it.
- **Spawning is Host-Only:** The optional spawn buttons only work if you are the lobby host. Connected clients cannot spawn items.

---

## Configuration

The config file is created automatically on first launch at:
```
<GameDir>\BepInEx\config\com.saleblazers.jei.cfg
```

You can tweak options like:

```ini
[JEI]
## Enable the JEI catalog overlay.
EnableJEI = true

## Enable the item spawner buttons (host only). Set to false for pure recipe lookup.
EnableTMI = true

## Show sell value and recipe count in inventory tooltips.
EnablePriceTooltip = true

## Default batch amount for the second spawn button.
SpawnCount = 10

## Active UI Language (RU or EN).
Language = RU

## Catalog grid columns (4 to 12).
GridCols = 8

## Catalog grid rows (3 to 8).
GridRows = 6
```

---

## Building from Source

If you want to compile it yourself:

1. Clone the repo:
   ```bash
   git clone https://github.com/romrem30-eng/Saleblazers.JEI.git
   cd Saleblazers.JEI
   ```
2. Make sure you have [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) installed and game path in `Saleblazers.JEI.csproj` pointing to your Saleblazers folder.
3. Run:
   ```bash
   dotnet build -c Release
   ```
4. Compiled DLL is placed in `bin/Release/Saleblazers.JEI.dll`.

---

## License

This project is licensed under the [MIT License](LICENSE).
