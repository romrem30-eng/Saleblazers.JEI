using System;
using BepInEx.Configuration;

namespace Saleblazers.ModBase;

/// <summary>
/// Bilingual localization helper (Russian / English) for Saleblazers JEI.
/// Can be toggled live from the top bar button (Lang: RU/EN) and persists in BepInEx config.
/// </summary>
internal static class JeiLoc
{
    private static ConfigEntry<string> _langConfig;
    public static bool IsRu { get; private set; } = true;

    public static void Init(ConfigFile cfg)
    {
        _langConfig = cfg.Bind("JEI", "Language", "RU", "UI language: RU or EN.");
        string val = _langConfig.Value?.Trim().ToUpperInvariant() ?? "RU";
        IsRu = val != "EN";
    }

    public static void ToggleLanguage()
    {
        IsRu = !IsRu;
        if (_langConfig != null)
            _langConfig.Value = IsRu ? "RU" : "EN";
    }

    public static string Pick(string ru, string en) => IsRu ? ru : en;

    // --- Header & Mode Tabs ---
    public static string HeaderTitle => Pick(
        "JEI  •  КАТАЛОГ ПРЕДМЕТОВ И РЕЦЕПТОВ",
        "JEI  •  ITEM & RECIPE CATALOG");

    public static string LangButton => IsRu ? "Lang: RU" : "Lang: EN";

    public static string ModeCatalog => Pick("ПРЕДМЕТЫ", "ITEMS");
    public static string ModeAttributes => Pick("АТРИБУТЫ", "ATTRIBUTES");

    public static string SearchLabel => Pick("Поиск:", "Search:");

    public static string ItemCount(int count) => Pick(
        $"Предметов: {count}",
        $"Items: {count}");

    public static string AttrCount(int count) => Pick(
        $"Атрибутов: {count}",
        $"Attributes: {count}");

    // --- Item Category Filters ---
    public static string CatAll => Pick("ВСЁ", "ALL");
    public static string CatWeapons => Pick("ОРУЖИЕ", "WEAPONS");
    public static string CatArmor => Pick("БРОНЯ", "ARMOR");
    public static string CatFood => Pick("ЕДА", "FOOD");
    public static string CatMaterials => Pick("МАТЕРИАЛЫ", "MATERIALS");
    public static string CatBuilding => Pick("СТРОЙКА", "BUILDING");
    public static string CatStations => Pick("СТАНКИ", "STATIONS");
    public static string CatConsumables => Pick("РАСХОДНИКИ", "CONSUMABLES");

    // --- Attribute Category Filters ---
    public static string AttrCatAll => Pick("ВСЕ", "ALL");
    public static string AttrCatCombat => Pick("БОЙ", "COMBAT");
    public static string AttrCatDefense => Pick("ЗАЩИТА", "DEFENSE");
    public static string AttrCatFood => Pick("ЕДА", "FOOD");
    public static string AttrCatUtility => Pick("ПРОЧЕЕ", "UTILITY");

    // --- Bottom bar & Tabs ---
    public static string CloseButton => Pick("ЗАКРЫТЬ (J / ESC)", "CLOSE (J / ESC)");
    public static string SpawnOne => Pick("СПАВН x1", "SPAWN x1");
    public static string SpawnBatch(int count) => Pick($"СПАВН x{count}", $"SPAWN x{count}");

    public static string TabRecipes(int? count = null) => count.HasValue
        ? Pick($"РЕЦЕПТЫ (R) [{count.Value}]", $"RECIPES (R) [{count.Value}]")
        : Pick("РЕЦЕПТЫ (R)", "RECIPES (R)");

    public static string TabUsages(int? count = null) => count.HasValue
        ? Pick($"ГДЕ НУЖЕН (U) [{count.Value}]", $"USAGES (U) [{count.Value}]")
        : Pick("ГДЕ НУЖЕН (U)", "USAGES (U)");

    // --- Detail Panel Empty State ---
    public static string SelectItemPrompt => Pick(
        "<color=#9AA4B8>Выберите предмет в таблице слева</color>",
        "<color=#9AA4B8>Select an item from the grid on the left</color>");

    public static string SelectItemHint => Pick(
        "Нажмите на любую ячейку слева, чтобы увидеть цену, дерево изучения, станок и интерактивные рецепты.",
        "Click any cell on the left to view item price, research tree, crafting station, and interactive recipes.");

    public static string SelectAttrPrompt => Pick(
        "<color=#9AA4B8>Выберите атрибут в списке слева</color>",
        "<color=#9AA4B8>Select an attribute from the list on the left</color>");

    public static string SelectAttrHint => Pick(
        "Нажмите на атрибут, чтобы увидеть его точные параметры, описание эффекта и список связанных предметов и блюд.",
        "Click an attribute to view its detailed effect formula, perk stats, and associated meals or items.");

    public static string RecipesDefaultHeader => Pick("--- РЕЦЕПТЫ ---", "--- RECIPES ---");
    public static string AttrDefaultHeader => Pick("--- ЭФФЕКТ АТРИБУТА ---", "--- ATTRIBUTE EFFECT ---");

    // --- Detail Panel Populated ---
    public static string BasePriceLabel => Pick("База:", "Base:");
    public static string CraftedPriceLabel => Pick("Крафт:", "Crafted:");
    public static string DropsLabel => Pick("Добыча:", "Source:");
    public static string GoToItemButton(string name) => Pick($"К предм.: {name}", $"Go to: {name}");

    public static string HowToCraftHeader(int count) => Pick(
        $"<color=#F5D76E><b>КАК СКРАФТИТЬ ({count})</b></color>",
        $"<color=#F5D76E><b>HOW TO CRAFT ({count})</b></color>");

    public static string UsedInHeader(int count) => Pick(
        $"<color=#6EC6F5><b>ГДЕ ИСПОЛЬЗУЕТСЯ ({count})</b></color>",
        $"<color=#6EC6F5><b>USED IN RECIPES ({count})</b></color>");

    public static string EmptyRecipesText => Pick(
        "<color=#8892A6>Этот предмет не крафтится напрямую.</color>",
        "<color=#8892A6>This item is not crafted directly.</color>");

    public static string EmptyUsagesText => Pick(
        "<color=#8892A6>Не используется в рецептах.</color>",
        "<color=#8892A6>Not used in any recipes.</color>");

    // --- Recipe Cards & Cooking ---
    public static string StationLabel => Pick("Станок:", "Station:");
    public static string TimeLabel => Pick("Время:", "Time:");
    public static string SecondsUnit => Pick("сек.", "s");
    public static string NoIngredients => Pick("   <color=#8892A6>• Без ингредиентов</color>", "   <color=#8892A6>• No ingredients</color>");
    public static string CookingLabel => Pick("Кулинария:", "Cooking:");
    public static string VesselLabel => Pick("Посуда:", "Vessel:");
    public static string SatiationLabel => Pick("Сытость:", "Satiation:");
    public static string HydrationLabel => Pick("Жажда:", "Hydration:");
    public static string RegenLabel => Pick("Регенерация:", "Regen:");
    public static string AffixesLabel => Pick("Эффект:", "Perk/Affix:");

    // --- Attribute Explorer Details ---
    public static string AttrCategoryLabel => Pick("Категория:", "Category:");
    public static string AttrInternalIdLabel => Pick("ID в коде:", "Internal ID:");
    public static string AttrEffectHeader => Pick("ОПИСАНИЕ ЭФФЕКТА", "EFFECT DESCRIPTION");
    public static string AttrFoodHeader => Pick("ДАЁТСЯ БЛЮДАМИ:", "GRANTED BY MEALS:");
    public static string AttrNoDescription => Pick("Пассивный модификатор или особый статус игры.", "Passive modifier or special in-game status.");

    // --- Research / Unlock ---
    public static string ResearchNotRequired => Pick(
        "<color=#8892A6><b>Изучение:</b> Не требуется (базовый предмет)</color>",
        "<color=#8892A6><b>Research:</b> Not required (base item)</color>");

    public static string ResearchStartUnlocked => Pick(
        "<color=#7BE082><b>Изучение:</b> Открыто со старта</color>",
        "<color=#7BE082><b>Research:</b> Unlocked from start</color>");

    public static string ResearchLabel => Pick("Изучение:", "Research:");
    public static string StoryQuestUnlock => Pick("По сюжетному квесту", "Story Quest");
    public static string ResearchTableUnlock => Pick("Стол исследований", "Research Table");
    public static string CostLabel => Pick("Цена:", "Cost:");
    public static string PointsUnit => Pick("очк./$", "pts/$");
    public static string PointsShort => Pick("очк.", "pts");
    public static string AfterNodeLabel => Pick("После узла:", "After node:");
    public static string RequiresPrefix => Pick("Нужно: ", "Requires: ");
    public static string MustResearchOrCraftPrefix => Pick("Нужно изучить/скрафтить: ", "Must research/craft: ");
    public static string CompactStartUnlocked => Pick("   <color=#7BE082>Открыто со старта</color>", "   <color=#7BE082>Unlocked from start</color>");

    // --- Notifications ---
    public static string NotifyLangSwitched => Pick(
        "Язык интерфейса JEI: Русский (RU)",
        "JEI Language: English (EN)");

    public static string NotifyTmiDisabled => Pick(
        "TMI отключён в конфиге.",
        "TMI spawner is disabled in config.");

    public static string NotifyHostOnly => Pick(
        "Спавн доступен только хосту.",
        "Item spawning is available to the host only.");

    public static string NotifySpawnedInv(int count, string item) => Pick(
        $"Выдано: {count}x {item}",
        $"Spawned: {count}x {item}");

    public static string NotifySpawnedMixed(int invCount, int feetCount, string item) => Pick(
        $"В инвентарь: {invCount}x, у ног: {feetCount}x ({item})",
        $"In inventory: {invCount}x, at feet: {feetCount}x ({item})");

    public static string NotifySpawnedFeet(int feetCount, string item) => Pick(
        $"Создано у ног: {feetCount}x {item}",
        $"Spawned at feet: {feetCount}x {item}");

    public static string NotifySpawnError => Pick(
        "Ошибка при спавне предмета.",
        "Failed to spawn item.");
}
