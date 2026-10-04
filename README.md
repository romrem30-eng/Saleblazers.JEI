# Saleblazers JEI (Just Enough Items)

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/romrem30-eng/Saleblazers.JEI/releases)
[![Status](https://img.shields.io/badge/status-stable-success.svg)](https://github.com/romrem30-eng/Saleblazers.JEI)
[![Target](https://img.shields.io/badge/game-Saleblazers%20(IL2CPP)-informational.svg)](https://store.steampowered.com/app/1416960/Saleblazers/)
[![Framework](https://img.shields.io/badge/runtime-BepInEx%206%20IL2CPP-purple.svg)](https://github.com/BepInEx/BepInEx)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

An in-game item catalog, recipe lookup, station browser, and research guide for Saleblazers, inspired by Just Enough Items (JEI). Built natively for BepInEx 6 Unity IL2CPP.

---

## Overview

Saleblazers features hundreds of items, multi-tier crafting stations, and an extensive research tree. Without an integrated recipe viewer, tracking dependencies, required crafting benches, and research prerequisites often requires tedious manual guesswork.

Saleblazers JEI parses the internal runtime databases (`HRItemDatabase`, `HRCraftingDatabase`, `HRSkillTree`) dynamically at game launch and provides a comprehensive, responsive in-game overlay.

---

## Features

- **Item & Recipe Catalog**: Searchable, paginated grid displaying all items registered in the game database with fast category filters (**Weapons**, **Armor**, **Food**, **Materials**, **Building**, **Workstations**, **Consumables**).
- **Bi-Directional Recipe Navigation**:
  - **Recipes (`R`)**: Inspect how to craft the selected item, including workstation type, craft duration, and ingredient quantities.
  - **Usages (`U`)**: Inspect all recipes where the selected item is used as an ingredient, or all items crafted by a selected workbench.
- **Dynamic Culinary Simulation**: Decodes Saleblazers' internal cooking system, showing required cooking vessels, satiation, hydration values, and food affixes/potencies.
- **Comprehensive Attribute & Buff Codex**: Dedicated Attributes tab featuring 200+ gameplay attributes with value ranges, triggers (on-block, on-hit, on-crit), durations, equipment compatibility, and granting meals.
- **World & Entity Drop Tables**: Integrated drops from game databases covering fish (angling), wildlife, bosses (Bellstalker, Zena, Boar Leader, Sifu, Karrax), enemy factions, mining veins, timber, and crops with exact percentage chances and quantities.
- **Interactive Workstation Links**: Clicking a crafting station badge in any recipe immediately redirects the catalog to that station, showing everything craftable on it.
- **Interactive Ingredient Links**: Clicking any ingredient or recipe result jumps directly to its catalog entry.
- **Research & Skill Tree Breakdown**: Displays the exact skill tree node name, research table requirements, prerequisite items (with jump button), and unlock criteria.
- **Dual Spawner (TMI)**: Dedicated `SPAWN x1` and `SPAWN x10` batch spawn buttons (host only; can be toggled via configuration).
- **Inventory Price & Recipe Tooltip**: Hovering over inventory slots displays the item's base sell value, crafted value, and recipe counts.
- **Live Bilingual Localization (RU / EN)**:
  - Toggle between English and Russian on the fly via the top bar `Lang: RU/EN` button without restarting the game.
  - Bilingual search index: item searches match both English and localized Russian names simultaneously.
- **Reliable Cursor Management**: Hardware cursor automatically releases when the interface opens and restores camera control when closed.

---

## Default Controls

| Key / Action | Function |
|---|---|
| `J` or `F8` | Open or close the JEI Catalog |
| `R` (hovering over item) | Open Recipes for the hovered item |
| `U` (hovering over item) | Open Usages for the hovered item |
| `Mouse Wheel` (over recipe area) | Scroll through recipe cards |
| `Mouse Wheel` (over item grid) | Turn catalog pages |
| `Left Click` on Station name | Jump to that crafting station's recipes |
| `Left Click` on Ingredient / Result | Jump to that item |
| `Lang: RU/EN` (top right) | Toggle between English and Russian UI |
| `ESC` | Close the catalog |

---

## Installation

### Method 1: Automated Installer (Recommended)

1. Download the latest release archive (`Saleblazers.JEI-v1.0.0.zip`) from the [Releases](https://github.com/romrem30-eng/Saleblazers.JEI/releases) page.
2. Extract the archive into any folder.
3. Run `install.bat`.
   - The installer automatically detects your Saleblazers directory via Steam libraries.
   - If BepInEx 6 IL2CPP is not present, it will automatically download and set up BepInEx 6.
   - It will place `Saleblazers.JEI.dll` into your `BepInEx\plugins\` folder.
4. Launch the game through Steam.

### Method 2: Manual Installation

1. Install [BepInEx 6 Unity IL2CPP x64](https://github.com/BepInEx/BepInEx/releases) into your game folder:
   ```
   <SteamLibrary>\steamapps\common\Saleblazers\Default\
   ```
2. Launch the game once to allow BepInEx to generate folder structures and interop assemblies, then exit the game.
3. Place `Saleblazers.JEI.dll` into:
   ```
   <SteamLibrary>\steamapps\common\Saleblazers\Default\BepInEx\plugins\
   ```
4. Start Saleblazers.

---

## Building from Source

### Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or newer.
- An installed copy of **Saleblazers** with **BepInEx 6 IL2CPP** installed and run at least once (to produce the interop assemblies in `BepInEx/interop/`).

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/romrem30-eng/Saleblazers.JEI.git
   cd Saleblazers.JEI
   ```

2. Verify or update the game path in `Saleblazers.JEI.csproj` if your Steam library is located on a different drive:
   ```xml
   <GameDir>C:\Program Files (x86)\Steam\steamapps\common\Saleblazers\Default</GameDir>
   ```

3. Build the project:
   ```bash
   dotnet build -c Release
   ```

4. The compiled assembly `Saleblazers.JEI.dll` will be output to:
   - `bin\Release\Saleblazers.JEI.dll`
   - It will also automatically copy to your `BepInEx\plugins\` folder if the target path exists.

---

## Configuration

The configuration file is generated upon first launch at:
```
<GameDir>\BepInEx\config\com.saleblazers.jei.cfg
```

Configurable options:

```ini
[JEI]
## Enable the JEI catalog overlay.
# Setting type: Boolean
# Default value: true
EnableJEI = true

## Enable the TMI item spawner in the detail panel (host only).
# Setting type: Boolean
# Default value: true
EnableTMI = true

## Show base and crafted value tooltip over inventory slots.
# Setting type: Boolean
# Default value: true
EnablePriceTooltip = true

## Default batch amount for the second TMI spawn button.
# Setting type: Int32
# Default value: 10
SpawnCount = 10

## Active UI Language (RU or EN).
# Setting type: String
# Default value: RU
Language = RU

## Catalog grid columns (4 to 12).
# Setting type: Int32
# Default value: 8
GridCols = 8

## Catalog grid rows (3 to 8).
# Setting type: Int32
# Default value: 6
GridRows = 6
```

---

## Technical Architecture

Saleblazers is built on Unity IL2CPP with Mirror networking and Rewired input. Key architectural components of this mod include:

- `JeiCatalog.cs`: Scans `HRItemDatabase`, `HRCraftingDatabase`, `CraftingTableReferences`, and all loaded `HRSkillTree` / `HRSkillNode` hierarchies. Establishes reverse lookup maps for crafting stations and ingredients.
- `JeiUI.cs`: Procedural Unity UI canvas rendered on top of the game screen using native TextMeshPro SDF typography and custom raycast handling.
- `JeiLoc.cs`: Live language provider managing state and localized terminology without reloading assets.
- `CursorPatch.cs`: Harmony patches and frame synchronizers enforcing proper cursor unlock state in gameplay while preserving main menu stability.
- `ModHost.cs`: Injected IL2CPP MonoBehaviour ensuring guaranteed frame-rate ticks regardless of scene transitions.

---

## License

This project is licensed under the [MIT License](LICENSE). You are free to use, modify, and distribute this software in accordance with the license terms.
