using System;
using System.Collections.Generic;
using System.IO;
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
    public List<int> AssociatedFoodItemIDs = new();

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
                if (!string.IsNullOrEmpty(DescriptionEn)) return DescriptionEn;
                if (!string.IsNullOrEmpty(DescriptionRu)) return DescriptionRu;
            }
            return JeiLoc.AttrNoDescription;
        }
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

    public List<string> GetLocalizedWorldDrops()
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
    private static readonly Dictionary<int, List<string>> _dropSources = new();
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
        ParseDropCsv();
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
                if (_dropSources.TryGetValue(it.ItemID, out var srcs)) e.RawWorldDrops.AddRange(srcs);
                _items[it.ItemID] = e;
            }
        }

        // Localized item names
        foreach (var e in _items.Values)
            e.Name = HRItemDatabase.GetLocalizedItemNameByID(e.ItemID, e.FallbackName);

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

        // 5. Scan Food & Culinary Recipes from HRFoodDatabase
        ScanFoodDatabase();

        // 6. Scan Item Attributes & Affixes from HRAttributeDatabase
        ScanAttributeDatabase();

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
                        if (affId > 0 && !r.FoodAffixIDs.Contains(affId))
                            r.FoodAffixIDs.Add(affId);
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
                            // Fallback lookup in items by name
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

            if (attrDb?.AttributeInfos == null)
            {
                _log?.LogWarning("[JEI] AttributeDB or AttributeInfos not found.");
                return;
            }

            _log?.LogInfo($"[JEI] Scanning {attrDb.AttributeInfos.Length} attributes from MasterAttributeDB...");

            foreach (var ai in attrDb.AttributeInfos)
            {
                if (ai == null || ai.ID <= 0) continue;

                var entry = new JeiAttributeEntry
                {
                    ID = ai.ID,
                    StringID = ai.StringID,
                    TitleEn = ai.Title,
                    TitleColor = ai.TitleColor.a > 0.05f ? ai.TitleColor : Color.white,
                    Icon = ai.Icon
                };

                // Formatted description in English
                string descFormatEn = ai.DescriptionFormat;
                if (!string.IsNullOrEmpty(descFormatEn))
                {
                    if (ai.DescriptionsVar != null && ai.DescriptionsVar.Length > 0)
                    {
                        try
                        {
                            var args = new object[ai.DescriptionsVar.Length];
                            for (int i = 0; i < ai.DescriptionsVar.Length; i++) args[i] = ai.DescriptionsVar[i];
                            entry.DescriptionEn = string.Format(descFormatEn, args);
                        }
                        catch (Exception) { entry.DescriptionEn = descFormatEn; }
                    }
                    else
                    {
                        entry.DescriptionEn = descFormatEn;
                    }
                }

                // Russian / localized title and description via game's localization
                try
                {
                    if (attrDb.GetLocalizedTitleDesSplit(ai, out string locTitle, out var locDescs))
                    {
                        if (!string.IsNullOrEmpty(locTitle)) entry.TitleRu = locTitle;
                        if (locDescs != null && locDescs.Length > 0)
                        {
                            entry.DescriptionRu = string.Join(" ", locDescs);
                        }
                    }
                }
                catch (Exception) { }

                if (string.IsNullOrEmpty(entry.TitleRu)) entry.TitleRu = entry.TitleEn;
                if (string.IsNullOrEmpty(entry.DescriptionRu)) entry.DescriptionRu = entry.DescriptionEn;

                // Determine category
                string searchKey = ((entry.StringID ?? "") + " " + (entry.TitleEn ?? "") + " " + (entry.DescriptionEn ?? "")).ToLowerInvariant();
                if (searchKey.Contains("damage") || searchKey.Contains("attack") || searchKey.Contains("crit") || searchKey.Contains("fire") || searchKey.Contains("burn") || searchKey.Contains("ice") || searchKey.Contains("freeze") || searchKey.Contains("shock") || searchKey.Contains("poison") || searchKey.Contains("bleed") || searchKey.Contains("pierce"))
                {
                    entry.Category = "Combat";
                }
                else if (searchKey.Contains("defense") || searchKey.Contains("armor") || searchKey.Contains("health") || searchKey.Contains("shield") || searchKey.Contains("resist") || searchKey.Contains("guard") || searchKey.Contains("block") || searchKey.Contains("heal") || searchKey.Contains("regen"))
                {
                    entry.Category = "Defense";
                }
                else if (searchKey.Contains("food") || searchKey.Contains("hunger") || searchKey.Contains("satiat") || searchKey.Contains("hydrat") || searchKey.Contains("thirst") || searchKey.Contains("taste") || searchKey.Contains("dish") || searchKey.Contains("meal"))
                {
                    entry.Category = "Food";
                }
                else
                {
                    entry.Category = "Utility";
                }

                _attributes.Add(entry);
                _attributesById[entry.ID] = entry;
            }

            // Link attributes to meals that grant them
            foreach (var item in _items.Values)
            {
                foreach (var r in item.Recipes)
                {
                    if (r.IsCooking && r.FoodAffixIDs != null)
                    {
                        foreach (int affId in r.FoodAffixIDs)
                        {
                            if (_attributesById.TryGetValue(affId, out var attrEntry))
                            {
                                if (!attrEntry.AssociatedFoodItemIDs.Contains(item.ItemID))
                                    attrEntry.AssociatedFoodItemIDs.Add(item.ItemID);
                            }
                        }
                    }
                }
            }

            _attributes.Sort((a, b) => a.ID.CompareTo(b.ID));
            _log?.LogInfo($"[JEI] Attributes loaded: {_attributes.Count} entries.");
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

    public static string BeautifyDropSource(string raw, bool ru)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        string s = raw.Trim();
        if (s.StartsWith("PF_")) s = s.Substring(3);
        s = s.Replace("_Variant", "").Replace(" Variant", "").Replace("_Mineable", "");

        if (ru)
        {
            if (s.Contains("Bamboo")) return "Бамбук (дерево)";
            if (s.Contains("Birch")) return "Берёза";
            if (s.Contains("Pine") || s.Contains("Pinon")) return "Сосна";
            if (s.Contains("Maple")) return "Клён";
            if (s.Contains("Oak")) return "Дуб";
            if (s.Contains("Spruce")) return "Ель";
            if (s.Contains("CherryBlossom")) return "Сакура";
            if (s.Contains("Coconut")) return "Кокосовая пальма";
            if (s.Contains("Banana")) return "Банановая пальма";
            if (s.Contains("Cactus")) return "Кактус";
            if (s.Contains("FiberBush") || s.Contains("Shrub") || s.Contains("Bush")) return "Кустарник";
            if (s.Contains("CopperRock")) return "Медная жила";
            if (s.Contains("IronRock")) return "Железная жила";
            if (s.Contains("GoldRock")) return "Золотая жила";
            if (s.Contains("StoneRock") || s.Contains("SandRock") || s.Contains("RedRock") || s.Contains("RockSalvage")) return "Каменная порода";
            if (s.Contains("AmmoCrate") || s.Contains("AmmBox") || s.Contains("Crate_Ammo")) return "Ящик боеприпасов";
            if (s.Contains("StoreSalvage") || s.Contains("WoodSalvage") || s.Contains("Outpost")) return "Обломки / Руины";
            if (s.Contains("Tree") || s.Contains("Stump")) return "Дерево";
        }
        else
        {
            if (s.Contains("Bamboo")) return "Bamboo Tree";
            if (s.Contains("Birch")) return "Birch Tree";
            if (s.Contains("Pine") || s.Contains("Pinon")) return "Pine Tree";
            if (s.Contains("Maple")) return "Maple Tree";
            if (s.Contains("Oak")) return "Oak Tree";
            if (s.Contains("Spruce")) return "Spruce Tree";
            if (s.Contains("CherryBlossom")) return "Cherry Blossom";
            if (s.Contains("Coconut")) return "Coconut Tree";
            if (s.Contains("Banana")) return "Banana Tree";
            if (s.Contains("Cactus")) return "Cactus";
            if (s.Contains("FiberBush") || s.Contains("Shrub") || s.Contains("Bush")) return "Fiber Bush";
            if (s.Contains("CopperRock")) return "Copper Ore Vein";
            if (s.Contains("IronRock")) return "Iron Ore Vein";
            if (s.Contains("GoldRock")) return "Gold Ore Vein";
            if (s.Contains("StoneRock") || s.Contains("SandRock") || s.Contains("RedRock") || s.Contains("RockSalvage")) return "Stone Deposit";
            if (s.Contains("AmmoCrate") || s.Contains("AmmBox") || s.Contains("Crate_Ammo")) return "Ammo Crate";
            if (s.Contains("StoreSalvage") || s.Contains("WoodSalvage") || s.Contains("Outpost")) return "Salvage / Ruins";
            if (s.Contains("Tree") || s.Contains("Stump")) return "Tree";
        }

        return s.Replace('_', ' ');
    }

    private static void ParseDropCsv()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Databases", "MineableResourceCSV.csv");
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
                if (p.Length > 6 && int.TryParse(p[6].Trim(), out int dropId) && currentSource != null)
                {
                    if (!_dropSources.TryGetValue(dropId, out var list)) { list = new List<string>(); _dropSources[dropId] = list; }
                    if (!list.Contains(currentSource)) list.Add(currentSource);
                }
            }
            _log?.LogInfo($"[JEI] Drop CSV parsed: {_dropSources.Count} drop items mapped.");
        }
        catch (Exception e)
        {
            _log?.LogWarning($"[JEI] CSV parse failed: {e.Message}");
        }
    }

    private static TMP_FontAsset LoadFont() => UiKit.LoadFont();
}
