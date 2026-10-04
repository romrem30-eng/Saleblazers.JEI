using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using TMPro;
using UnityEngine;

namespace Saleblazers.ModBase;

public class JeiRecipe
{
    public int ResultItemID;
    public int NumToCraft;
    public float TimeToCraft;
    public int StationFlag;
    public int StationItemID;
    public string StationName;
    public List<KeyValuePair<int, int>> Ingredients = new(); // itemID, amount
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

public class JeiItemEntry
{
    public int ItemID;
    public string Name;
    public string FallbackName;
    public float BaseValue;
    public float CraftedValue;
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
/// JEI catalog: scans HRItemDatabase.ItemArray + HRCraftingDatabase.CraftableItems + HRSkillTree
/// and builds ItemsById / Recipes / Usages / Station links / Research unlock requirements.
/// </summary>
internal static class JeiCatalog
{
    private static ManualLogSource _log;
    private static TMP_FontAsset _font;
    private static bool _scanned;
    private static readonly Dictionary<int, JeiItemEntry> _items = new();
    private static readonly Dictionary<int, List<string>> _dropSources = new();
    private static readonly Dictionary<int, int> _stationItemIds = new();

    public static TMP_FontAsset Font => _font != null ? _font : (_font = LoadFont());
    public static bool Scanned => _scanned;
    public static IReadOnlyDictionary<int, JeiItemEntry> Items => _items;

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

        // ---- items ----
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

        // ---- localized names ----
        foreach (var e in _items.Values)
            e.Name = HRItemDatabase.GetLocalizedItemNameByID(e.ItemID, e.FallbackName);

        // ---- station ItemIDs from CraftingTableReferences ----
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
                    }
                }
                catch (Exception) { }
            }
        }
        PopulateFallbackStationIds();

        // ---- recipes (R) + usages (U) + basic unlock flags ----
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

                // Check UnlockIDs: in HRCraftingInfo, UnlockIDs are items unlocked by crafting/researching this item
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

                    // Also link recipe to the crafting station's Usages so viewing a Workbench shows what it crafts!
                    if (stnItemId > 0 && _items.TryGetValue(stnItemId, out var stnItem) && !stnItem.Usages.Contains(r))
                    {
                        stnItem.Usages.Add(r);
                    }
                }
            }
        }

        // ---- Scan Research / Skill Trees (ResearchBenchTreeSO + any loaded HRSkillTree) ----
        ScanSkillTrees(craftingDb?.ResearchBenchTreeSO);

        _scanned = true;
        _log?.LogInfo($"[JEI] Catalog: {_items.Count} items, R/U & Research indexes built.");
        return true;
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

            // Build requirement description in both RU and EN
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
