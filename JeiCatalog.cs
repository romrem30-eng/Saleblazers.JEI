using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using TMPro;
using UnityEngine;

namespace Saleblazers.ModBase;

public enum JeiCategoryFilter
{
    All,
    Weapons,
    Armor,
    Food,
    Materials,
    Building,
    Stations,
    Consumables
}

public class JeiRecipe
{
    public int ResultItemID;
    public int NumToCraft;
    public float TimeToCraft;
    public int StationFlag;
    public int StationItemID;
    public string StationName;
    public List<KeyValuePair<int, int>> Ingredients = new(); // itemID, amount

    // Culinary / Food extensions
    public bool IsCooking;
    public string VesselType;
    public float SatiatingSeconds;
    public float HydratingSeconds;
    public float RegenPercent;
    public List<int> FoodAffixIDs = new();
}

public class JeiUnlockInfo
{
    public bool StartUnlocked;
    public bool QuestUnlocked;
    public string NodeNameRu;
    public string NodeNameEn;
    public int Cost;
    public int SkillPoints;
    public string ParentNodeNameRu;
    public string ParentNodeNameEn;
    public string RequirementTextRu;
    public string RequirementTextEn;
    public int RequiredItemID;
    public int RequiredItemCount;

    public string NodeName => JeiLoc.IsRu
        ? (!string.IsNullOrEmpty(NodeNameRu) ? NodeNameRu : NodeNameEn)
        : (!string.IsNullOrEmpty(NodeNameEn) ? NodeNameEn : NodeNameRu);

    public string ParentNodeName => JeiLoc.IsRu
        ? (!string.IsNullOrEmpty(ParentNodeNameRu) ? ParentNodeNameRu : ParentNodeNameEn)
        : (!string.IsNullOrEmpty(ParentNodeNameEn) ? ParentNodeNameEn : ParentNodeNameRu);

    public string RequirementText => JeiLoc.IsRu
        ? (!string.IsNullOrEmpty(RequirementTextRu) ? RequirementTextRu : RequirementTextEn)
        : (!string.IsNullOrEmpty(RequirementTextEn) ? RequirementTextEn : RequirementTextRu);
}

public class JeiAttributeEntry
{
    public int ID;
    public string StringID;
    public string TitleRu;
    public string TitleEn;
    public Color TitleColor = Color.white;
    public string DescriptionRu;
    public string DescriptionEn;
    public Sprite Icon;
    public string Category = "Utility"; // Combat, Defense, Food, Utility

    // Value ranges & mechanics
    public float MinValue;
    public float MaxValue;
    public string ValueRangeText;
    public float Duration;
    public string DurationTextRu;
    public string DurationTextEn;
    public string TriggerTextRu;
    public string TriggerTextEn;
    public string AppliesToRu;
    public string AppliesToEn;
    public string MinRarity;
    public string SourceNameRu;
    public string SourceNameEn;
    private string _sourceNameFallback;
    public string RequirementsText;

    // Associated items: meals, ingredients, weapons/gear
    public List<int> AssociatedFoodItemIDs = new();
    public List<int> AssociatedIngredientItemIDs = new();
    public List<int> AssociatedGearItemIDs = new();

    public string LocalizedCategory => Category switch
    {
        "Combat" => JeiLoc.AttrCatCombat,
        "Defense" => JeiLoc.AttrCatDefense,
        "Food" => JeiLoc.AttrCatFood,
        _ => JeiLoc.AttrCatUtility
    };

    public string SourceName
    {
        get
        {
            if (JeiLoc.IsRu)
            {
                if (!string.IsNullOrEmpty(SourceNameRu)) return SourceNameRu;
                if (!string.IsNullOrEmpty(SourceNameEn)) return SourceNameEn;
            }
            else
            {
                if (!string.IsNullOrEmpty(SourceNameEn)) return SourceNameEn;
                if (!string.IsNullOrEmpty(SourceNameRu)) return SourceNameRu;
            }
            return _sourceNameFallback;
        }
        set
        {
            _sourceNameFallback = value;
        }
    }

    public string Title
    {
        get
        {
            if (JeiLoc.IsRu)
            {
                if (!string.IsNullOrEmpty(TitleRu)) return TitleRu;
                if (!string.IsNullOrEmpty(TitleEn)) return TitleEn;
            }
            else
            {
                if (!string.IsNullOrEmpty(TitleEn) && !JeiCatalog.HasCyrillic(TitleEn)) return TitleEn;
                if (!string.IsNullOrEmpty(StringID)) return JeiCatalog.CleanAttributeName(StringID);
                if (!string.IsNullOrEmpty(TitleEn)) return TitleEn;
                if (!string.IsNullOrEmpty(TitleRu)) return TitleRu;
            }
            return !string.IsNullOrEmpty(StringID) ? StringID : ("#" + ID);
        }
    }

    public string Description
    {
        get
        {
            if (JeiLoc.IsRu)
            {
                if (!string.IsNullOrEmpty(DescriptionRu)) return DescriptionRu;
                if (!string.IsNullOrEmpty(DescriptionEn)) return DescriptionEn;
            }
            else
            {
                if (!string.IsNullOrEmpty(DescriptionEn) && !JeiCatalog.HasCyrillic(DescriptionEn)) return DescriptionEn;
                if (!string.IsNullOrEmpty(DescriptionEn)) return DescriptionEn;
                if (!string.IsNullOrEmpty(DescriptionRu)) return DescriptionRu;
            }
            return JeiLoc.AttrNoDescription;
        }
    }

    public string TriggerText => JeiLoc.IsRu
        ? (!string.IsNullOrEmpty(TriggerTextRu) ? TriggerTextRu : TriggerTextEn)
        : (!string.IsNullOrEmpty(TriggerTextEn) ? TriggerTextEn : TriggerTextRu);

    public string AppliesTo => JeiLoc.IsRu
        ? (!string.IsNullOrEmpty(AppliesToRu) ? AppliesToRu : AppliesToEn)
        : (!string.IsNullOrEmpty(AppliesToEn) ? AppliesToEn : AppliesToRu);

    public string DurationText => JeiLoc.IsRu
        ? (!string.IsNullOrEmpty(DurationTextRu) ? DurationTextRu : DurationTextEn)
        : (!string.IsNullOrEmpty(DurationTextEn) ? DurationTextEn : DurationTextRu);
}

public class JeiItemDropSource
{
    public string GroupKey;
    public string NameRu;
    public string NameEn;
    public float MinChance;
    public float MaxChance;
    public int MinAmount;
    public int MaxAmount;
    public bool IsBoss;

    public string Format(bool ru)
    {
        string name = ru ? NameRu : NameEn;
        string chanceText;
        if (Mathf.Approximately(MinChance, MaxChance) || MaxChance <= MinChance)
        {
            float c = MaxChance * 100f;
            chanceText = c >= 99.5f ? "100%" : (c < 1f ? $"{c:0.#}%" : $"{Mathf.RoundToInt(c)}%");
        }
        else
        {
            float c1 = MinChance * 100f;
            float c2 = MaxChance * 100f;
            chanceText = $"{Mathf.RoundToInt(c1)}-{Mathf.RoundToInt(c2)}%";
        }

        string amtText = "";
        if (MinAmount > 0 || MaxAmount > 0)
        {
            if (MinAmount == MaxAmount || MaxAmount <= 0) amtText = $"{MinAmount}x";
            else if (MinAmount <= 0) amtText = $"{MaxAmount}x";
            else amtText = $"{MinAmount}-{MaxAmount}x";
        }

        if (!string.IsNullOrEmpty(amtText))
            return $"{name} <color=#F5D76E>({chanceText}, {amtText})</color>";
        return $"{name} <color=#F5D76E>({chanceText})</color>";
    }
}

public class JeiItemEntry
{
    public int ItemID;
    public string Name;
    public string FallbackName;
    public float BaseValue;
    public float CraftedValue;
    public bool IsFood;
    public List<string> Categories = new();
    public List<JeiRecipe> Recipes = new();  // R: produces this
    public List<JeiRecipe> Usages = new();   // U: uses this as ingredient (or crafted at this station)
    public List<JeiItemDropSource> DropSources = new();
    public List<string> RawWorldDrops = new(); // raw world source keys (from CSV)
    public JeiUnlockInfo Unlock;             // research / unlock info
    internal HRItemDatabase.HRItemData Source;
    private Sprite _icon;
    private bool _iconLoaded;

    public string DisplayName
    {
        get
        {
            if (JeiLoc.IsRu)
            {
                if (!string.IsNullOrEmpty(Name)) return Name;
                if (!string.IsNullOrEmpty(FallbackName)) return FallbackName;
            }
            else
            {
                if (!string.IsNullOrEmpty(FallbackName)) return FallbackName;
                if (!string.IsNullOrEmpty(Name)) return Name;
            }
            return "#" + ItemID;
        }
    }

    public List<string> GetFormattedDropSources(int maxCount = 3)
    {
        if (DropSources.Count > 0)
        {
            var sorted = DropSources
                .OrderByDescending(d => d.IsBoss)
                .ThenByDescending(d => d.MaxChance)
                .ToList();

            var res = new List<string>();
            int count = Mathf.Min(sorted.Count, maxCount);
            for (int i = 0; i < count; i++)
            {
                res.Add(sorted[i].Format(JeiLoc.IsRu));
            }

            if (sorted.Count > maxCount)
            {
                int remaining = sorted.Count - maxCount;
                res.Add(JeiLoc.IsRu ? $"<color=#8E9AA8>+ ещё {remaining}</color>" : $"<color=#8E9AA8>+ {remaining} more</color>");
            }

            return res;
        }

        if (RawWorldDrops.Count > 0)
        {
            var list = new List<string>(RawWorldDrops.Count);
            for (int i = 0; i < RawWorldDrops.Count; i++)
            {
                string loc = JeiCatalog.BeautifyDropSource(RawWorldDrops[i], JeiLoc.IsRu);
                if (!string.IsNullOrEmpty(loc) && !list.Contains(loc))
                    list.Add(loc);
            }
            return list;
        }

        return new List<string>();
    }

    public List<string> GetLocalizedWorldDrops() => GetFormattedDropSources(3);

    public bool MatchesCategory(JeiCategoryFilter filter)
    {
        if (filter == JeiCategoryFilter.All) return true;

        string nameLower = (FallbackName ?? "").ToLowerInvariant();

        switch (filter)
        {
            case JeiCategoryFilter.Weapons:
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("weapon") || cl.Contains("melee") || cl.Contains("ranged") || cl.Contains("gun") || cl.Contains("bow") || cl.Contains("sword"))
                        return true;
                }
                return nameLower.Contains("sword") || nameLower.Contains("bow") || nameLower.Contains("gun") || nameLower.Contains("rifle") || nameLower.Contains("knife") || nameLower.Contains("staff") || nameLower.Contains("wand");

            case JeiCategoryFilter.Armor:
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("armor") || cl.Contains("clothing") || cl.Contains("helmet") || cl.Contains("hat") || cl.Contains("chest") || cl.Contains("pants") || cl.Contains("boots") || cl.Contains("shield") || cl.Contains("backpack"))
                        return true;
                }
                return nameLower.Contains("helmet") || nameLower.Contains("hat") || nameLower.Contains("shirt") || nameLower.Contains("jacket") || nameLower.Contains("pants") || nameLower.Contains("boots") || nameLower.Contains("shield");

            case JeiCategoryFilter.Food:
                if (IsFood) return true;
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("food") || cl.Contains("ingredient") || cl.Contains("drink") || cl.Contains("meal") || cl.Contains("crop") || cl.Contains("produce") || cl.Contains("fish") || cl.Contains("meat"))
                        return true;
                }
                return nameLower.Contains("soup") || nameLower.Contains("stew") || nameLower.Contains("sushi") || nameLower.Contains("bread") || nameLower.Contains("pie") || nameLower.Contains("meat") || nameLower.Contains("fish") || nameLower.Contains("apple");

            case JeiCategoryFilter.Materials:
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("material") || cl.Contains("resource") || cl.Contains("ore") || cl.Contains("ingot") || cl.Contains("bar") || cl.Contains("wood") || cl.Contains("stone") || cl.Contains("cloth") || cl.Contains("leather") || cl.Contains("plank"))
                        return true;
                }
                return nameLower.Contains("ore") || nameLower.Contains("ingot") || nameLower.Contains("bar") || nameLower.Contains("wood") || nameLower.Contains("stone") || nameLower.Contains("scrap");

            case JeiCategoryFilter.Building:
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("building") || cl.Contains("structure") || cl.Contains("wall") || cl.Contains("floor") || cl.Contains("roof") || cl.Contains("furniture") || cl.Contains("door") || cl.Contains("window") || cl.Contains("decor") || cl.Contains("light"))
                        return true;
                }
                return nameLower.Contains("wall") || nameLower.Contains("floor") || nameLower.Contains("roof") || nameLower.Contains("door") || nameLower.Contains("chair") || nameLower.Contains("table") || nameLower.Contains("lamp") || nameLower.Contains("shelf");

            case JeiCategoryFilter.Stations:
                if (JeiCatalog.IsStationItem(ItemID)) return true;
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("crafting") || cl.Contains("station") || cl.Contains("bench") || cl.Contains("anvil") || cl.Contains("furnace") || cl.Contains("stove") || cl.Contains("forge") || cl.Contains("kiln") || cl.Contains("cauldron"))
                        return true;
                }
                return nameLower.Contains("workbench") || nameLower.Contains("crafting table") || nameLower.Contains("anvil") || nameLower.Contains("furnace") || nameLower.Contains("stove") || nameLower.Contains("cauldron");

            case JeiCategoryFilter.Consumables:
                foreach (var c in Categories)
                {
                    string cl = c.ToLowerInvariant();
                    if (cl.Contains("consumable") || cl.Contains("potion") || cl.Contains("medicine") || cl.Contains("ammo") || cl.Contains("bullet") || cl.Contains("arrow") || cl.Contains("grenade") || cl.Contains("bandage"))
                        return true;
                }
                return nameLower.Contains("potion") || nameLower.Contains("bullet") || nameLower.Contains("ammo") || nameLower.Contains("arrow") || nameLower.Contains("bandage") || nameLower.Contains("medkit");

            default:
                return true;
        }
    }

    public Sprite Icon
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                try { _icon = Source?.ItemSprite; } catch (Exception) { }
            }
            return _icon;
        }
    }
}

/// <summary>
/// JEI catalog: scans HRItemDatabase, HRCraftingDatabase, HRSkillTree, HRFoodDatabase, and HRAttributeDatabase.
/// Builds comprehensive items, culinary recipes, workstation links, research unlock info, and attribute codex.
/// </summary>
internal static class JeiCatalog
{
    private static ManualLogSource _log;
    private static TMP_FontAsset _font;
    private static bool _scanned;
    private static readonly Dictionary<int, JeiItemEntry> _items = new();
    private static readonly List<JeiAttributeEntry> _attributes = new();
    private static readonly Dictionary<int, JeiAttributeEntry> _attributesById = new();
    private static readonly Dictionary<int, List<JeiItemDropSource>> _itemDropSources = new();
    private static readonly Dictionary<int, int> _stationItemIds = new();
    private static readonly HashSet<int> _allStationItemIds = new();

    public static TMP_FontAsset Font => _font != null ? _font : (_font = LoadFont());
    public static bool Scanned => _scanned;
    public static IReadOnlyDictionary<int, JeiItemEntry> Items => _items;
    public static IReadOnlyList<JeiAttributeEntry> Attributes => _attributes;
    public static IReadOnlyDictionary<int, JeiAttributeEntry> AttributesById => _attributesById;

    public static bool IsStationItem(int itemId) => _allStationItemIds.Contains(itemId);

    public static void Init(ManualLogSource log)
    {
        _log = log;
        ParseAllDropCsvs();
    }

    public static void EnsureScanned()
    {
        if (_scanned && _items.Count > 0) return;
        try
        {
            if (Scan() && _items.Count > 0) _scanned = true;
        }
        catch (Exception e)
        {
            _log?.LogWarning($"[JEI] Scan failed: {e.Message}");
        }
    }

    private static bool Scan()
    {
        var gm = ModService.GameManager;
        var itemDb = gm?.MasterItemDB;
        var rb = UnityEngine.Object.FindObjectOfType<HRRecipeBookSystem>();
        var craftingDb = rb?.MasterCraftingDB;
        if (craftingDb != null && itemDb == null) itemDb = craftingDb.MasterItemDB;
        if (itemDb == null && HRConsoleCommands.Get != null) itemDb = HRConsoleCommands.Get.MasterItemDB;

        if (itemDb == null)
        {
            try
            {
                var dbs = Resources.FindObjectsOfTypeAll<HRItemDatabase>();
                if (dbs != null && dbs.Length > 0) itemDb = dbs[0];
            }
            catch (Exception) { }
        }

        if (craftingDb == null)
        {
            try
            {
                var cdbs = Resources.FindObjectsOfTypeAll<HRCraftingDatabase>();
                if (cdbs != null && cdbs.Length > 0) craftingDb = cdbs[0];
            }
            catch (Exception) { }
        }

        _log?.LogInfo($"[JEI] Scan: itemDb={(itemDb != null)}, craftDb={(craftingDb != null)}, items={(itemDb?.ItemArray != null ? itemDb.ItemArray.Length : 0)}, recipes={(craftingDb?.CraftableItems != null ? craftingDb.CraftableItems.Length : 0)}");
        if (itemDb == null && craftingDb == null) return false;

        _items.Clear();
        _stationItemIds.Clear();
        _allStationItemIds.Clear();
        _attributes.Clear();
        _attributesById.Clear();

        // 1. Items from HRItemDatabase
        if (itemDb?.ItemArray != null)
        {
            foreach (var it in itemDb.ItemArray)
            {
                if (it == null || it.ItemID <= 0) continue;
                var e = new JeiItemEntry
                {
                    ItemID = it.ItemID,
                    FallbackName = it.ItemName,
                    BaseValue = it.Data.ItemValue,
                    CraftedValue = it.Data.CraftedItemValue,
                    Source = it,
                };
                if (it.CategoryList != null)
                    foreach (var cat in it.CategoryList)
                        if (cat?.CategoryName != null && !e.Categories.Contains(cat.CategoryName))
                            e.Categories.Add(cat.CategoryName);
                if (_itemDropSources.TryGetValue(it.ItemID, out var dList))
                {
                    e.DropSources.AddRange(dList);
                    foreach (var ds in dList)
                        if (!e.RawWorldDrops.Contains(ds.GroupKey)) e.RawWorldDrops.Add(ds.GroupKey);
                }
                _items[it.ItemID] = e;
            }
        }

        // Localized item names
        foreach (var e in _items.Values)
        {
            e.Name = HRItemDatabase.GetLocalizedItemNameByID(e.ItemID, e.FallbackName);
        }

        // 2. Station ItemIDs from CraftingTableReferences
        if (craftingDb?.CraftingTableReferences != null)
        {
            foreach (var tr in craftingDb.CraftingTableReferences)
            {
                if (tr == null || tr.ItemSO == null) continue;
                try
                {
                    int stnItemId = tr.ItemSO.GetItemId();
                    int flagVal = (int)tr.Flag;
                    if (stnItemId > 0)
                    {
                        _stationItemIds[flagVal] = stnItemId;
                        _allStationItemIds.Add(stnItemId);
                    }
                }
                catch (Exception) { }
            }
        }
        PopulateFallbackStationIds();

        // 3. Recipes (R) + Usages (U) from HRCraftingDatabase
        if (craftingDb?.CraftableItems != null)
        {
            foreach (var ci in craftingDb.CraftableItems)
            {
                if (ci == null || ci.ResultItemID <= 0) continue;

                if (_items.TryGetValue(ci.ResultItemID, out var targetItem))
                {
                    targetItem.Unlock ??= new JeiUnlockInfo();
                    if (ci.bStartUnlocked) targetItem.Unlock.StartUnlocked = true;
                    if (ci.bQuestUnlocked) targetItem.Unlock.QuestUnlocked = true;
                    if (ci.OverridePrice > 0 && targetItem.Unlock.Cost <= 0)
                        targetItem.Unlock.Cost = ci.OverridePrice;
                }

                if (ci.UnlockIDs != null)
                {
                    foreach (int unlockedId in ci.UnlockIDs)
                    {
                        if (unlockedId > 0 && unlockedId != ci.ResultItemID && _items.TryGetValue(unlockedId, out var unlockedEntry))
                        {
                            unlockedEntry.Unlock ??= new JeiUnlockInfo();
                            if (unlockedEntry.Unlock.RequiredItemID <= 0)
                            {
                                unlockedEntry.Unlock.RequiredItemID = ci.ResultItemID;
                                unlockedEntry.Unlock.RequiredItemCount = 1;
                            }
                        }
                    }
                }

                if (ci.Recipes == null) continue;
                foreach (var rc in ci.Recipes)
                {
                    if (rc == null) continue;
                    int flag = (int)rc.OverrideFlag != 0 ? (int)rc.OverrideFlag : (int)ci.Flag;
                    int stnItemId = ResolveStationItemId(flag);
                    var r = new JeiRecipe
                    {
                        ResultItemID = ci.ResultItemID,
                        NumToCraft = Mathf.Max(1, rc.NumToCraft),
                        TimeToCraft = rc.TimeToCraft,
                        StationFlag = flag,
                        StationItemID = stnItemId,
                        StationName = ResolveStationName(flag),
                    };
                    if (rc.Ingredients != null)
                        foreach (var ing in rc.Ingredients)
                            if (ing != null && ing.ItemID > 0)
                                r.Ingredients.Add(new KeyValuePair<int, int>(ing.ItemID, Mathf.Max(1, ing.AmountRequired)));

                    if (_items.TryGetValue(r.ResultItemID, out var target)) target.Recipes.Add(r);
                    foreach (var ing in r.Ingredients)
                        if (_items.TryGetValue(ing.Key, out var use) && !use.Usages.Contains(r))
                            use.Usages.Add(r);

                    if (stnItemId > 0 && _items.TryGetValue(stnItemId, out var stnItem) && !stnItem.Usages.Contains(r))
                    {
                        stnItem.Usages.Add(r);
                    }
                }
            }
        }

        // 4. Scan Research / Skill Trees
        ScanSkillTrees(craftingDb?.ResearchBenchTreeSO);

        // 5. Scan Item Attributes & Affixes from HRAttributeDatabase & HRAffixRegistry
        ScanAttributeDatabase();

        // 6. Scan Food & Culinary Recipes from HRFoodDatabase
        ScanFoodDatabase();

        _scanned = true;
        _log?.LogInfo($"[JEI] Catalog: {_items.Count} items, {_attributes.Count} attributes, R/U & Culinary indexes built.");
        return true;
    }

    private static void ScanFoodDatabase()
    {
        try
        {
            HRFoodDatabase.EnsureLoaded();

            // Mark known food ingredients, consumables, and vessels
            if (HRFoodDatabase.IngredientsByItemID != null)
            {
                foreach (var kv in HRFoodDatabase.IngredientsByItemID)
                {
                    int itemId = kv.Key;
                    if (_items.TryGetValue(itemId, out var entry))
                        entry.IsFood = true;

                    // Also link ingredient's AffixID to attribute entries
                    var ingData = kv.Value;
                    if (ingData != null && ingData.AffixID > 0)
                    {
                        if (_attributesById.TryGetValue(ingData.AffixID, out var attrEntry))
                        {
                            if (!attrEntry.AssociatedIngredientItemIDs.Contains(itemId))
                                attrEntry.AssociatedIngredientItemIDs.Add(itemId);
                            if (!attrEntry.AssociatedFoodItemIDs.Contains(itemId))
                                attrEntry.AssociatedFoodItemIDs.Add(itemId);
                        }
                    }
                }
            }

            if (HRFoodDatabase.ConsumablesByItemID != null)
            {
                foreach (var kv in HRFoodDatabase.ConsumablesByItemID)
                {
                    int itemId = kv.Key;
                    if (_items.TryGetValue(itemId, out var entry))
                        entry.IsFood = true;
                }
            }

            // Map ingredient NameKey -> ItemID
            var nameKeyToItemId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (HRFoodDatabase.IngredientsByItemID != null)
            {
                foreach (var kv in HRFoodDatabase.IngredientsByItemID)
                {
                    var ingData = kv.Value;
                    if (ingData != null && !string.IsNullOrEmpty(ingData.NameKey))
                    {
                        if (!nameKeyToItemId.ContainsKey(ingData.NameKey))
                            nameKeyToItemId[ingData.NameKey] = kv.Key;
                    }
                }
            }

            // Process all Signature Recipes & Family Recipes
            var recipesToProcess = new List<HRFoodSignatureRecipeData>();
            if (HRFoodDatabase.SignatureRecipes != null)
            {
                foreach (var r in HRFoodDatabase.SignatureRecipes)
                    if (r != null && !recipesToProcess.Contains(r)) recipesToProcess.Add(r);
            }
            if (HRFoodDatabase.FamilyRecipes != null)
            {
                foreach (var r in HRFoodDatabase.FamilyRecipes)
                    if (r != null && !recipesToProcess.Contains(r)) recipesToProcess.Add(r);
            }

            _log?.LogInfo($"[JEI] Processing {recipesToProcess.Count} signature food recipes...");

            foreach (var sig in recipesToProcess)
            {
                int mealItemId = sig.VisualItemID;
                if (mealItemId <= 0)
                {
                    try { mealItemId = HRFoodDatabase.ResolveMealVisualItemID(sig.ID, null); }
                    catch (Exception) { }
                }

                if (mealItemId <= 0 || !_items.TryGetValue(mealItemId, out var mealEntry))
                    continue;

                mealEntry.IsFood = true;

                int stnFlag = sig.StationCraftingFlags;
                int stnItemId = ResolveStationItemId(stnFlag);
                string stnName = ResolveStationName(stnFlag);

                var r = new JeiRecipe
                {
                    ResultItemID = mealItemId,
                    NumToCraft = 1,
                    TimeToCraft = 3.0f,
                    StationFlag = stnFlag,
                    StationItemID = stnItemId,
                    StationName = stnName,
                    IsCooking = true,
                    VesselType = sig.VesselType,
                    SatiatingSeconds = sig.SatiatingSeconds,
                    HydratingSeconds = sig.HydratingSeconds,
                    RegenPercent = sig.RegenPercent
                };

                if (sig.Affixes != null)
                {
                    foreach (var aff in sig.Affixes)
                    {
                        if (aff == null) continue;
                        int affId = aff.AffixID;
                        if (affId > 0)
                        {
                            if (!r.FoodAffixIDs.Contains(affId))
                                r.FoodAffixIDs.Add(affId);

                            if (_attributesById.TryGetValue(affId, out var attrEntry))
                            {
                                if (!attrEntry.AssociatedFoodItemIDs.Contains(mealItemId))
                                    attrEntry.AssociatedFoodItemIDs.Add(mealItemId);
                            }
                        }
                    }
                }

                // Parse ingredients
                if (sig.Ingredients != null)
                {
                    foreach (var ing in sig.Ingredients)
                    {
                        if (ing == null || string.IsNullOrEmpty(ing.NameKey)) continue;
                        int count = Mathf.Max(1, ing.Count);

                        int ingItemId = 0;
                        if (nameKeyToItemId.TryGetValue(ing.NameKey, out int directId))
                        {
                            ingItemId = directId;
                        }
                        else
                        {
                            foreach (var it in _items.Values)
                            {
                                if (it.FallbackName != null && it.FallbackName.IndexOf(ing.NameKey, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    ingItemId = it.ItemID;
                                    break;
                                }
                            }
                        }

                        if (ingItemId > 0)
                        {
                            r.Ingredients.Add(new KeyValuePair<int, int>(ingItemId, count));
                            if (_items.TryGetValue(ingItemId, out var ingItemEntry))
                            {
                                ingItemEntry.IsFood = true;
                                if (!ingItemEntry.Usages.Contains(r))
                                    ingItemEntry.Usages.Add(r);

                                // If ingredient grants an affix, link this cooked meal to that affix as well!
                                if (HRFoodDatabase.IngredientsByItemID != null &&
                                    HRFoodDatabase.IngredientsByItemID.TryGetValue(ingItemId, out var ingData) &&
                                    ingData.AffixID > 0)
                                {
                                    if (_attributesById.TryGetValue(ingData.AffixID, out var attrEntry))
                                    {
                                        if (!attrEntry.AssociatedFoodItemIDs.Contains(mealItemId))
                                            attrEntry.AssociatedFoodItemIDs.Add(mealItemId);
                                        if (!r.FoodAffixIDs.Contains(ingData.AffixID))
                                            r.FoodAffixIDs.Add(ingData.AffixID);
                                    }
                                }
                            }
                        }
                    }
                }

                // Add recipe to the meal item
                if (!mealEntry.Recipes.Contains(r))
                    mealEntry.Recipes.Add(r);

                // Add to cooking station usages
                if (stnItemId > 0 && _items.TryGetValue(stnItemId, out var stnEntry))
                {
                    if (!stnEntry.Usages.Contains(r))
                        stnEntry.Usages.Add(r);
                }
            }
        }
        catch (Exception e)
        {
            _log?.LogWarning($"[JEI] ScanFoodDatabase error: {e.Message}");
        }
    }

    internal static bool HasCyrillic(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++)
            if (s[i] >= '\u0400' && s[i] <= '\u04FF') return true;
        return false;
    }

    // --- Unique Game Attributes Knowledge Base (Detailed mechanics for non-numeric & boss traits) ---
    private static readonly Dictionary<string, (string titleRu, string titleEn, string descRu, string descEn, string appliesRu, string appliesEn, string sourceRu, string sourceEn)> _uniqueAttributeKnowledge = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BellstalkerBlessing"] = (
            "Благословение Беллсталкера", "Bellstalker's Blessing",
            "Древняя защита от ночных хищников. Снижает агрессию ночных сталкеров и увеличивает ценность редкой добычи при ночной охоте.",
            "Ancient protection against nocturnal stalkers. Decreases night predator aggression and increases rare loot drops during night hunts.",
            "Персонаж (пассивно)", "Player Character (passive)",
            "Победа над Беллсталкером / Ночной алтарь", "Defeating Bellstalker / Night Shrine"
        ),
        ["HRBellstalkerBlessingAttribute"] = (
            "Благословение Беллсталкера", "Bellstalker's Blessing",
            "Древняя защита от ночных хищников. Снижает агрессию ночных сталкеров и увеличивает ценность редкой добычи при ночной охоте.",
            "Ancient protection against nocturnal stalkers. Decreases night predator aggression and increases rare loot drops during night hunts.",
            "Персонаж (пассивно)", "Player Character (passive)",
            "Победа над Беллсталкером / Ночной алтарь", "Defeating Bellstalker / Night Shrine"
        ),
        ["Благословение Беллсталкера"] = (
            "Благословение Беллсталкера", "Bellstalker's Blessing",
            "Древняя защита от ночных хищников. Снижает агрессию ночных сталкеров и увеличивает ценность редкой добычи при ночной охоте.",
            "Ancient protection against nocturnal stalkers. Decreases night predator aggression and increases rare loot drops during night hunts.",
            "Персонаж (пассивно)", "Player Character (passive)",
            "Победа над Беллсталкером / Ночной алтарь", "Defeating Bellstalker / Night Shrine"
        ),
        ["BarterDifficultyModifier"] = (
            "Мастерство торга", "Barter Mastery",
            "Изменяет терпение покупателей и сложность торговли в магазине, повышая шансы на успешную сделку по максимальной цене.",
            "Modifies customer patience and negotiation difficulty in the shop, improving chances for maximum sale profit.",
            "Торговля и магазин", "Shop & Bartering",
            "Торговые навыки / Перки харизмы", "Trading Skills / Charisma Perks"
        ),
        ["Haggling"] = (
            "Мастерство торга", "Barter Mastery",
            "Изменяет терпение покупателей и сложность торговли в магазине, повышая шансы на успешную сделку по максимальной цене.",
            "Modifies customer patience and negotiation difficulty in the shop, improving chances for maximum sale profit.",
            "Торговля и магазин", "Shop & Bartering",
            "Торговые навыки / Перки харизмы", "Trading Skills / Charisma Perks"
        ),
        ["HRHagglingAffix"] = (
            "Мастерство торга", "Barter Mastery",
            "Изменяет терпение покупателей и сложность торговли в магазине, повышая шансы на успешную сделку по максимальной цене.",
            "Modifies customer patience and negotiation difficulty in the shop, improving chances for maximum sale profit.",
            "Торговля и магазин", "Shop & Bartering",
            "Торговые навыки / Перки харизмы", "Trading Skills / Charisma Perks"
        ),
        ["Мастерство торга"] = (
            "Мастерство торга", "Barter Mastery",
            "Изменяет терпение покупателей и сложность торговли в магазине, повышая шансы на успешную сделку по максимальной цене.",
            "Modifies customer patience and negotiation difficulty in the shop, improving chances for maximum sale profit.",
            "Торговля и магазин", "Shop & Bartering",
            "Торговые навыки / Перки харизмы", "Trading Skills / Charisma Perks"
        ),
        ["CharacterOriginSpecial"] = (
            "Черта происхождения", "Origin Trait",
            "Уникальный стартовый навык или пассивная черта, выбранная при создании персонажа (происхождение героя).",
            "Unique starting background trait chosen during character creation that permanently shapes hero abilities.",
            "Персонаж (постоянно)", "Player Character (permanent)",
            "Выбор предыстории персонажа", "Character Origin Selection"
        ),
        ["ConsumableHealingModifier"] = (
            "Эффективность медицины", "Medicine Potency",
            "Увеличивает количество здоровья, восстанавливаемое бинтами, лечебными зельями и аптечками.",
            "Increases the amount of health restored from bandages, healing potions, and first-aid kits.",
            "Медицина и расходники", "Medicine & Consumables",
            "Медицинские перки / Оснащение", "Medical Perks / Gear"
        ),
        ["Dirty"] = (
            "Грязь", "Covered in Dirt",
            "Персонаж испачкан. Снижает скорость передвижения и гигиену, ускоряя обезвоживание. Смывается в воде.",
            "Character is covered in mud/dirt. Reduces movement speed and hygiene while increasing dehydration rate. Wash in water.",
            "Персонаж (дебафф)", "Player Character (debuff)",
            "Грязевые ямы / Болота", "Mud pits / Swamp biomes"
        ),
        ["FreezeDeath"] = (
            "Смертельное обморожение", "Lethal Hypothermia",
            "Критическое переохлаждение. Наносит непрерывный смертельный урон при падении температуры тела до нуля.",
            "Extreme lethal hypothermia. Inflicts severe damage over time when core body warmth drops to absolute zero.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Ледяные биомы / Бураны", "Freezing Biomes / Blizzards"
        ),
        ["Frostbite"] = (
            "Обморожение", "Frostbite",
            "Холод сковывает конечности. Снижает скорость атаки и восстановления выносливости в ледяных биомах.",
            "Biting cold stiffens muscles. Slows attack speed and stamina recovery in freezing environments.",
            "Персонаж (негативный статус)", "Player Character (debuff)",
            "Холодный ветер / Снежные бури", "Freezing Winds / Snowstorms"
        ),
        ["Pristine"] = (
            "Идеальное состояние", "Pristine",
            "Предмет высочайшего качества без повреждений. Значительно увеличивает стоимость продажи и прочность.",
            "Pristine item in flawless mint condition. Significantly increases selling value and item durability.",
            "Предметы и экипировка", "Items & Equipment",
            "Идеальный крафт / Редкие сундуки", "Perfect Crafting / Rare Loot"
        ),
        ["HRPristineAffix"] = (
            "Идеальное состояние", "Pristine",
            "Предмет высочайшего качества без повреждений. Значительно увеличивает стоимость продажи и прочность.",
            "Pristine item in flawless mint condition. Significantly increases selling value and item durability.",
            "Предметы и экипировка", "Items & Equipment",
            "Идеальный крафт / Редкие сундуки", "Perfect Crafting / Rare Loot"
        ),
        ["Идеальное состояние"] = (
            "Идеальное состояние", "Pristine",
            "Предмет высочайшего качества без повреждений. Значительно увеличивает стоимость продажи и прочность.",
            "Pristine item in flawless mint condition. Significantly increases selling value and item durability.",
            "Предметы и экипировка", "Items & Equipment",
            "Идеальный крафт / Редкие сундуки", "Perfect Crafting / Rare Loot"
        ),
        ["Radiation"] = (
            "Радиация", "Radiation",
            "Воздействие радиоактивных материалов или зон. Постепенно снижает максимальный запас здоровья, пока не принято лекарство.",
            "Exposure to radioactive zones or materials. Gradually caps maximum health until treated with anti-radiation medicine.",
            "Персонаж (радиация)", "Player Character (radiation)",
            "Радиоактивные руды / Зараженные зоны", "Radioactive Ores / Contaminated Zones"
        ),
        ["Radioactive"] = (
            "Радиоактивный", "Radioactive",
            "Воздействие радиоактивных материалов или зон. Постепенно снижает максимальный запас здоровья, пока не принято лекарство.",
            "Exposure to radioactive zones or materials. Gradually caps maximum health until treated with anti-radiation medicine.",
            "Персонаж (радиация)", "Player Character (radiation)",
            "Радиоактивные руды / Зараженные зоны", "Radioactive Ores / Contaminated Zones"
        ),
        ["Радиоактивный"] = (
            "Радиоактивный", "Radioactive",
            "Воздействие радиоактивных материалов или зон. Постепенно снижает максимальный запас здоровья, пока не принято лекарство.",
            "Exposure to radioactive zones or materials. Gradually caps maximum health until treated with anti-radiation medicine.",
            "Персонаж (радиация)", "Player Character (radiation)",
            "Радиоактивные руды / Зараженные зоны", "Radioactive Ores / Contaminated Zones"
        ),
        ["Радиация"] = (
            "Радиация", "Radiation",
            "Воздействие радиоактивных материалов или зон. Постепенно снижает максимальный запас здоровья, пока не принято лекарство.",
            "Exposure to radioactive zones or materials. Gradually caps maximum health until treated with anti-radiation medicine.",
            "Персонаж (радиация)", "Player Character (radiation)",
            "Радиоактивные руды / Зараженные зоны", "Radioactive Ores / Contaminated Zones"
        ),
        ["Starving"] = (
            "Голод", "Starvation",
            "Сытость на нуле. Резко снижает максимальную выносливость и скорость бега персонажа. Требуется еда.",
            "Satiation depleted to zero. Heavily reduces maximum stamina pool and sprint speed. Consume food immediately.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Отсутствие пищи", "Food Depletion"
        ),
        ["Hunger"] = (
            "Голод", "Starvation",
            "Сытость на нуле. Резко снижает максимальную выносливость и скорость бега персонажа. Требуется еда.",
            "Satiation depleted to zero. Heavily reduces maximum stamina pool and sprint speed. Consume food immediately.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Отсутствие пищи", "Food Depletion"
        ),
        ["Голод"] = (
            "Голод", "Starvation",
            "Сытость на нуле. Резко снижает максимальную выносливость и скорость бега персонажа. Требуется еда.",
            "Satiation depleted to zero. Heavily reduces maximum stamina pool and sprint speed. Consume food immediately.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Отсутствие пищи", "Food Depletion"
        ),
        ["Dehydrated"] = (
            "Жажда", "Dehydration",
            "Жажда на максимуме. Замедляет восстановление выносливости и увеличивает восприимчивость к перегреву. Требуется вода.",
            "Hydration reached zero. Halves stamina regeneration rate and raises heat vulnerability. Drink water immediately.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Отсутствие воды / Жара", "Water Depletion / Heat"
        ),
        ["Thirst"] = (
            "Жажда", "Dehydration",
            "Жажда на максимуме. Замедляет восстановление выносливости и увеличивает восприимчивость к перегреву. Требуется вода.",
            "Hydration reached zero. Halves stamina regeneration rate and raises heat vulnerability. Drink water immediately.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Отсутствие воды / Жара", "Water Depletion / Heat"
        ),
        ["Жажда"] = (
            "Жажда", "Dehydration",
            "Жажда на максимуме. Замедляет восстановление выносливости и увеличивает восприимчивость к перегреву. Требуется вода.",
            "Hydration reached zero. Halves stamina regeneration rate and raises heat vulnerability. Drink water immediately.",
            "Персонаж (критический статус)", "Player Character (critical debuff)",
            "Отсутствие воды / Жара", "Water Depletion / Heat"
        ),
        ["Wet"] = (
            "Намокание", "Soaked / Wet",
            "Персонаж или предмет намок. Повышает защиту от огня, но увеличивает урон от электричества и ускоряет замерзание.",
            "Character or item is soaked. Increases fire resistance, but drastically amplifies electric damage and chill rate.",
            "Персонаж и предметы", "Character & Items",
            "Водоемы / Дождь", "Water Bodies / Rain"
        ),
        ["Намокание"] = (
            "Намокание", "Soaked / Wet",
            "Персонаж или предмет намок. Повышает защиту от огня, но увеличивает урон от электричества и ускоряет замерзание.",
            "Character or item is soaked. Increases fire resistance, but drastically amplifies electric damage and chill rate.",
            "Персонаж и предметы", "Character & Items",
            "Водоемы / Дождь", "Water Bodies / Rain"
        ),
        ["Chilled"] = (
            "Охлаждение", "Chilled",
            "Воздействие холодной воды или стужи. Снижает скорость бега и постепенно расходует тепло тела.",
            "Cold immersion status. Decreases sprint speed and slowly drains body warmth reserves.",
            "Персонаж (статус)", "Player Character (status)",
            "Ледяная вода / Ночной холод", "Cold Water / Night Cold"
        ),
        ["Bleed"] = (
            "Кровотечение", "Bleeding",
            "Глубокие раны. Наносит периодический физический урон в секунду. Складывается в серии ударов.",
            "Deep lacerations inflicting periodic physical damage per second. Stacks upon multiple strikes.",
            "Оружие и персонажи", "Weapons & Characters",
            "Колющее / Режущее оружие / Когти хищников", "Slashing / Piercing Weapons / Predator Claws"
        ),
        ["HRBleedAffix"] = (
            "Кровотечение", "Bleeding",
            "Глубокие раны. Наносит периодический физический урон в секунду. Складывается в серии ударов.",
            "Deep lacerations inflicting periodic physical damage per second. Stacks upon multiple strikes.",
            "Оружие и персонажи", "Weapons & Characters",
            "Колющее / Режущее оружие / Когти хищников", "Slashing / Piercing Weapons / Predator Claws"
        ),
        ["Кровавый"] = (
            "Кровавый", "Bleeding",
            "Глубокие раны. Наносит периодический физический урон в секунду. Складывается в серии ударов.",
            "Deep lacerations inflicting periodic physical damage per second. Stacks upon multiple strikes.",
            "Оружие и персонажи", "Weapons & Characters",
            "Колющее / Режущее оружие / Когти хищников", "Slashing / Piercing Weapons / Predator Claws"
        ),
        ["Кровотечение"] = (
            "Кровотечение", "Bleeding",
            "Глубокие раны. Наносит периодический физический урон в секунду. Складывается в серии ударов.",
            "Deep lacerations inflicting periodic physical damage per second. Stacks upon multiple strikes.",
            "Оружие и персонажи", "Weapons & Characters",
            "Колющее / Режущее оружие / Когти хищников", "Slashing / Piercing Weapons / Predator Claws"
        ),
        ["BleedStack"] = (
            "Стек кровотечения", "Bleed Buildup",
            "Уровень накопления кровотечения. При заполнении шкалы вызывает сильный всплеск физического урона.",
            "Bleed intensity stack. Triggers a massive burst of hemorrhage damage upon reaching maximum stacks.",
            "Оружие и персонажи", "Weapons & Characters",
            "Серийные режущие удары", "Continuous Slashing Combos"
        ),
        ["FireStack"] = (
            "Накопление горения", "Burn Buildup",
            "Уровень накопления огня. При заполнении шкалы поджигает цель, вызывая длительный периодический урон огнем.",
            "Fire accumulation stack. Ignites the target upon reaching full threshold, inflicting sustained burn damage.",
            "Оружие и стихии", "Weapons & Elements",
            "Огненное оружие / Костры / Факелы", "Fire Weapons / Campfires / Torches"
        ),
        ["Stolen"] = (
            "Украденный предмет", "Stolen Item",
            "Предмет был украден у торговцев или жителей. Законные торговцы откажутся его покупать без специальных навыков сбыта.",
            "Stolen property mark. Lawful merchants refuse to buy stolen goods unless player possesses underground fencing perks.",
            "Предметы (статус владения)", "Items (Ownership Status)",
            "Воровство из чужих магазинов", "Stealing from NPC Shops"
        ),
        ["Украдено"] = (
            "Украденный предмет", "Stolen Item",
            "Предмет был украден у торговцев или жителей. Законные торговцы откажутся его покупать без специальных навыков сбыта.",
            "Stolen property mark. Lawful merchants refuse to buy stolen goods unless player possesses underground fencing perks.",
            "Предметы (статус владения)", "Items (Ownership Status)",
            "Воровство из чужих магазинов", "Stealing from NPC Shops"
        ),
        ["Sharpened"] = (
            "Заточенный", "Sharpened",
            "Оружие идеально заточено на точильном камне. Дает бонус к прямому физическому урону.",
            "Weapon honed on a sharpening stone. Grants bonus to base direct physical attack damage.",
            "Холодное оружие", "Melee Weapons",
            "Точильный станок / Камни заточки", "Sharpening Wheel / Whetstones"
        ),
        ["Invulnerable"] = (
            "Неуязвимость", "Invulnerable",
            "Полный иммунитет к любому входящему урону на время действия эффекта.",
            "Complete immunity to all incoming forms of damage for effect duration.",
            "Персонаж и объекты", "Player Character & Objects",
            "Особые эффекты / Зелья", "Special Effects / Potions"
        ),
        ["EarningBonus"] = (
            "Бонус к заработку", "Earning Bonus",
            "Увеличивает количество монет и выручки от продаж в магазине.",
            "Increases shop revenue and coins received from customer sales.",
            "Магазин и торговля", "Shop & Economy",
            "Улучшения магазина / Перки", "Shop Upgrades / Economy Perks"
        ),
        ["ItemSize"] = (
            "Размер предмета", "Item Size",
            "Определяет габариты и вес предмета при транспортировке и размещении на витринах магазина.",
            "Determines spatial footprint and transport weight when placing items on shop display counters.",
            "Все предметы", "All Items",
            "Физические свойства предмета", "Item Physics Property"
        ),
        ["CleaningPower"] = (
            "Сила очистки", "Cleaning Power",
            "Эффективность удаления грязи и пятен с пола, витрин и мебели шваброй или моющими средствами.",
            "Effectiveness at scrubbing mud, dirt, and spills from store floors and display counters.",
            "Инструменты уборки", "Cleaning Tools & Mops",
            "Инструменты клининга", "Cleaning Equipment"
        ),
        ["Slots"] = (
            "Слоты", "Slots",
            "Количество доступных ячеек для хранения предметов в контейнере или витрине.",
            "Number of storage or display item slots available on this fixture.",
            "Контейнеры и витрины", "Containers & Displays",
            "Свойства мебели", "Furniture Property"
        ),
        ["Damage"] = (
            "Урон", "Damage",
            "Базовый урон оружия или инструмента при нанесении прямых ударов по врагам и объектам.",
            "Base damage inflicted by weapon or tool upon landing direct strikes against targets.",
            "Оружие и инструменты", "Weapons & Tools",
            "Характеристики оружия / Модификаторы", "Weapon Stats / Modifiers"
        ),
        ["Урон"] = (
            "Урон", "Damage",
            "Базовый урон оружия или инструмента при нанесении прямых ударов по врагам и объектам.",
            "Base damage inflicted by weapon or tool upon landing direct strikes against targets.",
            "Оружие и инструменты", "Weapons & Tools",
            "Характеристики оружия / Модификаторы", "Weapon Stats / Modifiers"
        )
    };

    internal static string CleanAttributeName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string s = raw.Trim(':', ' ');
        if (s.StartsWith("HR", StringComparison.OrdinalIgnoreCase) && s.Length > 2 && char.IsUpper(s[2]))
            s = s.Substring(2);
        if (s.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase) && s.Length > 9)
            s = s.Substring(0, s.Length - 9);
        if (s.EndsWith("Affix", StringComparison.OrdinalIgnoreCase) && s.Length > 5)
            s = s.Substring(0, s.Length - 5);
        if (s.EndsWith("FoodAffix", StringComparison.OrdinalIgnoreCase) && s.Length > 9)
            s = s.Substring(0, s.Length - 9);

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && (!char.IsUpper(s[i - 1]) || (i + 1 < s.Length && !char.IsUpper(s[i + 1]))))
            {
                sb.Append(' ');
            }
            sb.Append(s[i]);
        }
        return sb.ToString().Trim(':', ' ');
    }

    private static readonly Dictionary<string, string> _termTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Flaming"] = "Огненный",
        ["Chilling"] = "Леденящий",
        ["Freezing"] = "Замораживающий",
        ["Poisonous"] = "Ядовитый",
        ["Poison"] = "Яд",
        ["Shock"] = "Шок",
        ["Electricity"] = "Электричество",
        ["Bleed"] = "Кровотечение",
        ["Bleeding"] = "Кровотечение",
        ["Bloody"] = "Кровавый",
        ["Breaching"] = "Пробивающий",
        ["Armored"] = "Бронированный",
        ["Armor Piercing"] = "Бронебойность",
        ["Critical"] = "Критический",
        ["Damage"] = "Урон",
        ["Attack"] = "Атака",
        ["Defense"] = "Защита",
        ["Resistance"] = "Сопротивление",
        ["Regen"] = "Регенерация",
        ["Regeneration"] = "Регенерация",
        ["Stamina"] = "Выносливость",
        ["Health"] = "Здоровье",
        ["Speed"] = "Скорость",
        ["Movement Speed"] = "Скорость бега",
        ["Swing Speed"] = "Скорость замаха",
        ["Efficiency"] = "Эффективность",
        ["Warmth"] = "Тепло",
        ["Dehydration"] = "Обезвоживание",
        ["Starvation"] = "Голод",
        ["Cooking"] = "Кулинария",
        ["Cook"] = "Готовка",
        ["Craft"] = "Крафт",
        ["Crafting"] = "Создание",
        ["Smiting"] = "Карающий",
        ["Healing"] = "Исцеление",
        ["Miner"] = "Шахтер",
        ["Mage Armor"] = "Магическая броня",
        ["Taunt"] = "Провокация",
        ["Thorns"] = "Шипы",
        ["Burst"] = "Всплеск",
        ["Twin"] = "Двойной",
        ["Triple"] = "Тройной",
        ["Projectiles"] = "Снаряды",
        ["Homing"] = "Самонаведение",
        ["Fishing"] = "Рыбалка",
        ["Rod"] = "Удочка",
        ["Knots"] = "Узлы",
        ["Valuable"] = "Ценный",
        ["Dense"] = "Плотный",
        ["Antitoxin"] = "Антитоксин",
        ["Weightless"] = "Невесомый",
        ["Springy"] = "Пружинистый",
        ["Shelled"] = "Панцирный",
        ["Special Delivery"] = "Особая доставка",
        ["Quickfire"] = "Скорострельность",
        ["Quiet Wading"] = "Бесшумное движение",
        ["Item Size"] = "Размер предмета",
        ["Cleaning Power"] = "Сила очистки",
        ["Slots"] = "Слоты",
        ["Invulnerable"] = "Неуязвимость",
        ["Earning Bonus"] = "Бонус к заработку",
        ["Attack Speed"] = "Скорость атаки",
        ["Add Happiness"] = "Прирост настроения",
        ["Reduce Happiness"] = "Снижение настроения",
        ["Display Sale To Customer"] = "Продажа с витрины",
        ["Open Close Store"] = "Открытие/Закрытие магазина",
        ["Can craft other items"] = "Создание других предметов",
        ["Skips the bartering minigame"] = "Пропуск мини-игры торга",
        ["The required power for this item"] = "Требуемая энергия",
        ["The I D of the power grid this connects to"] = "ID электросети",
        ["Pristine"] = "Идеальное состояние",
        ["Haggling"] = "Мастерство торга",
        ["Radiation"] = "Радиация",
        ["Radioactive"] = "Радиоактивный",
        ["Wet"] = "Намокание",
        ["Hunger"] = "Голод",
        ["Thirst"] = "Жажда",
        ["Stolen"] = "Украдено"
    };

    private static readonly Dictionary<string, string> _ruToEnTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Идеальное состояние"] = "Pristine",
        ["Мастерство торга"] = "Barter Mastery",
        ["Благословение Беллсталкера"] = "Bellstalker's Blessing",
        ["Черта происхождения"] = "Origin Trait",
        ["Эффективность медицины"] = "Medicine Potency",
        ["Грязь"] = "Covered in Dirt",
        ["Смертельное обморожение"] = "Lethal Hypothermia",
        ["Обморожение"] = "Frostbite",
        ["Радиация"] = "Radiation",
        ["Радиоактивный"] = "Radioactive",
        ["Голод"] = "Starvation",
        ["Жажда"] = "Dehydration",
        ["Намокание"] = "Soaked / Wet",
        ["Охлаждение"] = "Chilled",
        ["Кровотечение"] = "Bleeding",
        ["Кровавый"] = "Bleeding",
        ["Стек кровотечения"] = "Bleed Buildup",
        ["Накопление горения"] = "Burn Buildup",
        ["Украдено"] = "Stolen Item",
        ["Украденный предмет"] = "Stolen Item",
        ["Украденный"] = "Stolen Item",
        ["Заточенный"] = "Sharpened",
        ["Неуязвимость"] = "Invulnerable",
        ["Бонус к заработку"] = "Earning Bonus",
        ["Размер предмета"] = "Item Size",
        ["Сила очистки"] = "Cleaning Power",
        ["Слоты"] = "Slots",
        ["Урон"] = "Damage",
        ["Защита"] = "Defense",
        ["Атака"] = "Attack",
        ["Здоровье"] = "Health",
        ["Выносливость"] = "Stamina",
        ["Скорость"] = "Speed",
        ["Скорость бега"] = "Movement Speed",
        ["Скорость атаки"] = "Attack Speed",
        ["Шипы"] = "Thorns",
        ["Яд"] = "Poison",
        ["Шок"] = "Shock",
        ["Огонь"] = "Fire"
    };

    private static string TranslateAffixTitle(string englishName)
    {
        if (string.IsNullOrEmpty(englishName)) return "";
        string res = englishName.Trim(':', ' ');
        foreach (var pair in _termTranslations)
        {
            if (res.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                res = System.Text.RegularExpressions.Regex.Replace(res, "\\b" + System.Text.RegularExpressions.Regex.Escape(pair.Key) + "\\b", pair.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }
        return res.Trim(':', ' ');
    }

    private static void ResolveAttributeTitles(JeiAttributeEntry entry, string rawStringId, string gameTitle, string locTitle)
    {
        string sId = rawStringId ?? "";
        string sTitle = gameTitle ?? "";
        string sLoc = locTitle ?? "";

        // 1. English candidate from StringID
        string cleanFromId = CleanAttributeName(sId);
        if (string.IsNullOrEmpty(cleanFromId) && entry.ID > 0)
            cleanFromId = "#" + entry.ID;

        // 2. Candidate EN: prioritize non-Cyrillic gameTitle, fallback to cleanFromId
        string candidateEn = "";
        if (!string.IsNullOrEmpty(sTitle) && !HasCyrillic(sTitle))
            candidateEn = CleanAttributeName(sTitle);
        else if (!string.IsNullOrEmpty(cleanFromId) && !HasCyrillic(cleanFromId))
            candidateEn = cleanFromId;
        else
            candidateEn = CleanAttributeName(sTitle);

        candidateEn = candidateEn.Trim(':', ' ');

        // 3. Candidate RU: prioritize Cyrillic locTitle, then Cyrillic gameTitle, fallback to translated candidateEn
        string candidateRu = "";
        if (!string.IsNullOrEmpty(sLoc) && HasCyrillic(sLoc))
            candidateRu = sLoc.Trim(':', ' ');
        else if (!string.IsNullOrEmpty(sTitle) && HasCyrillic(sTitle))
            candidateRu = sTitle.Trim(':', ' ');
        else
            candidateRu = TranslateAffixTitle(candidateEn).Trim(':', ' ');

        entry.TitleEn = candidateEn;
        entry.TitleRu = candidateRu;

        // 4. Match against unique attribute knowledge
        string searchKey = $"{sId} {cleanFromId} {sTitle} {sLoc} {candidateEn} {candidateRu}".Trim();
        foreach (var kv in _uniqueAttributeKnowledge)
        {
            if (searchKey.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var info = kv.Value;
                entry.TitleRu = info.titleRu.Trim(':', ' ');
                entry.TitleEn = info.titleEn.Trim(':', ' ');
                if (!string.IsNullOrEmpty(info.descRu)) entry.DescriptionRu = info.descRu;
                if (!string.IsNullOrEmpty(info.descEn)) entry.DescriptionEn = info.descEn;
                if (!string.IsNullOrEmpty(info.appliesRu)) entry.AppliesToRu = info.appliesRu;
                if (!string.IsNullOrEmpty(info.appliesEn)) entry.AppliesToEn = info.appliesEn;
                if (!string.IsNullOrEmpty(info.sourceRu)) entry.SourceNameRu = info.sourceRu;
                if (!string.IsNullOrEmpty(info.sourceEn)) entry.SourceNameEn = info.sourceEn;
                break;
            }
        }

        // 5. Fail-safe: TitleEn MUST NOT have Cyrillic letters
        if (HasCyrillic(entry.TitleEn))
        {
            foreach (var kv in _ruToEnTranslations)
            {
                if (entry.TitleEn.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    entry.TitleEn = kv.Value;
                    break;
                }
            }
        }
        if (HasCyrillic(entry.TitleEn))
        {
            if (!string.IsNullOrEmpty(cleanFromId) && !HasCyrillic(cleanFromId))
                entry.TitleEn = cleanFromId;
            else if (!string.IsNullOrEmpty(entry.StringID) && !HasCyrillic(entry.StringID))
                entry.TitleEn = CleanAttributeName(entry.StringID);
            else
                entry.TitleEn = "Attribute " + entry.ID;
        }

        // 6. Guarantee no stray colons or spaces
        entry.TitleEn = entry.TitleEn?.Trim(':', ' ') ?? "";
        entry.TitleRu = entry.TitleRu?.Trim(':', ' ') ?? "";
    }

    private static void ScanAttributeDatabase()
    {
        try
        {
            var gi = ModService.GameInstance;
            var attrDb = gi?.MasterAttributeDB;
            if (attrDb == null)
            {
                var dbs = Resources.FindObjectsOfTypeAll<HRAttributeDatabase>();
                if (dbs != null && dbs.Length > 0) attrDb = dbs[0];
            }

            // 1. Initialize HRAffixRegistry
            try { HRAffixRegistry.EnsureInitialized(); } catch (Exception) { }

            var indexedDefs = new HashSet<int>();

            // 2. Scan all definitions from modern HRAffixRegistry (200+ perks and affixes)
            if (HRAffixRegistry.Definitions != null)
            {
                int defCount = HRAffixRegistry.Definitions.Count;
                _log?.LogInfo($"[JEI] Scanning {defCount} affix definitions from HRAffixRegistry...");

                for (int defIdx = 0; defIdx < defCount; defIdx++)
                {
                    var def = HRAffixRegistry.Definitions[defIdx];
                    if (def == null) continue;

                    int affixId = def.ID > 0 ? def.ID : (def.LinkedAttributeID > 0 ? def.LinkedAttributeID : (10000 + defIdx));
                    if (indexedDefs.Contains(affixId)) continue;
                    indexedDefs.Add(affixId);

                    var entry = new JeiAttributeEntry
                    {
                        ID = affixId,
                        StringID = def.StringID,
                        MinValue = def.MinValue,
                        MaxValue = def.MaxValue,
                        Duration = def.Duration,
                        TitleColor = Color.white
                    };

                    // Display Name & Titles
                    string dispName = null;
                    try { dispName = HRAffixRegistry.GetDisplayNameFallback(def); } catch { }
                    ResolveAttributeTitles(entry, def.StringID, dispName, null);

                    // Min Rarity
                    if ((int)def.MinimumRarity > 0)
                        entry.MinRarity = def.MinimumRarity.ToString();

                    // Source
                    if (!string.IsNullOrEmpty(def.LinkedAttributeSource))
                    {
                        entry.SourceNameEn = def.LinkedAttributeSource;
                        entry.SourceNameRu = def.LinkedAttributeSource;
                    }

                    // Applies to
                    switch (def.ItemType)
                    {
                        case HRAffixItemType.Weapon:
                            entry.AppliesToEn = "Weapons (Melee & Ranged)";
                            entry.AppliesToRu = "Оружие (ближний и дальний бой)";
                            break;
                        case HRAffixItemType.Clothing:
                            entry.AppliesToEn = "Armor & Clothing";
                            entry.AppliesToRu = "Броня и экипировка";
                            break;
                        case HRAffixItemType.Food:
                            entry.AppliesToEn = "Food Meals & Ingredients";
                            entry.AppliesToRu = "Еда, блюда и ингредиенты";
                            break;
                        case HRAffixItemType.FishingRod:
                            entry.AppliesToEn = "Fishing Rods";
                            entry.AppliesToRu = "Удочки";
                            break;
                        case HRAffixItemType.CraftingStation:
                            entry.AppliesToEn = "Crafting Workstations";
                            entry.AppliesToRu = "Рабочие верстаки";
                            break;
                        default:
                            entry.AppliesToEn = def.ItemType.ToString();
                            entry.AppliesToRu = def.ItemType.ToString();
                            break;
                    }

                    // Triggers
                    entry.TriggerTextEn = def.Triggers.ToString();
                    string trig = def.Triggers.ToString().ToLowerInvariant();
                    if (trig.Contains("hit")) entry.TriggerTextRu = "При нанесении удара";
                    else if (trig.Contains("damage")) entry.TriggerTextRu = "При получении урона";
                    else if (trig.Contains("eat") || trig.Contains("consume")) entry.TriggerTextRu = "При употреблении в пищу";
                    else if (trig.Contains("equip")) entry.TriggerTextRu = "При экипировке (пассивно)";
                    else entry.TriggerTextRu = "Постоянный эффект";

                    // Duration
                    if (entry.Duration > 0)
                    {
                        entry.DurationTextEn = $"{entry.Duration:0.#}s";
                        entry.DurationTextRu = $"{entry.Duration:0.#} сек.";
                    }
                    else
                    {
                        entry.DurationTextEn = "Permanent (passive)";
                        entry.DurationTextRu = "Постоянно (пассивный)";
                    }

                    // Value Range
                    if (entry.MinValue != 0 || entry.MaxValue != 0)
                    {
                        if (entry.MaxValue == 0 || Mathf.Approximately(entry.MinValue, entry.MaxValue))
                        {
                            entry.ValueRangeText = (entry.MinValue > 0 ? "+" : "") + $"{entry.MinValue:0.#}";
                        }
                        else
                        {
                            entry.ValueRangeText = (entry.MinValue > 0 ? "+" : "") + $"{entry.MinValue:0.#} - {entry.MaxValue:0.#}";
                        }
                    }
                    else
                    {
                        entry.ValueRangeText = "";
                    }

                    // Description (if not already set by unique knowledge)
                    if (string.IsNullOrEmpty(entry.DescriptionEn))
                    {
                        if (!string.IsNullOrEmpty(entry.ValueRangeText))
                        {
                            entry.DescriptionEn = $"{entry.ValueRangeText}% {entry.TitleEn}";
                            entry.DescriptionRu = $"{entry.ValueRangeText}% {entry.TitleRu}";
                        }
                        else
                        {
                            entry.DescriptionEn = entry.TitleEn;
                            entry.DescriptionRu = entry.TitleRu;
                        }
                    }

                    // Category
                    string catSearch = ((entry.StringID ?? "") + " " + (entry.TitleEn ?? "")).ToLowerInvariant();
                    if (def.ItemType == HRAffixItemType.Food || catSearch.Contains("food") || catSearch.Contains("cook") || catSearch.Contains("meal") || catSearch.Contains("satiat") || catSearch.Contains("hydrat"))
                    {
                        entry.Category = "Food";
                    }
                    else if (def.ItemType == HRAffixItemType.Weapon || catSearch.Contains("damage") || catSearch.Contains("attack") || catSearch.Contains("crit") || catSearch.Contains("pierce") || catSearch.Contains("fire") || catSearch.Contains("ice") || catSearch.Contains("shock") || catSearch.Contains("poison") || catSearch.Contains("bleed"))
                    {
                        entry.Category = "Combat";
                    }
                    else if (def.ItemType == HRAffixItemType.Clothing || catSearch.Contains("armor") || catSearch.Contains("defense") || catSearch.Contains("shield") || catSearch.Contains("resist") || catSearch.Contains("regen"))
                    {
                        entry.Category = "Defense";
                    }
                    else
                    {
                        entry.Category = "Utility";
                    }

                    _attributes.Add(entry);
                    _attributesById[entry.ID] = entry;
                    if (def.LinkedAttributeID > 0)
                        _attributesById[def.LinkedAttributeID] = entry;
                }
            }

            // 3. Scan & Supplement from MasterAttributeDB (enrich matches, add unique legacy/boss attributes)
            if (attrDb?.AttributeInfos != null)
            {
                _log?.LogInfo($"[JEI] Supplementing with {attrDb.AttributeInfos.Length} attributes from MasterAttributeDB...");

                for (int i = 0; i < attrDb.AttributeInfos.Length; i++)
                {
                    var ai = attrDb.AttributeInfos[i];
                    if (ai == null) continue;

                    int resolvedId = ai.ID > 0 ? ai.ID : (i + 1);

                    // Check if already registered from HRAffixRegistry
                    if (_attributesById.TryGetValue(resolvedId, out var existing))
                    {
                        if (existing.Icon == null && ai.Icon != null) existing.Icon = ai.Icon;
                        if (ai.TitleColor.a > 0.05f) existing.TitleColor = ai.TitleColor;

                        // Check localized title/description from game
                        string locT = null;
                        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray locD = null;
                        try
                        {
                            attrDb.GetLocalizedTitleDesSplit(ai, out locT, out locD);
                        }
                        catch { }

                        ResolveAttributeTitles(existing, ai.StringID ?? existing.StringID, ai.Title, locT);

                        if (locD != null && locD.Length > 0 && string.IsNullOrEmpty(existing.DescriptionRu))
                        {
                            string fullLoc = string.Join(" ", locD);
                            if (!string.IsNullOrEmpty(existing.ValueRangeText))
                                fullLoc = fullLoc.Replace("{0}", existing.ValueRangeText);
                            else
                                fullLoc = fullLoc.Replace("{0}%", "").Replace("{0}", "").Trim();
                            if (!string.IsNullOrEmpty(fullLoc)) existing.DescriptionRu = fullLoc;
                        }

                        // Check description format
                        string dFormat = ai.DescriptionFormat;
                        if (!string.IsNullOrEmpty(dFormat) && string.IsNullOrEmpty(existing.DescriptionEn))
                        {
                            if (dFormat.Contains("{0}"))
                            {
                                if (!string.IsNullOrEmpty(existing.ValueRangeText))
                                    existing.DescriptionEn = dFormat.Replace("{0}", existing.ValueRangeText);
                                else
                                    existing.DescriptionEn = dFormat.Replace("{0}%", "").Replace("{0}", "").Trim();
                            }
                            else
                            {
                                existing.DescriptionEn = dFormat;
                            }
                        }
                        continue;
                    }

                    // This is a unique/legacy attribute!
                    var entry = new JeiAttributeEntry
                    {
                        ID = resolvedId,
                        StringID = ai.StringID,
                        TitleColor = ai.TitleColor.a > 0.05f ? ai.TitleColor : Color.white,
                        Icon = ai.Icon,
                        DurationTextEn = "Permanent (passive)",
                        DurationTextRu = "Постоянно (пассивный)",
                        ValueRangeText = ""
                    };

                    string locTitle = null;
                    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray locDescs = null;
                    try { attrDb.GetLocalizedTitleDesSplit(ai, out locTitle, out locDescs); } catch { }

                    ResolveAttributeTitles(entry, ai.StringID, ai.Title, locTitle);

                    // Inspect prefab for numbers/sources
                    HRAttribute prefabAttr = null;
                    if (ai.AttributePrefab != null)
                    {
                        try { prefabAttr = ai.AttributePrefab.GetComponent<HRAttribute>(); } catch { }
                    }

                    if (prefabAttr != null)
                    {
                        if (prefabAttr.Value != 0)
                        {
                            entry.MinValue = prefabAttr.Value;
                            entry.ValueRangeText = (prefabAttr.Value > 0 ? "+" : "") + $"{prefabAttr.Value:0.#}";
                        }
                        if (prefabAttr.Duration > 0)
                        {
                            entry.Duration = prefabAttr.Duration;
                            entry.DurationTextEn = $"{prefabAttr.Duration:0.#}s";
                            entry.DurationTextRu = $"{prefabAttr.Duration:0.#} сек.";
                        }
                        if (!string.IsNullOrEmpty(prefabAttr.Source))
                        {
                            entry.SourceNameEn = prefabAttr.Source;
                            entry.SourceNameRu = prefabAttr.Source;
                        }
                        if (prefabAttr.DisplayTriggerTypes != null && prefabAttr.DisplayTriggerTypes.Length > 0)
                        {
                            entry.TriggerTextEn = string.Join(", ", prefabAttr.DisplayTriggerTypes);
                            entry.TriggerTextRu = string.Join(", ", prefabAttr.DisplayTriggerTypes);
                        }
                    }

                    // Formatted description if not already set by unique knowledge
                    if (string.IsNullOrEmpty(entry.DescriptionEn))
                    {
                        string descF = ai.DescriptionFormat;
                        if (!string.IsNullOrEmpty(descF))
                        {
                            if (descF.Contains("{0}"))
                            {
                                if (!string.IsNullOrEmpty(entry.ValueRangeText))
                                    entry.DescriptionEn = descF.Replace("{0}", entry.ValueRangeText);
                                else
                                    entry.DescriptionEn = descF.Replace("{0}%", "").Replace("{0}", "").Trim();
                            }
                            else
                            {
                                entry.DescriptionEn = descF;
                            }
                        }
                        else
                        {
                            entry.DescriptionEn = entry.TitleEn;
                        }
                    }

                    if (string.IsNullOrEmpty(entry.DescriptionRu))
                    {
                        if (locDescs != null && locDescs.Length > 0)
                            entry.DescriptionRu = string.Join(" ", locDescs).Replace("{0}%", "").Replace("{0}", "").Trim();
                        else
                            entry.DescriptionRu = entry.DescriptionEn;
                    }

                    if (string.IsNullOrEmpty(entry.AppliesToEn))
                    {
                        entry.AppliesToEn = "Player Character & Equipment";
                        entry.AppliesToRu = "Персонаж и экипировка";
                    }

                    // Category
                    string cSearch = ((entry.StringID ?? "") + " " + (entry.TitleEn ?? "") + " " + (entry.DescriptionEn ?? "")).ToLowerInvariant();
                    if (cSearch.Contains("damage") || cSearch.Contains("attack") || cSearch.Contains("bleed") || cSearch.Contains("crit") || cSearch.Contains("fire") || cSearch.Contains("shock") || cSearch.Contains("poison"))
                        entry.Category = "Combat";
                    else if (cSearch.Contains("defense") || cSearch.Contains("armor") || cSearch.Contains("resist") || cSearch.Contains("guard") || cSearch.Contains("health") || cSearch.Contains("regen"))
                        entry.Category = "Defense";
                    else if (cSearch.Contains("food") || cSearch.Contains("meal") || cSearch.Contains("hunger") || cSearch.Contains("starv") || cSearch.Contains("thirst") || cSearch.Contains("dehydrat"))
                        entry.Category = "Food";
                    else
                        entry.Category = "Utility";

                    _attributes.Add(entry);
                    _attributesById[resolvedId] = entry;
                }
            }

            _attributes.Sort((a, b) => a.ID.CompareTo(b.ID));
            _log?.LogInfo($"[JEI] Unified Attributes Codex loaded: {_attributes.Count} entries.");
        }
        catch (Exception e)
        {
            _log?.LogWarning($"[JEI] ScanAttributeDatabase error: {e.Message}");
        }
    }

    private static void ScanSkillTrees(HRSkillTree primaryTree)
    {
        try
        {
            var trees = new List<HRSkillTree>();
            if (primaryTree != null) trees.Add(primaryTree);
            try
            {
                var allTrees = Resources.FindObjectsOfTypeAll<HRSkillTree>();
                if (allTrees != null)
                {
                    foreach (var t in allTrees)
                        if (t != null && !trees.Contains(t)) trees.Add(t);
                }
            }
            catch (Exception) { }

            var visited = new HashSet<IntPtr>();
            foreach (var tree in trees)
            {
                if (tree?.RootNodes == null) continue;
                for (int i = 0; i < tree.RootNodes.Count; i++)
                {
                    var root = tree.RootNodes[i];
                    if (root != null)
                        TraverseSkillNode(tree, root, null, visited);
                }
            }
        }
        catch (Exception e)
        {
            _log?.LogWarning($"[JEI] ScanSkillTrees error: {e.Message}");
        }
    }

    private static void TraverseSkillNode(HRSkillTree tree, HRSkillNode node, HRSkillNode parent, HashSet<IntPtr> visited)
    {
        if (node == null || node.Pointer == IntPtr.Zero) return;
        if (!visited.Add(node.Pointer)) return;

        try
        {
            string nodeNameRu = null;
            try { nodeNameRu = node.GetSkillName(); } catch (Exception) { }
            string nodeNameEn = node.SkillName;
            if (string.IsNullOrWhiteSpace(nodeNameRu)) nodeNameRu = nodeNameEn;
            if (string.IsNullOrWhiteSpace(nodeNameEn)) nodeNameEn = nodeNameRu;

            string parentNameRu = null;
            string parentNameEn = null;
            if (parent != null)
            {
                try { parentNameRu = parent.GetSkillName(); } catch (Exception) { }
                parentNameEn = parent.SkillName;
                if (string.IsNullOrWhiteSpace(parentNameRu)) parentNameRu = parentNameEn;
                if (string.IsNullOrWhiteSpace(parentNameEn)) parentNameEn = parentNameRu;
            }

            int cost = node.SkillCost;
            int sp = node.SkillPoints;
            if (sp <= 0) sp = node._SkillPoints;

            string reqTextRu = BuildNodeRequirementText(node, true, out int reqItemId, out int reqItemCount);
            string reqTextEn = BuildNodeRequirementText(node, false, out _, out _);

            var recipeSkill = node.TryCast<HRRecipePlayerSkill>();
            if (recipeSkill?.WeaponRecipeToUnlock != null)
            {
                foreach (var w in recipeSkill.WeaponRecipeToUnlock)
                {
                    if (w == null || w.ItemID <= 0) continue;
                    if (_items.TryGetValue(w.ItemID, out var itemEntry))
                    {
                        itemEntry.Unlock ??= new JeiUnlockInfo();
                        var u = itemEntry.Unlock;
                        if (!string.IsNullOrWhiteSpace(nodeNameRu)) u.NodeNameRu = nodeNameRu;
                        if (!string.IsNullOrWhiteSpace(nodeNameEn)) u.NodeNameEn = nodeNameEn;
                        if (cost > 0) u.Cost = cost;
                        else if (tree != null)
                        {
                            try
                            {
                                int treeCost = tree.GetItemTotalUnlockCost(w.ItemID);
                                if (treeCost > 0) u.Cost = treeCost;
                            }
                            catch (Exception) { }
                        }
                        if (sp > 0) u.SkillPoints = sp;
                        if (!string.IsNullOrWhiteSpace(parentNameRu)) u.ParentNodeNameRu = parentNameRu;
                        if (!string.IsNullOrWhiteSpace(parentNameEn)) u.ParentNodeNameEn = parentNameEn;
                        if (!string.IsNullOrWhiteSpace(reqTextRu)) u.RequirementTextRu = reqTextRu;
                        if (!string.IsNullOrWhiteSpace(reqTextEn)) u.RequirementTextEn = reqTextEn;
                        if (reqItemId > 0)
                        {
                            u.RequiredItemID = reqItemId;
                            u.RequiredItemCount = Mathf.Max(1, reqItemCount);
                        }
                    }
                }
            }

            if (node.SkillsToUnlock != null)
            {
                foreach (var child in node.SkillsToUnlock)
                {
                    if (child != null)
                        TraverseSkillNode(tree, child, node, visited);
                }
            }
        }
        catch (Exception) { }
    }

    private static string GetItemNameForLang(int itemId, bool ru)
    {
        if (_items.TryGetValue(itemId, out var ie))
        {
            if (ru)
            {
                if (!string.IsNullOrEmpty(ie.Name)) return ie.Name;
                if (!string.IsNullOrEmpty(ie.FallbackName)) return ie.FallbackName;
            }
            else
            {
                if (!string.IsNullOrEmpty(ie.FallbackName)) return ie.FallbackName;
                if (!string.IsNullOrEmpty(ie.Name)) return ie.Name;
            }
        }
        return "#" + itemId;
    }

    private static string BuildNodeRequirementText(HRSkillNode node, bool ru, out int reqItemId, out int reqItemCount)
    {
        reqItemId = 0;
        reqItemCount = 0;
        var parts = new List<string>();

        try
        {
            if (node.ItemToCraft != null && node.ItemToCraft.ItemID > 0)
            {
                reqItemId = node.ItemToCraft.ItemID;
                reqItemCount = Mathf.Max(1, node.NumberToCraft);
                string itemName = GetItemNameForLang(reqItemId, ru);
                parts.Add(ru ? $"Скрафтить: {reqItemCount}x {itemName}" : $"Craft: {reqItemCount}x {itemName}");
            }
            if (node.ItemToCheck != null && node.ItemToCheck.ItemID > 0 && node.NumberOfTransactions > 0)
            {
                int checkId = node.ItemToCheck.ItemID;
                if (reqItemId <= 0)
                {
                    reqItemId = checkId;
                    reqItemCount = node.NumberOfTransactions;
                }
                string itemName = GetItemNameForLang(checkId, ru);
                parts.Add(ru ? $"Продать: {node.NumberOfTransactions}x {itemName}" : $"Sell: {node.NumberOfTransactions}x {itemName}");
            }
            if (!string.IsNullOrWhiteSpace(node.QuestName))
                parts.Add(ru ? $"Квест: {node.QuestName}" : $"Quest: {node.QuestName}");
            if (!string.IsNullOrWhiteSpace(node.EnemyName) && node.EnemyCount > 0)
                parts.Add(ru ? $"Победить: {node.EnemyCount}x {node.EnemyName}" : $"Defeat: {node.EnemyCount}x {node.EnemyName}");
            if (!string.IsNullOrWhiteSpace(node.OutpostName) && node.OutpostCount > 0)
                parts.Add(ru ? $"Аванпост: {node.OutpostCount}x {node.OutpostName}" : $"Outpost: {node.OutpostCount}x {node.OutpostName}");
            if (!string.IsNullOrWhiteSpace(node.MasterySkillToCheck) && node.MasteryLevel > 0)
                parts.Add(ru ? $"Мастерство {node.MasterySkillToCheck}: ур. {node.MasteryLevel}" : $"Mastery {node.MasterySkillToCheck}: Lv. {node.MasteryLevel}");
            if (node.ShopLevel > 0)
                parts.Add(ru ? $"Уровень магазина: {node.ShopLevel}" : $"Shop Level: {node.ShopLevel}");
            if (node.PlayerLevel > 0)
                parts.Add(ru ? $"Уровень игрока: {node.PlayerLevel}" : $"Player Level: {node.PlayerLevel}");
        }
        catch (Exception) { }

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static void PopulateFallbackStationIds()
    {
        var map = new (int flag, string[] keywords)[]
        {
            (2, new[] { "Wood Crafting Table", "Wooden Crafting Table", "Wooden Workbench", "Crafting Table" }),
            (131072, new[] { "Wood Crafting Table", "Wooden Crafting Table", "Wooden Workbench", "Crafting Table" }),
            (262144, new[] { "Stone Crafting Table", "Stone Workbench" }),
            (524288, new[] { "Copper Crafting Table", "Copper Workbench" }),
            (1048576, new[] { "Iron Crafting Table", "Iron Workbench" }),
            (8, new[] { "Furnace", "Stone Furnace", "Forge" }),
            (32, new[] { "Campfire", "Cooking Pot" }),
            (64, new[] { "Sawmill", "Splitting Block", "Carpentry" }),
            (128, new[] { "Charcoal Kiln", "Kiln" }),
            (256, new[] { "Sewing Machine", "Sewing Table", "Loom" }),
            (512, new[] { "Refinery", "Grinder" }),
            (1024, new[] { "Oven", "Brick Oven" }),
            (2048, new[] { "Stove", "Cooking Stove" }),
            (4096, new[] { "Electronics", "Chemistry" }),
            (8192, new[] { "Anvil", "Weaponry", "Smithy" }),
            (16384, new[] { "Advanced Forge", "Advanced Anvil" }),
            (32768, new[] { "Wine Barrel", "Keg", "Fermenter" }),
            (65536, new[] { "Coffee Machine", "Espresso" }),
            (2097152, new[] { "Cauldron", "Pot" })
        };

        foreach (var (flag, keywords) in map)
        {
            if (_stationItemIds.ContainsKey(flag) && _stationItemIds[flag] > 0)
                continue;

            foreach (var kw in keywords)
            {
                int foundId = 0;
                foreach (var it in _items.Values)
                {
                    if (it.FallbackName != null && it.FallbackName.Equals(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        foundId = it.ItemID;
                        break;
                    }
                }
                if (foundId == 0)
                {
                    foreach (var it in _items.Values)
                    {
                        if (it.FallbackName != null && it.FallbackName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundId = it.ItemID;
                            break;
                        }
                    }
                }
                if (foundId > 0)
                {
                    _stationItemIds[flag] = foundId;
                    _allStationItemIds.Add(foundId);
                    break;
                }
            }
        }
    }

    public static int ResolveStationItemId(int flag)
    {
        if (_stationItemIds.TryGetValue(flag, out int id) && id > 0)
            return id;
        return 0;
    }

    public static string ResolveStationName(int flag)
    {
        if (_stationItemIds.TryGetValue(flag, out int stnId) && stnId > 0 && _items.TryGetValue(stnId, out var stnEntry))
        {
            string dn = stnEntry.DisplayName;
            if (!string.IsNullOrEmpty(dn) && !dn.StartsWith("#"))
                return dn;
        }

        if (JeiLoc.IsRu)
        {
            return flag switch
            {
                0 => "Ручной крафт",
                1 => "Ручной крафт",
                2 => "Верстак",
                4 => "Чертёж постройки",
                8 => "Печь / Плавильня",
                16 => "Фермерство",
                32 => "Костёр",
                64 => "Лесопилка",
                128 => "Угольная печь",
                256 => "Швейная машинка",
                512 => "Переработчик",
                1024 => "Духовка",
                2048 => "Плита",
                4096 => "Лаборатория электроники",
                8192 => "Оружейная кузница",
                16384 => "Продвинутая кузница",
                32768 => "Винная бочка",
                65536 => "Кофемашина",
                131072 => "Деревянный верстак",
                262144 => "Каменный верстак",
                524288 => "Медный верстак",
                1048576 => "Железный верстак",
                2097152 => "Котёл",
                4194304 => "Аппарат Боба",
                _ => ((HRCraftingFlag)flag).ToString()
            };
        }
        else
        {
            return flag switch
            {
                0 => "Hand Crafting",
                1 => "Hand Crafting",
                2 => "Workbench",
                4 => "Building Blueprint",
                8 => "Furnace / Forge",
                16 => "Farming",
                32 => "Campfire",
                64 => "Sawmill",
                128 => "Charcoal Kiln",
                256 => "Sewing Machine",
                512 => "Refinery",
                1024 => "Oven",
                2048 => "Stove",
                4096 => "Electronics Lab",
                8192 => "Weaponry Anvil",
                16384 => "Advanced Forge",
                32768 => "Wine Barrel",
                65536 => "Coffee Machine",
                131072 => "Wood Crafting Table",
                262144 => "Stone Crafting Table",
                524288 => "Copper Crafting Table",
                1048576 => "Iron Crafting Table",
                2097152 => "Cauldron",
                4194304 => "Bob's Machine",
                _ => ((HRCraftingFlag)flag).ToString()
            };
        }
    }

    private static (string groupKey, string nameRu, string nameEn, bool isBoss)? MatchCharacter(string s)
    {
        // Bosses
        if (s.Contains("Bellstalker"))
            return ("Bellstalker", "Беллсталкер (Босс)", "Bellstalker (Boss)", true);
        if (s.Contains("Zena"))
            return ("Zena", "Зена (Босс)", "Zena (Boss)", true);
        if (s.Contains("GiantMonkey") || s.Contains("Apex_Bonehead"))
            return ("GiantMonkey", "Гигантская обезьяна (Босс)", "Giant Monkey (Boss)", true);
        if (s.Contains("BossAnimal_Tier1_Boar"))
            return ("BossBoar", "Вепрь-вожак (Босс)", "Boar Leader (Boss)", true);
        if (s.Contains("Crab_LargeBoss"))
            return ("BossCrab", "Карракс (Босс)", "Karrax Crab (Boss)", true);
        if (s.Contains("Gulper_Matriarch"))
            return ("BossGulper", "Матка гульперов (Босс)", "Gulper Matriarch (Boss)", true);
        if (s.Contains("BossCowboy"))
            return ("BossCowboy", "Главарь ковбоев (Босс)", "Cowboy Boss", true);
        if (s.Contains("BossRonin"))
            return ("BossRonin", "Главарь ронинов (Босс)", "Ronin Boss", true);
        if (s.Contains("Ronin_Sifu"))
            return ("RoninSifu", "Шифу (Босс)", "Sifu (Boss)", true);

        // Animals
        if (s.Contains("Boar"))
            return ("Boar", "Кабаны", "Boars", false);
        if (s.Contains("Wolf"))
            return ("Wolf", "Волки", "Wolves", false);
        if (s.Contains("Chicken"))
            return ("Chicken", "Куры", "Chickens", false);
        if (s.Contains("Duck"))
            return ("Duck", "Утки", "Ducks", false);
        if (s.Contains("Cow"))
            return ("Cow", "Коровы", "Cows", false);
        if (s.Contains("Rabbit"))
            return ("Rabbit", "Кролики", "Rabbits", false);
        if (s.Contains("Frog"))
            return ("Frog", "Лягушки", "Frogs", false);
        if (s.Contains("Crab"))
            return ("Crab", "Крабы", "Crabs", false);
        if (s.Contains("Shark"))
            return ("Shark", "Акулы", "Sharks", false);
        if (s.Contains("Gulper"))
            return ("Gulper", "Гульперы", "Gulpers", false);
        if (s.Contains("Bird") || s.Contains("Seagull"))
            return ("Bird", "Птицы", "Birds", false);

        // Fish (Fishing / World drops)
        if (s.Contains("KoiFish"))
            return ("KoiFish", "Карп кои (рыбалка)", "Koi Fish (Fishing)", false);
        if (s.Contains("Salmon"))
            return ("Salmon", "Лосось (рыбалка)", "Salmon (Fishing)", false);
        if (s.Contains("Seabass"))
            return ("Seabass", "Морской окунь (рыбалка)", "Seabass (Fishing)", false);
        if (s.Contains("Squid"))
            return ("Squid", "Кальмар (рыбалка)", "Squid (Fishing)", false);
        if (s.Contains("Pufferfish"))
            return ("Pufferfish", "Рыба-фугу (рыбалка)", "Pufferfish (Fishing)", false);

        // Factions
        if (s.Contains("Cultist"))
            return ("Cultist", "Культисты", "Cultists", false);
        if (s.Contains("Bellcoat") || s.Contains("Bellsworn") || s.Contains("ApexHunter"))
            return ("Bellcoat", "Служители Колокола", "Bellcoat Servants", false);
        if (s.Contains("Ronin") || s.Contains("Samurai"))
            return ("Ronin", "Ронины / Самураи", "Ronin / Samurai", false);
        if (s.Contains("Ranger"))
            return ("Ranger", "Рейнджеры", "Rangers", false);
        if (s.Contains("Shepherd"))
            return ("Shepherd", "Пастыри", "Shepherds", false);
        if (s.Contains("Knight"))
            return ("Knight", "Рыцари", "Knights", false);
        if (s.Contains("KazaiVillager") || s.Contains("Customer"))
            return ("Villager", "Жители Казаи", "Kazai Villagers", false);
        if (s.Contains("JessBro") || s.Contains("JessSis"))
            return ("JessFamily", "Семья Джесс", "Jess Family", false);
        if (s.Contains("Cowboy") || s.Contains("Gunner") || s.Contains("Henchman"))
            return ("Cowboy", "Ковбои / Бандиты", "Cowboys / Bandits", false);

        return null;
    }

    private static (string groupKey, string nameRu, string nameEn, bool isBoss)? MatchResource(string s)
    {
        if (s.Contains("Bamboo"))
            return ("Bamboo", "Бамбук", "Bamboo", false);
        if (s.Contains("Birch"))
            return ("Birch", "Берёза", "Birch Tree", false);
        if (s.Contains("Pine") || s.Contains("Pinon"))
            return ("Pine", "Сосна", "Pine Tree", false);
        if (s.Contains("Maple"))
            return ("Maple", "Клён", "Maple Tree", false);
        if (s.Contains("Oak"))
            return ("Oak", "Дуб", "Oak Tree", false);
        if (s.Contains("Spruce"))
            return ("Spruce", "Ель", "Spruce Tree", false);
        if (s.Contains("CherryBlossom"))
            return ("CherryBlossom", "Сакура", "Cherry Blossom", false);
        if (s.Contains("Coconut"))
            return ("Coconut", "Кокосовая пальма", "Coconut Palm", false);
        if (s.Contains("Banana"))
            return ("Banana", "Банановая пальма", "Banana Tree", false);
        if (s.Contains("Cactus"))
            return ("Cactus", "Кактус", "Cactus", false);

        if (s.Contains("CopperRock"))
            return ("CopperRock", "Медная жила", "Copper Ore Vein", false);
        if (s.Contains("IronRock"))
            return ("IronRock", "Железная жила", "Iron Ore Vein", false);
        if (s.Contains("GoldRock"))
            return ("GoldRock", "Золотая жила", "Gold Ore Vein", false);
        if (s.Contains("StoneRock") || s.Contains("SandRock") || s.Contains("RedRock") || s.Contains("RedStone") || s.Contains("RockSalvage"))
            return ("StoneRock", "Каменное месторождение", "Stone Deposit", false);

        if (s.Contains("AmmoCrate") || s.Contains("AmmBox") || s.Contains("Crate_Ammo"))
            return ("AmmoCrate", "Ящик боеприпасов", "Ammo Crate", false);
        if (s.Contains("Salvage") || s.Contains("Outpost") || s.Contains("VaseDecor") || s.Contains("VaseOrnate") || s.Contains("VaseStorage"))
            return ("Salvage", "Обломки / Руины", "Salvage / Ruins", false);

        if (s.Contains("FiberBush") || s.Contains("Shrub") || s.Contains("Bush"))
            return ("FiberBush", "Кустарник / Волокна", "Fiber Bush", false);
        if (s.Contains("GlowMushroom"))
            return ("GlowMushroom", "Светящиеся грибы", "Glow Mushrooms", false);

        if (s.Contains("Wheat"))
            return ("Wheat", "Пшеница (урожай)", "Wheat (Harvest)", false);
        if (s.Contains("Tomato"))
            return ("Tomato", "Томаты (урожай)", "Tomato (Harvest)", false);
        if (s.Contains("Corn"))
            return ("Corn", "Кукуруза (урожай)", "Corn (Harvest)", false);
        if (s.Contains("Grape"))
            return ("Grape", "Виноград (урожай)", "Grape (Harvest)", false);
        if (s.Contains("Cabbage"))
            return ("Cabbage", "Капуста (урожай)", "Cabbage (Harvest)", false);
        if (s.Contains("Rice"))
            return ("Rice", "Рис (урожай)", "Rice (Harvest)", false);
        if (s.Contains("Berrybush"))
            return ("Berrybush", "Ягодный куст", "Berry Bush", false);

        if (s.Contains("Tree") || s.Contains("Stump") || s.Contains("Log") || s.Contains("Sproutling"))
            return ("Tree", "Деревья", "Trees", false);

        return null;
    }

    private static (string groupKey, string nameRu, string nameEn, bool isBoss) ResolveSourceInfo(string raw, bool isCharacter)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("Unknown", "Неизвестно", "Unknown", false);
        string s = raw.Trim();

        var res = isCharacter ? MatchCharacter(s) : MatchResource(s);
        if (res.HasValue) return res.Value;

        res = isCharacter ? MatchResource(s) : MatchCharacter(s);
        if (res.HasValue) return res.Value;

        string clean = s;
        if (clean.StartsWith("PF_")) clean = clean.Substring(3);
        clean = clean.Replace("_Variant", "").Replace(" Variant", "").Replace("_Mineable", "").Replace('_', ' ').Trim();
        return (clean, clean, clean, false);
    }

    public static string BeautifyDropSource(string raw, bool ru)
    {
        var info = ResolveSourceInfo(raw, false);
        return ru ? info.nameRu : info.nameEn;
    }

    private static void AddDropSource(int dropId, string rawSource, float chance, int minAmt, int maxAmt, bool isCharacter)
    {
        if (dropId <= 0) return;
        var info = ResolveSourceInfo(rawSource, isCharacter);

        if (!_itemDropSources.TryGetValue(dropId, out var list))
        {
            list = new List<JeiItemDropSource>();
            _itemDropSources[dropId] = list;
        }

        var existing = list.FirstOrDefault(d => d.GroupKey == info.groupKey);
        if (existing != null)
        {
            if (chance > 0f)
            {
                if (existing.MinChance <= 0f) existing.MinChance = chance;
                else existing.MinChance = Mathf.Min(existing.MinChance, chance);
                existing.MaxChance = Mathf.Max(existing.MaxChance, chance);
            }
            if (minAmt > 0)
                existing.MinAmount = existing.MinAmount <= 0 ? minAmt : Mathf.Min(existing.MinAmount, minAmt);
            if (maxAmt > 0)
                existing.MaxAmount = Mathf.Max(existing.MaxAmount, maxAmt);
            existing.IsBoss |= info.isBoss;
        }
        else
        {
            list.Add(new JeiItemDropSource
            {
                GroupKey = info.groupKey,
                NameRu = info.nameRu,
                NameEn = info.nameEn,
                MinChance = chance,
                MaxChance = chance,
                MinAmount = minAmt,
                MaxAmount = maxAmt,
                IsBoss = info.isBoss
            });
        }
    }

    private static void ParseSingleDropCsv(string path, bool isCharacter)
    {
        if (!File.Exists(path)) return;
        string currentSource = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var p = raw.Split(',');
            if (p.Length > 0 && p[0].Trim().Length > 0 && p[0].Trim()[0] != '~')
            {
                currentSource = p[0].Trim();
                continue;
            }

            if (p.Length > 8 && int.TryParse(p[6].Trim(), out int dropId) && currentSource != null)
            {
                float.TryParse(p[8].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float chance);
                int minAmt = 0;
                int maxAmt = 0;
                if (p.Length > 9) int.TryParse(p[9].Trim(), out minAmt);
                if (p.Length > 10) int.TryParse(p[10].Trim(), out maxAmt);

                AddDropSource(dropId, currentSource, chance, minAmt, maxAmt, isCharacter);
            }
        }
    }

    private static void ParseAllDropCsvs()
    {
        try
        {
            _itemDropSources.Clear();
            string charCsv = Path.Combine(Application.streamingAssetsPath, "Databases", "MineableCharacterCSV.csv");
            ParseSingleDropCsv(charCsv, true);

            string resCsv = Path.Combine(Application.streamingAssetsPath, "Databases", "MineableResourceCSV.csv");
            ParseSingleDropCsv(resCsv, false);

            _log?.LogInfo($"[JEI] Drop CSVs parsed: {_itemDropSources.Count} drop items mapped with rich source info.");
        }
        catch (Exception e)
        {
            _log?.LogWarning($"[JEI] CSV parse failed: {e.Message}");
        }
    }

    private static TMP_FontAsset LoadFont() => UiKit.LoadFont();
}
