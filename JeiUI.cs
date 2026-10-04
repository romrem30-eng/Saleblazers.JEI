using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Saleblazers.ModBase;

/// <summary>
/// JEI / Merchant Toolkit UI:
/// - Items mode: Catalog grid with search, category filters (Weapons, Armor, Food, Materials, Building, Stations, Consumables),
///   interactive Recipe/Usage detail tabs with culinary stats (vessel, satiation, hydration, affixes), and dual TMI spawner.
/// - Attributes mode: Attribute codex browsing all 200+ game affixes/perks by category (Combat, Defense, Food, Utility)
///   with detailed effect descriptions and interactive links to meals/items.
/// - Live bilingual RU/EN language switcher.
/// </summary>
internal static class JeiUI
{
    private static BepInEx.Logging.ManualLogSource _log;
    private static ConfigEntry<bool> _enableJei;
    private static ConfigEntry<bool> _enableTmi;
    private static ConfigEntry<bool> _enablePriceTooltip;
    private static ConfigEntry<int> _spawnCount;
    private static ConfigEntry<int> _gridCols;
    private static ConfigEntry<int> _gridRows;

    private static TMP_FontAsset _font;
    private static Canvas _canvas;
    private static RectTransform _panel;
    private static TMP_Text _titleText;
    private static TMP_Text _langBtnText;
    private static TMP_Text _modeItemsBtnText;
    private static TMP_Text _modeAttrsBtnText;
    private static Image _modeItemsBtnImg;
    private static Image _modeAttrsBtnImg;

    private static TMP_Text _searchLblText;
    private static TMP_InputField _search;
    private static TMP_Text _countText;
    private static RectTransform _filterBar;
    private static RectTransform _gridContainer;
    private static TMP_Text _pageText;
    private static TMP_Text _closeBtnText;

    // Detail panel
    private static RectTransform _detail;
    private static Image _detailIcon;
    private static Image _detailIconBg;
    private static TMP_Text _detailHeaderText;
    private static TMP_Text _detailMetaText;
    private static TMP_Text _recipeSectionTitle;
    private static TMP_Text _recipePageText;
    private static RectTransform _recipeListContainer;
    private static TMP_Text _spawn1Text;
    private static TMP_Text _spawnBatchText;
    private static GameObject _spawn1Obj;
    private static GameObject _spawn10Obj;
    private static GameObject _tabRObj;
    private static GameObject _tabUObj;
    private static Image _tabRImg;
    private static TMP_Text _tabRTxt;
    private static Image _tabUImg;
    private static TMP_Text _tabUTxt;

    private static readonly List<Image> _cellBgs = new();
    private static readonly List<Image> _cellIcons = new();
    private static readonly List<TMP_Text> _cellLabels = new();
    private static readonly List<GameObject> _dynamic = new();
    private static readonly List<GameObject> _filterDynamic = new();
    private static readonly List<GameObject> _detailDynamic = new();
    private static readonly List<ValueTuple<RectTransform, Action>> _clickables = new();
    private static readonly List<ValueTuple<RectTransform, Action>> _detailClickables = new();
    private static readonly List<JeiItemEntry> _filteredItems = new();
    private static readonly List<JeiAttributeEntry> _filteredAttrs = new();

    private static Canvas _tipCanvas;
    private static TMP_Text _tipText;
    private static RectTransform _tipRect;

    private static int _mode; // 0 = Items, 1 = Attributes
    private static JeiCategoryFilter _itemFilter = JeiCategoryFilter.All;
    private static string _attrFilter = "All"; // "All", "Combat", "Defense", "Food", "Utility"
    private static int _page;
    private static int _selectedItemId;
    private static int _selectedAttrId;
    private static int _recipeOffset;
    private static int _recipesPerPage = 3;
    private static int _frameCounter;
    private static bool _tabRecipes = true;
    private static bool _built;
    private static bool _visible;
    private static int _hoveredItemId;
    private static int _lastHoverLogged;
    private static string _lastSearch = "";

    public static bool IsVisible => _visible;

    public static void Init(ConfigFile cfg, BepInEx.Logging.ManualLogSource log)
    {
        _log = log;
        UiKit.SetLogger(log);
        JeiLoc.Init(cfg);
        _enableJei = cfg.Bind("JEI", "EnableJEI", true, "Enable the JEI catalog panel (J key).");
        _enableTmi = cfg.Bind("JEI", "EnableTMI", true, "Enable the TMI item spawner in the detail panel.");
        _enablePriceTooltip = cfg.Bind("JEI", "EnablePriceTooltip", true, "Show base/crafted value tooltip over inventory slots.");
        _spawnCount = cfg.Bind("JEI", "SpawnCount", 10, "Default batch amount for the second TMI spawn button.");
        _gridCols = cfg.Bind("JEI", "GridCols", 8, "Catalog grid columns.");
        _gridRows = cfg.Bind("JEI", "GridRows", 6, "Catalog grid rows.");
    }

    public static void ForceClose()
    {
        _visible = false;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        HidePriceTooltip();
    }

    public static void HandleInput()
    {
        try
        {
            if (_enableJei != null && !_enableJei.Value)
                return;

            if (!ModService.IsInGameplay)
            {
                if (_visible) ForceClose();
                HidePriceTooltip();
                return;
            }

            if (_visible)
            {
                CursorPatch.EnforceCursorEachFrame();

                if (UiKit.KeyPressed(KeyCode.Escape))
                {
                    Toggle();
                    return;
                }
            }

            if (UiKit.KeyPressed(KeyCode.J) || UiKit.KeyPressed(KeyCode.F8))
            {
                Toggle();
                return;
            }

            _frameCounter++;
            if ((_frameCounter & 0x7) == 0) RefreshHover();

            if (_enablePriceTooltip.Value) UpdatePriceTooltip();
            else HidePriceTooltip();

            if (_visible)
            {
                bool typingSearch = _search != null && _search.isFocused;
                if (!typingSearch && _mode == 0 && UiKit.KeyPressed(KeyCode.R) && _selectedItemId > 0)
                {
                    _tabRecipes = true;
                    _recipeOffset = 0;
                    RefreshDetail();
                }
                if (!typingSearch && _mode == 0 && UiKit.KeyPressed(KeyCode.U) && _selectedItemId > 0)
                {
                    _tabRecipes = false;
                    _recipeOffset = 0;
                    RefreshDetail();
                }
                HandleClicks();
                HandleScroll();
                PollSearch();
            }
            else
            {
                if (UiKit.KeyPressed(KeyCode.R) && _hoveredItemId > 0) { OpenFor(_hoveredItemId, true); }
                if (UiKit.KeyPressed(KeyCode.U) && _hoveredItemId > 0) { OpenFor(_hoveredItemId, false); }
            }
        }
        catch (Exception e)
        {
            _log?.LogError($"[JEI] Input error: {e}");
        }
    }

    public static void Toggle()
    {
        if (!ModService.IsInGameplay)
        {
            if (_visible) ForceClose();
            return;
        }

        JeiCatalog.EnsureScanned();
        if (!_built || _canvas == null)
        {
            _built = false;
            Build();
        }
        if (_canvas == null)
        {
            _log?.LogWarning("[JEI] Build failed (canvas null) — panel disabled.");
            _visible = false;
            return;
        }

        _visible = !_visible;
        _canvas.gameObject.SetActive(_visible);
        if (_visible)
        {
            _canvas.enabled = true;
            _canvas.transform.SetAsLastSibling();
            ApplyLanguageToStaticLabels();
            Refresh();
            Canvas.ForceUpdateCanvases();
        }

        CursorPatch.SyncCursorState();
        _log?.LogInfo($"[JEI] Toggle -> visible={_visible}, items={JeiCatalog.Items.Count}, attrs={JeiCatalog.Attributes.Count}");
        if (!_visible) HidePriceTooltip();
    }

    private static void OpenFor(int itemId, bool recipes)
    {
        if (!ModService.IsInGameplay) return;
        JeiCatalog.EnsureScanned();
        if (!JeiCatalog.Items.ContainsKey(itemId)) return;
        if (!_built || _canvas == null)
        {
            _built = false;
            Build();
        }
        if (_canvas == null) return;

        _mode = 0;
        _selectedItemId = itemId;
        _tabRecipes = recipes;
        _recipeOffset = 0;
        _visible = true;
        _canvas.gameObject.SetActive(true);
        _canvas.enabled = true;
        _canvas.transform.SetAsLastSibling();
        ApplyLanguageToStaticLabels();
        Refresh();
        Canvas.ForceUpdateCanvases();
        CursorPatch.SyncCursorState();
    }

    public static void SelectItem(int itemId, bool showRecipes = true)
    {
        if (itemId <= 0 || !JeiCatalog.Items.ContainsKey(itemId)) return;
        _mode = 0;
        _selectedItemId = itemId;
        _tabRecipes = showRecipes;
        _recipeOffset = 0;
        RefreshModeState();
        RefreshGrid();
        RefreshDetail();
    }

    public static void SelectAttribute(int attrId)
    {
        if (attrId <= 0 || !JeiCatalog.AttributesById.ContainsKey(attrId)) return;
        _mode = 1;
        _selectedAttrId = attrId;
        RefreshModeState();
        RefreshGrid();
        RefreshDetail();
    }

    private static void SwitchMode(int newMode)
    {
        if (_mode == newMode) return;
        _mode = newMode;
        _page = 0;
        if (_search != null) _search.text = "";
        RefreshModeState();
        RefreshGrid();
        RefreshDetail();
    }

    private static void RefreshModeState()
    {
        if (_modeItemsBtnImg != null)
            _modeItemsBtnImg.color = _mode == 0 ? new Color(0.24f, 0.48f, 0.36f, 0.98f) : new Color(0.16f, 0.18f, 0.24f, 0.95f);
        if (_modeAttrsBtnImg != null)
            _modeAttrsBtnImg.color = _mode == 1 ? new Color(0.24f, 0.48f, 0.36f, 0.98f) : new Color(0.16f, 0.18f, 0.24f, 0.95f);

        bool isItems = _mode == 0;
        if (_spawn1Obj != null) _spawn1Obj.SetActive(isItems);
        if (_spawn10Obj != null) _spawn10Obj.SetActive(isItems);
        if (_tabRObj != null) _tabRObj.SetActive(isItems);
        if (_tabUObj != null) _tabUObj.SetActive(isItems);

        RebuildFilterBar();
    }

    private static void OnToggleLanguageClicked()
    {
        JeiLoc.ToggleLanguage();
        ApplyLanguageToStaticLabels();
        RebuildFilterBar();
        Refresh();
        ModService.NotifyInfo(JeiLoc.NotifyLangSwitched);
    }

    private static void ApplyLanguageToStaticLabels()
    {
        if (_titleText != null) _titleText.text = JeiLoc.HeaderTitle;
        if (_langBtnText != null) _langBtnText.text = JeiLoc.LangButton;
        if (_modeItemsBtnText != null) _modeItemsBtnText.text = JeiLoc.ModeCatalog;
        if (_modeAttrsBtnText != null) _modeAttrsBtnText.text = JeiLoc.ModeAttributes;
        if (_searchLblText != null) _searchLblText.text = JeiLoc.SearchLabel;
        if (_spawn1Text != null) _spawn1Text.text = JeiLoc.SpawnOne;
        if (_spawnBatchText != null)
        {
            int batchCount = Mathf.Max(2, _spawnCount != null ? _spawnCount.Value : 10);
            _spawnBatchText.text = JeiLoc.SpawnBatch(batchCount);
        }
        if (_closeBtnText != null) _closeBtnText.text = JeiLoc.CloseButton;
    }

    // ---------- hover & tooltip ----------
    private static void RefreshHover()
    {
        _hoveredItemId = 0;
        var mouse = ReInput.controllers?.Mouse;
        if (mouse == null) return;
        var es = EventSystem.current;
        if (es == null) return;

        var ped = new PointerEventData(es) { position = mouse.screenPosition };
        var results = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
        es.RaycastAll(ped, results);
        foreach (var r in results)
        {
            if (r.gameObject == null) continue;
            var slot = r.gameObject.GetComponentInParent<BaseInventorySlotUI>();
            if (slot?.CurrentWeapon != null)
            {
                _hoveredItemId = slot.CurrentWeapon.ItemID;
                if (_hoveredItemId != _lastHoverLogged)
                {
                    _lastHoverLogged = _hoveredItemId;
                    _log?.LogInfo($"[JEI] Hovered slot item #{_hoveredItemId}");
                }
                break;
            }
        }
    }

    private static void UpdatePriceTooltip()
    {
        if (_hoveredItemId <= 0 || _visible)
        {
            HidePriceTooltip();
            return;
        }

        if (_tipCanvas == null) BuildTooltip();
        if (_tipCanvas == null) return;

        if (!JeiCatalog.Items.TryGetValue(_hoveredItemId, out var entry))
        {
            HidePriceTooltip();
            return;
        }

        _tipCanvas.gameObject.SetActive(true);
        string itemName = entry.DisplayName;
        if (JeiLoc.IsRu)
        {
            _tipText.text = $"<b><color=#F5D76E>{itemName}</color></b>  <color=#8892A6>#{entry.ItemID}</color>\n" +
                            $"База: <color=#7BE082>${entry.BaseValue:F0}</color>   Крафт: <color=#6EC6F5>${entry.CraftedValue:F0}</color>\n" +
                            $"<color=#B8C0D0>[R] Рецепты ({entry.Recipes.Count})   [U] Крафты ({entry.Usages.Count})</color>";
        }
        else
        {
            _tipText.text = $"<b><color=#F5D76E>{itemName}</color></b>  <color=#8892A6>#{entry.ItemID}</color>\n" +
                            $"Base: <color=#7BE082>${entry.BaseValue:F0}</color>   Crafted: <color=#6EC6F5>${entry.CraftedValue:F0}</color>\n" +
                            $"<color=#B8C0D0>[R] Recipes ({entry.Recipes.Count})   [U] Usages ({entry.Usages.Count})</color>";
        }

        Vector2 sp = GetMouseScreenPosition();
        _tipRect.position = sp + new Vector2(18f, -12f);
    }

    private static void HidePriceTooltip()
    {
        if (_tipCanvas != null) _tipCanvas.gameObject.SetActive(false);
    }

    private static Vector2 GetMouseScreenPosition()
    {
        Vector2 sp = Vector2.zero;
        try
        {
            var mouse = ReInput.controllers?.Mouse;
            if (mouse != null) sp = mouse.screenPosition;
        }
        catch (Exception) { }

        if (sp == Vector2.zero)
        {
            try { sp = Input.mousePosition; } catch (Exception) { }
        }
        return sp;
    }

    // ---------- input handling ----------
    private static void HandleClicks()
    {
        bool clicked = false;
        Vector2 sp = Vector2.zero;
        try
        {
            var mouse = ReInput.controllers?.Mouse;
            if (mouse != null && mouse.GetButtonDown(0))
            {
                clicked = true;
                sp = mouse.screenPosition;
            }
        }
        catch (Exception) { }

        if (!clicked)
        {
            try
            {
                if (Input.GetMouseButtonDown(0))
                {
                    clicked = true;
                    sp = Input.mousePosition;
                }
            }
            catch (Exception) { }
        }

        if (!clicked) return;

        // Detail panel interactive elements (links, buttons)
        for (int i = 0; i < _detailClickables.Count; i++)
        {
            var dc = _detailClickables[i];
            if (dc.Item1 == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(dc.Item1, sp, null))
            {
                try { dc.Item2(); }
                catch (Exception ex) { _log?.LogError($"[JEI] Detail click error: {ex}"); }
                return;
            }
        }

        // Static & grid buttons
        for (int i = 0; i < _clickables.Count; i++)
        {
            var c = _clickables[i];
            if (c.Item1 == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(c.Item1, sp, null))
            {
                try { c.Item2(); }
                catch (Exception ex) { _log?.LogError($"[JEI] Click action error: {ex}"); }
                return;
            }
        }
    }

    private static void HandleScroll()
    {
        float dy = 0f;
        try
        {
            var mouse = ReInput.controllers?.Mouse;
            if (mouse != null) dy = mouse.GetAxisRaw(2);
        }
        catch (Exception) { }

        if (Mathf.Abs(dy) < 0.01f)
        {
            try { dy = Input.mouseScrollDelta.y; }
            catch (Exception) { }
        }

        if (Mathf.Abs(dy) < 0.01f) return;

        Vector2 sp = GetMouseScreenPosition();
        if (_detail != null && RectTransformUtility.RectangleContainsScreenPoint(_detail, sp, null))
        {
            _recipeOffset += dy > 0 ? -1 : 1;
            RefreshDetail();
        }
        else
        {
            _page += dy > 0 ? -1 : 1;
            RefreshGrid();
        }
    }

    private static void PollSearch()
    {
        if (_search == null) return;
        string t = _search.text;
        if (t != _lastSearch) { _lastSearch = t; _page = 0; RefreshGrid(); }
    }

    // ---------- build ----------
    private static void Build()
    {
        _font = JeiCatalog.Font ?? UiKit.LoadFont();
        if (_font == null)
        {
            _log?.LogError("[JEI] Build aborted: No font could be loaded!");
            return;
        }

        _clickables.Clear();
        _detailClickables.Clear();
        _dynamic.Clear();
        _filterDynamic.Clear();
        _detailDynamic.Clear();
        _cellBgs.Clear();
        _cellIcons.Clear();
        _cellLabels.Clear();

        int cols = Mathf.Clamp(_gridCols.Value, 4, 12);
        int rows = Mathf.Clamp(_gridRows.Value, 3, 8);
        int totalCells = cols * rows;

        _canvas = UiKit.MakeCanvas("JeiCatalog");
        _log?.LogInfo($"[JEI] Canvas built (renderMode={_canvas.renderMode}, sortingOrder={_canvas.sortingOrder})");

        // Dim background
        UiKit.MakeImage(_canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.65f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Main panel (1260 x 730)
        _panel = UiKit.MakeImage(_canvas.transform, "Panel", new Color(0.08f, 0.09f, 0.12f, 0.98f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260, 730)).rectTransform;

        // Header bar (height 44)
        UiKit.MakeImage(_panel, "HeaderBg", new Color(0.13f, 0.15f, 0.22f, 1f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 44));

        _titleText = UiKit.MakeText(_panel, "Title", JeiLoc.HeaderTitle, 18, new Color(0.96f, 0.85f, 0.45f), TextAnchor.MiddleLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -5), new Vector2(400, 34), _font);

        // Mode switch buttons: [ ITEMS ] and [ ATTRIBUTES ]
        var modeItemsBtn = UiKit.MakeClickable(_panel, "ModeItems", JeiLoc.ModeCatalog, () => SwitchMode(0),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(440, -6), new Vector2(120, 32), _font, _clickables);
        _modeItemsBtnImg = modeItemsBtn.GetComponent<Image>();
        _modeItemsBtnImg.color = new Color(0.24f, 0.48f, 0.36f, 0.98f);
        _modeItemsBtnText = modeItemsBtn.GetChild(0)?.GetComponent<TMP_Text>();
        if (_modeItemsBtnText != null) _modeItemsBtnText.fontSize = 12;

        var modeAttrsBtn = UiKit.MakeClickable(_panel, "ModeAttrs", JeiLoc.ModeAttributes, () => SwitchMode(1),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(570, -6), new Vector2(130, 32), _font, _clickables);
        _modeAttrsBtnImg = modeAttrsBtn.GetComponent<Image>();
        _modeAttrsBtnImg.color = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        _modeAttrsBtnText = modeAttrsBtn.GetChild(0)?.GetComponent<TMP_Text>();
        if (_modeAttrsBtnText != null) _modeAttrsBtnText.fontSize = 12;

        // Language toggle button in top right
        var langBtn = UiKit.MakeClickable(_panel, "LangSwitch", JeiLoc.LangButton, OnToggleLanguageClicked,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -6), new Vector2(115, 32), _font, _clickables);
        langBtn.GetComponent<Image>().color = new Color(0.22f, 0.36f, 0.54f, 0.96f);
        _langBtnText = langBtn.GetChild(0)?.GetComponent<TMP_Text>();
        if (_langBtnText != null) _langBtnText.fontSize = 13;

        // Search input row (y = -52, height = 28)
        _searchLblText = UiKit.MakeText(_panel, "SearchLbl", JeiLoc.SearchLabel, 14, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -52), new Vector2(65, 28), _font);

        var searchImg = UiKit.MakeImage(_panel, "Search", new Color(0.15f, 0.17f, 0.23f, 1f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(85, -52), new Vector2(340, 28));
        var searchGo = searchImg.rectTransform;
        _search = searchGo.gameObject.AddComponent<TMP_InputField>();
        var searchText = UiKit.MakeText(searchGo, "SearchTxt", "", 13, Color.white, TextAnchor.MiddleLeft,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, -4), _font);
        _search.textComponent = searchText;
        _search.text = "";

        var clearBtn = UiKit.MakeClickable(_panel, "ClearSearch", "X", () => { if (_search != null) { _search.text = ""; _page = 0; RefreshGrid(); } },
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(430, -52), new Vector2(28, 28), _font, _clickables);
        clearBtn.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.25f, 0.95f);

        _countText = UiKit.MakeText(_panel, "CountTxt", "", 13, new Color(0.68f, 0.74f, 0.84f), TextAnchor.MiddleRight,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(470, -52), new Vector2(330, 28), _font);

        // Filter bar container (y = -84, height = 26, width = 790)
        _filterBar = UiKit.MakeRect(_panel, "FilterBar",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -84), new Vector2(790, 26));

        // Grid container (y = -116, height = 530, width = 790)
        _gridContainer = UiKit.MakeRect(_panel, "Grid",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -116), new Vector2(790, 530));

        for (int i = 0; i < totalCells; i++)
        {
            int cellIdx = i;
            int col = cellIdx % cols;
            int row = cellIdx / cols;

            var cellBg = UiKit.MakeImage(_gridContainer, "Cell" + cellIdx, new Color(0.15f, 0.17f, 0.22f, 0.96f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 98f, -row * 86f), new Vector2(92, 82));
            var cellRt = cellBg.rectTransform;

            var iconImg = UiKit.MakeImage(cellRt, "Icon", Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(46, 46));
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var lbl = UiKit.MakeText(cellRt, "Label", "", 11, new Color(0.92f, 0.94f, 0.98f), TextAnchor.MiddleCenter,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(-6, 30), _font);
            lbl.overflowMode = TextOverflowModes.Ellipsis;

            _cellBgs.Add(cellBg);
            _cellIcons.Add(iconImg);
            _cellLabels.Add(lbl);
            _clickables.Add(new ValueTuple<RectTransform, Action>(cellRt, () => OnGridCellClicked(cellIdx)));
            _dynamic.Add(cellRt.gameObject);
        }

        // Detail panel (right side: 420 x 604)
        _detail = UiKit.MakeRect(_panel, "Detail",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(820, -52), new Vector2(420, 604));
        UiKit.MakeImage(_detail, "DetailBg", new Color(0.11f, 0.12f, 0.16f, 0.98f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Header card inside detail panel
        UiKit.MakeImage(_detail, "DetailHeaderBg", new Color(0.15f, 0.17f, 0.23f, 1f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(0, 80));

        _detailIconBg = UiKit.MakeImage(_detail, "DetailIconSlot", new Color(0.09f, 0.10f, 0.14f, 1f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -9), new Vector2(62, 62));

        _detailIcon = UiKit.MakeImage(_detailIconBg.rectTransform, "DetailIcon", new Color(1f, 1f, 1f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
        _detailIcon.preserveAspect = true;
        _detailIcon.raycastTarget = false;

        _detailHeaderText = UiKit.MakeText(_detail, "DetailHeaderTxt", JeiLoc.SelectItemPrompt, 14, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, -7), new Vector2(324, 66), _font);

        // Meta / Research / Drops block (y = -84, height = 74)
        _detailMetaText = UiKit.MakeText(_detail, "DetailMetaTxt", "", 12, new Color(0.90f, 0.92f, 0.96f), TextAnchor.UpperLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -84), new Vector2(396, 74), _font);
        _detailMetaText.overflowMode = TextOverflowModes.Truncate;

        // Recipe section header bar + scroll buttons (y = -162, height = 28)
        UiKit.MakeImage(_detail, "RecipeBarBg", new Color(0.14f, 0.16f, 0.22f, 0.96f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -162), new Vector2(-16, 28));

        _recipeSectionTitle = UiKit.MakeText(_detail, "RecipeSecTitle", JeiLoc.RecipesDefaultHeader, 13, new Color(0.96f, 0.85f, 0.45f), TextAnchor.MiddleLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -162), new Vector2(250, 28), _font);

        var rPrev = UiKit.MakeClickable(_detail, "RecPrev", "<", () => { _recipeOffset--; RefreshDetail(); },
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-108, -164), new Vector2(28, 24), _font, _clickables);
        rPrev.GetComponent<Image>().color = new Color(0.22f, 0.26f, 0.35f, 0.96f);

        _recipePageText = UiKit.MakeText(_detail, "RecPageTxt", "0/0", 12, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleCenter,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-42, -164), new Vector2(64, 24), _font);

        var rNext = UiKit.MakeClickable(_detail, "RecNext", ">", () => { _recipeOffset++; RefreshDetail(); },
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -164), new Vector2(28, 24), _font, _clickables);
        rNext.GetComponent<Image>().color = new Color(0.22f, 0.26f, 0.35f, 0.96f);

        // Interactive scrollable cards container (y = -194, height = 314)
        _recipeListContainer = UiKit.MakeRect(_detail, "RecipeList",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -194), new Vector2(396, 314));

        // Two Spawn (TMI) buttons
        int batchCount = Mathf.Max(2, _spawnCount.Value);
        var spawn1 = UiKit.MakeClickable(_detail, "Spawn1", JeiLoc.SpawnOne, () => SpawnSelected(1),
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 50), new Vector2(192, 34), _font, _clickables);
        spawn1.GetComponent<Image>().color = new Color(0.20f, 0.48f, 0.28f, 0.96f);
        _spawn1Text = spawn1.GetChild(0)?.GetComponent<TMP_Text>();
        _spawn1Obj = spawn1.gameObject;

        var spawn10 = UiKit.MakeClickable(_detail, "SpawnBatch", JeiLoc.SpawnBatch(batchCount), () => SpawnSelected(batchCount),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 50), new Vector2(192, 34), _font, _clickables);
        spawn10.GetComponent<Image>().color = new Color(0.24f, 0.56f, 0.32f, 0.96f);
        _spawnBatchText = spawn10.GetChild(0)?.GetComponent<TMP_Text>();
        _spawn10Obj = spawn10.gameObject;

        // Tabs inside detail panel (RECIPES / USAGES)
        var tabR = UiKit.MakeClickable(_detail, "TabR", JeiLoc.TabRecipes(), () => { _tabRecipes = true; _recipeOffset = 0; RefreshDetail(); },
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 10), new Vector2(192, 32), _font, _clickables);
        _tabRImg = tabR.GetComponent<Image>();
        _tabRTxt = tabR.GetChild(0)?.GetComponent<TMP_Text>();
        if (_tabRTxt != null) _tabRTxt.fontSize = 12;
        _tabRObj = tabR.gameObject;

        var tabU = UiKit.MakeClickable(_detail, "TabU", JeiLoc.TabUsages(), () => { _tabRecipes = false; _recipeOffset = 0; RefreshDetail(); },
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 10), new Vector2(192, 32), _font, _clickables);
        _tabUImg = tabU.GetComponent<Image>();
        _tabUTxt = tabU.GetChild(0)?.GetComponent<TMP_Text>();
        if (_tabUTxt != null) _tabUTxt.fontSize = 12;
        _tabUObj = tabU.gameObject;

        // Pagination + close at bottom of _panel
        var prev = UiKit.MakeClickable(_panel, "Prev", "<", () => { _page--; RefreshGrid(); },
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 16), new Vector2(56, 34), _font, _clickables);
        prev.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.32f, 0.96f);

        _pageText = UiKit.MakeText(_panel, "Page", "0 / 0", 14, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(84, 16), new Vector2(120, 34), _font);

        var next = UiKit.MakeClickable(_panel, "Next", ">", () => { _page++; RefreshGrid(); },
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(212, 16), new Vector2(56, 34), _font, _clickables);
        next.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.32f, 0.96f);

        var close = UiKit.MakeClickable(_panel, "Close", JeiLoc.CloseButton, () => Toggle(),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 16), new Vector2(180, 34), _font, _clickables);
        close.GetComponent<Image>().color = new Color(0.45f, 0.20f, 0.22f, 0.96f);
        _closeBtnText = close.GetChild(0)?.GetComponent<TMP_Text>();

        _dynamic.Add(prev.gameObject);
        _dynamic.Add(next.gameObject);
        _dynamic.Add(close.gameObject);
        _dynamic.Add(langBtn.gameObject);
        _dynamic.Add(modeItemsBtn.gameObject);
        _dynamic.Add(modeAttrsBtn.gameObject);

        RebuildFilterBar();

        _canvas.gameObject.SetActive(false);
        _built = true;
    }

    private static void RebuildFilterBar()
    {
        if (_filterBar == null) return;

        for (int i = 0; i < _filterDynamic.Count; i++)
        {
            if (_filterDynamic[i] != null)
                UnityEngine.Object.Destroy(_filterDynamic[i]);
        }
        _filterDynamic.Clear();

        if (_mode == 0)
        {
            // Item category buttons: ALL, WEAPONS, ARMOR, FOOD, MATERIALS, BUILDING, STATIONS, CONSUMABLES
            var filters = new (JeiCategoryFilter filter, string label)[]
            {
                (JeiCategoryFilter.All, JeiLoc.CatAll),
                (JeiCategoryFilter.Weapons, JeiLoc.CatWeapons),
                (JeiCategoryFilter.Armor, JeiLoc.CatArmor),
                (JeiCategoryFilter.Food, JeiLoc.CatFood),
                (JeiCategoryFilter.Materials, JeiLoc.CatMaterials),
                (JeiCategoryFilter.Building, JeiLoc.CatBuilding),
                (JeiCategoryFilter.Stations, JeiLoc.CatStations),
                (JeiCategoryFilter.Consumables, JeiLoc.CatConsumables),
            };

            float x = 0f;
            float btnW = 96f;
            float spacing = 3f;

            foreach (var (f, label) in filters)
            {
                var curFilter = f;
                bool active = _itemFilter == curFilter;
                var btn = UiKit.MakeClickable(_filterBar, "Cat_" + curFilter, label, () =>
                {
                    _itemFilter = curFilter;
                    _page = 0;
                    RebuildFilterBar();
                    RefreshGrid();
                }, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 0), new Vector2(btnW, 26), _font, _clickables);

                btn.GetComponent<Image>().color = active
                    ? new Color(0.24f, 0.48f, 0.36f, 0.98f)
                    : new Color(0.14f, 0.16f, 0.22f, 0.95f);

                var txt = btn.GetChild(0)?.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    txt.fontSize = 10;
                    txt.color = active ? new Color(1f, 0.95f, 0.65f) : new Color(0.85f, 0.88f, 0.95f);
                }

                _filterDynamic.Add(btn.gameObject);
                x += btnW + spacing;
            }
        }
        else
        {
            // Attribute category buttons: ALL, COMBAT, DEFENSE, FOOD, UTILITY
            var filters = new (string cat, string label)[]
            {
                ("All", JeiLoc.AttrCatAll),
                ("Combat", JeiLoc.AttrCatCombat),
                ("Defense", JeiLoc.AttrCatDefense),
                ("Food", JeiLoc.AttrCatFood),
                ("Utility", JeiLoc.AttrCatUtility),
            };

            float x = 0f;
            float btnW = 154f;
            float spacing = 4f;

            foreach (var (cat, label) in filters)
            {
                var curCat = cat;
                bool active = string.Equals(_attrFilter, curCat, StringComparison.OrdinalIgnoreCase);
                var btn = UiKit.MakeClickable(_filterBar, "AttrCat_" + curCat, label, () =>
                {
                    _attrFilter = curCat;
                    _page = 0;
                    RebuildFilterBar();
                    RefreshGrid();
                }, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 0), new Vector2(btnW, 26), _font, _clickables);

                btn.GetComponent<Image>().color = active
                    ? new Color(0.24f, 0.48f, 0.36f, 0.98f)
                    : new Color(0.14f, 0.16f, 0.22f, 0.95f);

                var txt = btn.GetChild(0)?.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    txt.fontSize = 11;
                    txt.color = active ? new Color(1f, 0.95f, 0.65f) : new Color(0.85f, 0.88f, 0.95f);
                }

                _filterDynamic.Add(btn.gameObject);
                x += btnW + spacing;
            }
        }
    }

    private static void BuildTooltip()
    {
        if (_font == null) _font = JeiCatalog.Font ?? UiKit.LoadFont();
        if (_font == null) return;

        _tipCanvas = UiKit.MakeCanvas("JeiPriceTip");
        _tipRect = UiKit.MakeImage(_tipCanvas.transform, "TipBg", new Color(0.06f, 0.07f, 0.10f, 0.96f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(280, 72)).rectTransform;
        _tipText = UiKit.MakeText(_tipRect, "TipTxt", "", 13, new Color(0.92f, 0.92f, 0.85f), TextAnchor.MiddleLeft,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, -10), _font);
        _tipCanvas.gameObject.SetActive(false);
    }

    // ---------- refresh ----------
    private static void Refresh()
    {
        JeiCatalog.EnsureScanned();
        RefreshModeState();
        RefreshGrid();
        RefreshDetail();
    }

    private static void RefreshGrid()
    {
        if (_gridContainer == null) return;

        string q = _search != null && _search.text != null ? _search.text.Trim().ToLowerInvariant() : "";

        if (_mode == 0)
        {
            // Items grid
            _filteredItems.Clear();
            foreach (var e in JeiCatalog.Items.Values)
            {
                if (!e.MatchesCategory(_itemFilter)) continue;

                if (q.Length > 0 &&
                    !(e.Name != null && e.Name.ToLowerInvariant().Contains(q)) &&
                    !(e.FallbackName != null && e.FallbackName.ToLowerInvariant().Contains(q)) &&
                    !e.ItemID.ToString().Contains(q))
                    continue;

                _filteredItems.Add(e);
            }
            _filteredItems.Sort((a, b) => a.ItemID.CompareTo(b.ItemID));

            if (_countText != null)
                _countText.text = JeiLoc.ItemCount(_filteredItems.Count);

            int per = _cellBgs.Count;
            if (per == 0) return;

            int maxPage = Mathf.Max(0, (_filteredItems.Count - 1) / per);
            _page = Mathf.Clamp(_page, 0, maxPage);

            for (int i = 0; i < per; i++)
            {
                int dataIdx = _page * per + i;
                var bg = _cellBgs[i];
                var icon = _cellIcons[i];
                var lbl = _cellLabels[i];

                if (dataIdx < _filteredItems.Count)
                {
                    var e = _filteredItems[dataIdx];
                    bool selected = e.ItemID == _selectedItemId;
                    bg.color = selected
                        ? new Color(0.22f, 0.44f, 0.30f, 0.98f)
                        : new Color(0.15f, 0.17f, 0.22f, 0.96f);

                    var sp = e.Icon;
                    if (sp != null)
                    {
                        icon.sprite = sp;
                        icon.color = Color.white;
                    }
                    else
                    {
                        icon.sprite = UiKit.WhiteSprite;
                        icon.color = new Color(1f, 1f, 1f, 0.08f);
                    }

                    lbl.text = e.DisplayName;
                    lbl.color = selected ? new Color(1f, 0.95f, 0.65f) : new Color(0.90f, 0.92f, 0.96f);
                }
                else
                {
                    bg.color = new Color(0.11f, 0.12f, 0.15f, 0.45f);
                    icon.sprite = UiKit.WhiteSprite;
                    icon.color = new Color(1f, 1f, 1f, 0f);
                    lbl.text = "";
                }
            }

            if (_pageText != null)
                _pageText.text = _filteredItems.Count == 0 ? "0 / 0" : $"{_page + 1} / {maxPage + 1}";
        }
        else
        {
            // Attributes grid
            _filteredAttrs.Clear();
            foreach (var a in JeiCatalog.Attributes)
            {
                if (!string.Equals(_attrFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(_attrFilter, a.Category, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (q.Length > 0 &&
                    !(a.Title != null && a.Title.ToLowerInvariant().Contains(q)) &&
                    !(a.StringID != null && a.StringID.ToLowerInvariant().Contains(q)) &&
                    !(a.Description != null && a.Description.ToLowerInvariant().Contains(q)) &&
                    !a.ID.ToString().Contains(q))
                    continue;

                _filteredAttrs.Add(a);
            }
            _filteredAttrs.Sort((a, b) => a.ID.CompareTo(b.ID));

            if (_countText != null)
                _countText.text = JeiLoc.AttrCount(_filteredAttrs.Count);

            int per = _cellBgs.Count;
            if (per == 0) return;

            int maxPage = Mathf.Max(0, (_filteredAttrs.Count - 1) / per);
            _page = Mathf.Clamp(_page, 0, maxPage);

            for (int i = 0; i < per; i++)
            {
                int dataIdx = _page * per + i;
                var bg = _cellBgs[i];
                var icon = _cellIcons[i];
                var lbl = _cellLabels[i];

                if (dataIdx < _filteredAttrs.Count)
                {
                    var a = _filteredAttrs[dataIdx];
                    bool selected = a.ID == _selectedAttrId;
                    bg.color = selected
                        ? new Color(0.22f, 0.36f, 0.52f, 0.98f)
                        : new Color(0.15f, 0.17f, 0.22f, 0.96f);

                    var sp = a.Icon;
                    if (sp != null)
                    {
                        icon.sprite = sp;
                        icon.color = Color.white;
                    }
                    else
                    {
                        icon.sprite = UiKit.WhiteSprite;
                        icon.color = new Color(1f, 1f, 1f, 0.08f);
                    }

                    lbl.text = a.Title;
                    lbl.color = a.TitleColor.a > 0.1f ? a.TitleColor : new Color(0.90f, 0.92f, 0.96f);
                }
                else
                {
                    bg.color = new Color(0.11f, 0.12f, 0.15f, 0.45f);
                    icon.sprite = UiKit.WhiteSprite;
                    icon.color = new Color(1f, 1f, 1f, 0f);
                    lbl.text = "";
                }
            }

            if (_pageText != null)
                _pageText.text = _filteredAttrs.Count == 0 ? "0 / 0" : $"{_page + 1} / {maxPage + 1}";
        }
    }

    private static void OnGridCellClicked(int cellIdx)
    {
        int per = _cellBgs.Count;
        int dataIdx = _page * per + cellIdx;

        if (_mode == 0)
        {
            if (dataIdx >= 0 && dataIdx < _filteredItems.Count)
            {
                _selectedItemId = _filteredItems[dataIdx].ItemID;
                _recipeOffset = 0;
                RefreshDetail();
                RefreshGrid();
            }
        }
        else
        {
            if (dataIdx >= 0 && dataIdx < _filteredAttrs.Count)
            {
                _selectedAttrId = _filteredAttrs[dataIdx].ID;
                _recipeOffset = 0;
                RefreshDetail();
                RefreshGrid();
            }
        }
    }

    private static void ClearDetailDynamic()
    {
        for (int i = 0; i < _detailDynamic.Count; i++)
        {
            if (_detailDynamic[i] != null)
                UnityEngine.Object.Destroy(_detailDynamic[i]);
        }
        _detailDynamic.Clear();
        _detailClickables.Clear();
    }

    private static void RefreshDetail()
    {
        if (_detailHeaderText == null || _detailMetaText == null || _recipeListContainer == null) return;

        ClearDetailDynamic();

        if (_mode == 0)
        {
            RefreshItemDetail();
        }
        else
        {
            RefreshAttributeDetail();
        }
    }

    private static void RefreshItemDetail()
    {
        if (_tabRImg != null)
            _tabRImg.color = _tabRecipes ? new Color(0.22f, 0.44f, 0.65f, 0.98f) : new Color(0.16f, 0.18f, 0.24f, 0.95f);
        if (_tabUImg != null)
            _tabUImg.color = !_tabRecipes ? new Color(0.22f, 0.44f, 0.65f, 0.98f) : new Color(0.16f, 0.18f, 0.24f, 0.95f);

        if (_selectedItemId <= 0 || !JeiCatalog.Items.TryGetValue(_selectedItemId, out var e))
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = UiKit.WhiteSprite;
                _detailIcon.color = new Color(1f, 1f, 1f, 0f);
            }
            if (_tabRTxt != null) _tabRTxt.text = JeiLoc.TabRecipes();
            if (_tabUTxt != null) _tabUTxt.text = JeiLoc.TabUsages();
            _detailHeaderText.text = JeiLoc.SelectItemPrompt;
            _detailMetaText.text = JeiLoc.SelectItemHint;
            _recipeSectionTitle.text = JeiLoc.RecipesDefaultHeader;
            _recipePageText.text = "0/0";
            return;
        }

        if (_tabRTxt != null) _tabRTxt.text = JeiLoc.TabRecipes(e.Recipes.Count);
        if (_tabUTxt != null) _tabUTxt.text = JeiLoc.TabUsages(e.Usages.Count);

        if (_detailIcon != null)
        {
            var sp = e.Icon;
            if (sp != null)
            {
                _detailIcon.sprite = sp;
                _detailIcon.color = Color.white;
            }
            else
            {
                _detailIcon.sprite = UiKit.WhiteSprite;
                _detailIcon.color = new Color(1f, 1f, 1f, 0.08f);
            }
        }

        // Header summary next to icon
        var hsb = new System.Text.StringBuilder();
        hsb.Append("<b><size=16><color=#F5D76E>").Append(e.DisplayName).Append("</color></size></b>  <color=#7E889B>#").Append(e.ItemID).Append("</color>\n");
        hsb.Append("<color=#B0B8C8>").Append(JeiLoc.BasePriceLabel).Append("</color> <color=#7BE082>$").Append(e.BaseValue.ToString("F0")).Append("</color>");
        if (e.CraftedValue > 0)
            hsb.Append("   <color=#B0B8C8>").Append(JeiLoc.CraftedPriceLabel).Append("</color> <color=#6EC6F5>$").Append(e.CraftedValue.ToString("F0")).Append("</color>");
        hsb.AppendLine();
        if (e.Categories.Count > 0)
            hsb.Append("<size=12><color=#8C96A8>").Append(string.Join(" • ", e.Categories)).Append("</color></size>");
        _detailHeaderText.text = hsb.ToString();

        // Meta block: Drops + Research/Unlock requirements
        var msb = new System.Text.StringBuilder();
        var drops = e.GetLocalizedWorldDrops();
        if (drops.Count > 0)
        {
            msb.Append("<color=#8FD694><b>").Append(JeiLoc.DropsLabel).Append("</b></color> ").Append(string.Join(", ", drops)).AppendLine();
        }
        AppendUnlockSummary(msb, e.Unlock);
        _detailMetaText.fontSize = 12;
        _detailMetaText.text = msb.ToString();

        if (e.Unlock != null && e.Unlock.RequiredItemID > 0 && JeiCatalog.Items.TryGetValue(e.Unlock.RequiredItemID, out var reqItem))
        {
            int targetReqId = e.Unlock.RequiredItemID;
            var reqBtn = UiKit.MakeClickable(_detail, "ReqJumpBtn", JeiLoc.GoToItemButton(reqItem.DisplayName), () => SelectItem(targetReqId, true),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -132), new Vector2(165, 22), _font, _detailClickables);
            reqBtn.GetComponent<Image>().color = new Color(0.20f, 0.36f, 0.52f, 0.95f);
            var reqLbl = reqBtn.GetChild(0)?.GetComponent<TMP_Text>();
            if (reqLbl != null) reqLbl.fontSize = 11;
            _detailDynamic.Add(reqBtn.gameObject);
        }

        var list = _tabRecipes ? e.Recipes : e.Usages;
        _recipeSectionTitle.text = _tabRecipes
            ? JeiLoc.HowToCraftHeader(list.Count)
            : JeiLoc.UsedInHeader(list.Count);

        if (list.Count == 0)
        {
            _recipePageText.text = "0/0";
            var emptyTxt = UiKit.MakeText(_recipeListContainer, "EmptyTxt",
                _tabRecipes ? JeiLoc.EmptyRecipesText : JeiLoc.EmptyUsagesText,
                13, Color.white, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -6), new Vector2(380, 40), _font);
            _detailDynamic.Add(emptyTxt.gameObject);
            return;
        }

        _recipeOffset = Mathf.Clamp(_recipeOffset, 0, list.Count - 1);

        float y = 0f;
        float maxHeight = 312f;
        int rendered = 0;

        for (int idx = _recipeOffset; idx < list.Count; idx++)
        {
            var r = list[idx];
            int ingCount = Mathf.Max(1, r.Ingredients.Count);
            JeiCatalog.Items.TryGetValue(r.ResultItemID, out var resItem);
            bool hasUnlockLine = !_tabRecipes && resItem?.Unlock != null && (!resItem.Unlock.StartUnlocked || !string.IsNullOrEmpty(resItem.Unlock.NodeName));
            bool hasCookLine = r.IsCooking && (r.SatiatingSeconds > 0 || r.HydratingSeconds > 0 || r.RegenPercent > 0 || r.FoodAffixIDs.Count > 0);

            float cardHeight = 46f + (hasUnlockLine ? 18f : 0f) + (hasCookLine ? 18f : 0f) + ingCount * 19f;

            if (rendered > 0 && y + cardHeight > maxHeight)
                break;

            BuildRecipeCard(r, resItem, y, cardHeight, hasUnlockLine, hasCookLine);
            y += cardHeight + 6f;
            rendered++;
        }

        _recipesPerPage = Mathf.Max(1, rendered);
        _recipePageText.text = $"{_recipeOffset + 1}-{Mathf.Min(list.Count, _recipeOffset + rendered)}/{list.Count}";
    }

    private static void RefreshAttributeDetail()
    {
        if (_selectedAttrId <= 0 || !JeiCatalog.AttributesById.TryGetValue(_selectedAttrId, out var a))
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = UiKit.WhiteSprite;
                _detailIcon.color = new Color(1f, 1f, 1f, 0f);
            }
            _detailHeaderText.text = JeiLoc.SelectAttrPrompt;
            _detailMetaText.text = JeiLoc.SelectAttrHint;
            _recipeSectionTitle.text = JeiLoc.AttrDefaultHeader;
            _recipePageText.text = "0/0";
            return;
        }

        if (_detailIcon != null)
        {
            var sp = a.Icon;
            if (sp != null)
            {
                _detailIcon.sprite = sp;
                _detailIcon.color = Color.white;
            }
            else
            {
                _detailIcon.sprite = UiKit.WhiteSprite;
                _detailIcon.color = new Color(1f, 1f, 1f, 0.08f);
            }
        }

        // Header summary next to icon
        var hsb = new System.Text.StringBuilder();
        string colHex = ColorUtility.ToHtmlStringRGB(a.TitleColor.a > 0.1f ? a.TitleColor : Color.white);
        hsb.Append("<b><size=16><color=#").Append(colHex).Append(">").Append(a.Title).Append("</color></size></b>\n");
        hsb.Append("<color=#B0B8C8>").Append(JeiLoc.AttrInternalIdLabel).Append("</color> <color=#F5D76E>#").Append(a.ID).Append("</color>");
        if (!string.IsNullOrEmpty(a.StringID))
            hsb.Append(" <color=#7E889B>(").Append(a.StringID).Append(")</color>");
        hsb.AppendLine();
        hsb.Append("<color=#B0B8C8>").Append(JeiLoc.AttrCategoryLabel).Append("</color> <color=#6EC6F5>").Append(a.LocalizedCategory).Append("</color>");
        if (!string.IsNullOrEmpty(a.MinRarity))
            hsb.Append("  |  <color=#B0B8C8>").Append(JeiLoc.AttrRarityLabel).Append("</color> <color=#F5D76E>").Append(a.MinRarity).Append("</color>");
        _detailHeaderText.text = hsb.ToString();

        // Meta block: Full effect description + numeric ranges + triggers + duration + sources
        _detailMetaText.fontSize = 11;
        var msb = new System.Text.StringBuilder();
        msb.Append("<color=#8FD694><b>").Append(JeiLoc.AttrEffectHeader).Append(":</b></color> <color=#FFFFFF>").Append(a.Description).Append("</color>\n");
        if (!string.IsNullOrEmpty(a.ValueRangeText))
        {
            msb.Append("<color=#B0B8C8>").Append(JeiLoc.AttrRangeLabel).Append(" </color><color=#F5D76E>").Append(a.ValueRangeText).Append("</color>  |  ");
        }
        msb.Append("<color=#B0B8C8>").Append(JeiLoc.AttrDurationLabel).Append(" </color><color=#F5D76E>").Append(a.DurationText).Append("</color>\n");

        bool hasMechanics = false;
        if (!string.IsNullOrEmpty(a.AppliesTo))
        {
            msb.Append("<color=#B0B8C8>").Append(JeiLoc.AttrAppliesToLabel).Append(" </color><color=#6EC6F5>").Append(a.AppliesTo).Append("</color>");
            hasMechanics = true;
        }
        if (!string.IsNullOrEmpty(a.TriggerText))
        {
            if (hasMechanics) msb.Append("  |  ");
            msb.Append("<color=#B0B8C8>").Append(JeiLoc.AttrTriggerLabel).Append(" </color><color=#FFAA55>").Append(a.TriggerText).Append("</color>");
            hasMechanics = true;
        }
        if (!string.IsNullOrEmpty(a.SourceName))
        {
            msb.Append("\n<color=#B0B8C8>").Append(JeiLoc.AttrSourceLabel).Append(" </color><color=#E0E5EE>").Append(a.SourceName).Append("</color>");
        }
        _detailMetaText.text = msb.ToString();

        // If no associated meals, show dedicated mechanics and acquisition panel
        if (a.AssociatedFoodItemIDs.Count == 0)
        {
            _recipeSectionTitle.text = JeiLoc.AttrMechanicsHeader;
            _recipePageText.text = "";

            var boxBg = UiKit.MakeImage(_recipeListContainer, "MechanicsBoxBg", new Color(0.14f, 0.16f, 0.22f, 0.95f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(396, 220));
            _detailDynamic.Add(boxBg.gameObject);

            var sbMech = new System.Text.StringBuilder();
            sbMech.Append("<size=13><color=#8FD694><b>").Append(JeiLoc.IsRu ? "КАК ПОЛУЧИТЬ И ИСПОЛЬЗОВАТЬ:" : "HOW TO ACQUIRE & USE:").Append("</b></color></size>\n\n");

            if (!string.IsNullOrEmpty(a.SourceName))
            {
                sbMech.Append("<color=#B0B8C8>").Append(JeiLoc.AttrSourceLabel).Append(" </color><color=#F5D76E><b>").Append(a.SourceName).Append("</b></color>\n\n");
            }
            if (!string.IsNullOrEmpty(a.AppliesTo))
            {
                sbMech.Append("<color=#B0B8C8>").Append(JeiLoc.AttrAppliesToLabel).Append(" </color><color=#6EC6F5>").Append(a.AppliesTo).Append("</color>\n\n");
            }
            if (!string.IsNullOrEmpty(a.TriggerText))
            {
                sbMech.Append("<color=#B0B8C8>").Append(JeiLoc.AttrTriggerLabel).Append(" </color><color=#FFAA55>").Append(a.TriggerText).Append("</color>\n\n");
            }

            sbMech.Append("<color=#A0A8B8>").Append(JeiLoc.AttrNoFoodLinked).Append("</color>");

            UiKit.MakeText(boxBg.rectTransform, "MechanicsDesc", sbMech.ToString(), 12, Color.white, TextAnchor.UpperLeft,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(12, 12), new Vector2(-24, -24), _font);
            return;
        }

        // List of associated meals / food dishes granting this attribute
        _recipeSectionTitle.text = $"{JeiLoc.AttrFoodHeader} ({a.AssociatedFoodItemIDs.Count})";

        _recipeOffset = Mathf.Clamp(_recipeOffset, 0, a.AssociatedFoodItemIDs.Count - 1);

        float y = 0f;
        float maxHeight = 312f;
        int rendered = 0;

        for (int idx = _recipeOffset; idx < a.AssociatedFoodItemIDs.Count; idx++)
        {
            int mealId = a.AssociatedFoodItemIDs[idx];
            if (!JeiCatalog.Items.TryGetValue(mealId, out var mealItem))
                continue;

            float cardHeight = 36f;
            if (rendered > 0 && y + cardHeight > maxHeight)
                break;

            var cardBg = UiKit.MakeImage(_recipeListContainer, "MealCard_" + mealId, new Color(0.14f, 0.16f, 0.21f, 0.96f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(396, cardHeight));
            var cardRt = cardBg.rectTransform;
            _detailDynamic.Add(cardBg.gameObject);

            var mealIcon = UiKit.MakeImage(cardRt, "MealIcon", Color.white,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(28, 28));
            mealIcon.preserveAspect = true;
            if (mealItem.Icon != null) mealIcon.sprite = mealItem.Icon;

            string typeBadge = "";
            if (a.AssociatedIngredientItemIDs.Contains(mealItem.ItemID))
                typeBadge = $"<color=#8FD694>{JeiLoc.AttrIngredientBadge}</color> ";
            else
                typeBadge = $"<color=#F5D76E>{JeiLoc.AttrMealBadge}</color> ";

            string mLine = $"{typeBadge}<b><color=#FFFFFF>{mealItem.DisplayName}</color></b> <color=#7E889B>(#{mealItem.ItemID})</color>";
            var mTxt = UiKit.MakeText(cardRt, "MealTitle", mLine, 12, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(-120, 0), _font);

            int targetMealId = mealItem.ItemID;
            var jumpBtn = UiKit.MakeClickable(cardRt, "JumpBtn", JeiLoc.IsRu ? "В каталог" : "View", () => SelectItem(targetMealId, true),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(85, 24), _font, _detailClickables);
            jumpBtn.GetComponent<Image>().color = new Color(0.20f, 0.44f, 0.30f, 0.95f);

            _detailClickables.Add(new ValueTuple<RectTransform, Action>(mTxt.rectTransform, () => SelectItem(targetMealId, true)));

            y += cardHeight + 5f;
            rendered++;
        }

        _recipesPerPage = Mathf.Max(1, rendered);
        _recipePageText.text = $"{_recipeOffset + 1}-{Mathf.Min(a.AssociatedFoodItemIDs.Count, _recipeOffset + rendered)}/{a.AssociatedFoodItemIDs.Count}";
    }

    private static void AppendUnlockSummary(System.Text.StringBuilder sb, JeiUnlockInfo u)
    {
        if (u == null)
        {
            sb.Append(JeiLoc.ResearchNotRequired);
            return;
        }

        if (u.StartUnlocked && string.IsNullOrEmpty(u.NodeName) && u.Cost <= 0 && u.RequiredItemID <= 0)
        {
            sb.Append(JeiLoc.ResearchStartUnlocked);
            return;
        }

        sb.Append("<color=#6EC6F5><b>").Append(JeiLoc.ResearchLabel).Append("</b></color> ");
        if (!string.IsNullOrEmpty(u.NodeName))
            sb.Append("<b><color=#FFFFFF>").Append(u.NodeName).Append("</color></b>");
        else if (u.QuestUnlocked)
            sb.Append("<color=#F5D76E>").Append(JeiLoc.StoryQuestUnlock).Append("</color>");
        else
            sb.Append("<color=#D8DEE9>").Append(JeiLoc.ResearchTableUnlock).Append("</color>");

        if (u.Cost > 0)
            sb.Append("  <color=#9AA8BC>").Append(JeiLoc.CostLabel).Append("</color> <color=#F5D76E>").Append(u.Cost).Append(" ").Append(JeiLoc.PointsUnit).Append("</color>");
        if (u.SkillPoints > 0 && u.SkillPoints != u.Cost)
            sb.Append("  <color=#9AA8BC>SP:</color> <color=#F5D76E>").Append(u.SkillPoints).Append("</color>");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(u.ParentNodeName))
            sb.Append("<color=#9AA8BC>").Append(JeiLoc.AfterNodeLabel).Append("</color> <color=#D8DEE9>").Append(u.ParentNodeName).Append("</color>   ");

        if (!string.IsNullOrEmpty(u.RequirementText))
        {
            sb.Append("<color=#FFB86C>").Append(JeiLoc.RequiresPrefix).Append(u.RequirementText).Append("</color>");
        }
        else if (u.RequiredItemID > 0)
        {
            string reqName = JeiCatalog.Items.TryGetValue(u.RequiredItemID, out var rie)
                ? rie.DisplayName
                : ("#" + u.RequiredItemID);
            sb.Append("<color=#FFB86C>").Append(JeiLoc.MustResearchOrCraftPrefix).Append(reqName).Append("</color>");
        }
    }

    private static void BuildRecipeCard(JeiRecipe r, JeiItemEntry resItem, float y, float cardHeight, bool hasUnlockLine, bool hasCookLine)
    {
        var cardBg = UiKit.MakeImage(_recipeListContainer, "RecCard", new Color(0.14f, 0.16f, 0.21f, 0.96f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(396, cardHeight));
        var cardRt = cardBg.rectTransform;
        _detailDynamic.Add(cardBg.gameObject);

        string resultName = resItem != null ? resItem.DisplayName : ("#" + r.ResultItemID);
        int count = Mathf.Max(1, r.NumToCraft);

        // 1) Result header
        string titleStr = count > 1
            ? $"• <b><color=#F5D76E>{resultName}</color></b> <color=#7BE082>x{count}</color>  <color=#7E889B>(#{r.ResultItemID})</color>"
            : $"• <b><color=#F5D76E>{resultName}</color></b>  <color=#7E889B>(#{r.ResultItemID})</color>";

        var resTxt = UiKit.MakeText(cardRt, "ResTitle", titleStr, 13, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -3), new Vector2(380, 20), _font);
        int targetResId = r.ResultItemID;
        _detailClickables.Add(new ValueTuple<RectTransform, Action>(resTxt.rectTransform, () => SelectItem(targetResId, true)));

        // 2) Station line
        string station = JeiCatalog.ResolveStationName(r.StationFlag);
        int stationItemId = r.StationItemID > 0 ? r.StationItemID : JeiCatalog.ResolveStationItemId(r.StationFlag);
        string stnFormatted = stationItemId > 0
            ? $"<color=#55C5FF><u>{station}</u></color>"
            : $"<color=#7EC8E3>{station}</color>";

        string stnLabel = r.IsCooking ? JeiLoc.CookingLabel : JeiLoc.StationLabel;
        string vesselPart = r.IsCooking && !string.IsNullOrEmpty(r.VesselType) ? $"   <color=#9AA8BC>{JeiLoc.VesselLabel}</color> <color=#F5D76E>{r.VesselType}</color>" : "";
        string stnLine = $"   <color=#9AA8BC>{stnLabel}</color> {stnFormatted}{vesselPart}   <color=#9AA8BC>{JeiLoc.TimeLabel}</color> <color=#D8DEE9>{r.TimeToCraft:0.#} {JeiLoc.SecondsUnit}</color>";

        var stnTxt = UiKit.MakeText(cardRt, "StnLine", stnLine, 12, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -22), new Vector2(380, 18), _font);

        if (stationItemId > 0)
        {
            int targetStnId = stationItemId;
            _detailClickables.Add(new ValueTuple<RectTransform, Action>(stnTxt.rectTransform, () => SelectItem(targetStnId, true)));
        }

        float rowY = 41f;

        // 3) Culinary stats line (satiation, hydration, regen, affixes)
        if (hasCookLine)
        {
            var csb = new System.Text.StringBuilder("   ");
            if (r.SatiatingSeconds > 0)
                csb.Append("<color=#9AA8BC>").Append(JeiLoc.SatiationLabel).Append("</color> <color=#7BE082>").Append(r.SatiatingSeconds.ToString("0")).Append("s</color>  ");
            if (r.HydratingSeconds > 0)
                csb.Append("<color=#9AA8BC>").Append(JeiLoc.HydrationLabel).Append("</color> <color=#6EC6F5>").Append(r.HydratingSeconds.ToString("0")).Append("s</color>  ");
            if (r.RegenPercent > 0)
                csb.Append("<color=#9AA8BC>").Append(JeiLoc.RegenLabel).Append("</color> <color=#FFB86C>+").Append((r.RegenPercent * 100f).ToString("0")).Append("%</color>  ");

            if (r.FoodAffixIDs.Count > 0 && JeiCatalog.AttributesById.TryGetValue(r.FoodAffixIDs[0], out var firstAttr))
            {
                csb.Append("<color=#9AA8BC>").Append(JeiLoc.AffixesLabel).Append("</color> <color=#E2B0FF><u>").Append(firstAttr.Title).Append("</u></color>");
            }

            var cookTxt = UiKit.MakeText(cardRt, "CookStatsLine", csb.ToString(), 11, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -rowY), new Vector2(380, 17), _font);

            if (r.FoodAffixIDs.Count > 0)
            {
                int targetAttrId = r.FoodAffixIDs[0];
                _detailClickables.Add(new ValueTuple<RectTransform, Action>(cookTxt.rectTransform, () => SelectAttribute(targetAttrId)));
            }

            rowY += 18f;
        }

        // 4) Research unlock line (when viewing usages)
        if (hasUnlockLine && resItem?.Unlock != null)
        {
            var u = resItem.Unlock;
            string uStr = !string.IsNullOrEmpty(u.NodeName)
                ? $"   <color=#9AA8BC>{JeiLoc.ResearchLabel}</color> <color=#FFB86C>{u.NodeName}</color>" + (u.Cost > 0 ? $" <color=#F5D76E>({u.Cost} {JeiLoc.PointsShort})</color>" : "")
                : (u.Cost > 0 ? $"   <color=#9AA8BC>{JeiLoc.ResearchLabel}</color> <color=#F5D76E>{u.Cost} {JeiLoc.PointsShort}</color>" : JeiLoc.CompactStartUnlocked);
            UiKit.MakeText(cardRt, "UnlockLine", uStr, 11, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -rowY), new Vector2(380, 17), _font);
            rowY += 18f;
        }

        // 5) Ingredient rows
        if (r.Ingredients.Count == 0)
        {
            UiKit.MakeText(cardRt, "FreeIng", JeiLoc.NoIngredients, 12, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -rowY), new Vector2(380, 18), _font);
            return;
        }

        for (int i = 0; i < r.Ingredients.Count; i++)
        {
            int ingId = r.Ingredients[i].Key;
            int ingAmt = Mathf.Max(1, r.Ingredients[i].Value);
            string ingName = JeiCatalog.Items.TryGetValue(ingId, out var ie)
                ? ie.DisplayName
                : ("#" + ingId);

            string ingLine = $"   <color=#F5D76E>• {ingAmt}x</color> <color=#D8E8FF><u>{ingName}</u></color> <color=#6E7889>(#{ingId})</color>";
            var ingTxt = UiKit.MakeText(cardRt, "Ing_" + i, ingLine, 12, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -rowY), new Vector2(380, 18), _font);

            int targetIngId = ingId;
            _detailClickables.Add(new ValueTuple<RectTransform, Action>(ingTxt.rectTransform, () => SelectItem(targetIngId, true)));
            rowY += 19f;
        }
    }

    // ---------- spawn (TMI) ----------
    private static void SpawnSelected(int count)
    {
        if (!_enableTmi.Value) { ModService.NotifyWarn(JeiLoc.NotifyTmiDisabled); return; }
        if (_selectedItemId <= 0) return;
        if (!Mirror.NetworkServer.active) { ModService.NotifyWarn(JeiLoc.NotifyHostOnly); return; }

        try
        {
            var console = HRConsoleCommands.Get;
            var pawn = ModService.LocalPawn;
            if (console == null || pawn == null) return;

            int targetCount = Mathf.Clamp(count, 1, 999);
            int remaining = targetCount;
            int addedToInv = 0;
            int droppedAtFeet = 0;

            Vector3 pos = pawn.transform.position;
            var inv = pawn.InventoryManager?.PlayerInventory;

            int safety = 0;
            while (remaining > 0 && safety++ < 30)
            {
                var weapon = console.SpawnPrefab_Server(_selectedItemId, 1, 0, pos + Vector3.up * 0.6f, Quaternion.identity, false);
                if (weapon == null) break;

                int maxStack = 1;
                try { maxStack = Mathf.Max(1, weapon.StackLimit); } catch (Exception) { }
                int batch = Mathf.Min(remaining, maxStack);

                if (batch > 1)
                {
                    try { weapon.SetStackCount(batch, false, false); } catch (Exception) { }
                }

                bool placed = false;
                if (inv != null)
                {
                    try
                    {
                        placed = inv.AddWeapon(weapon, -1, false, true, batch, true);
                    }
                    catch (Exception) { }
                }

                if (placed) addedToInv += batch;
                else droppedAtFeet += batch;

                remaining -= batch;
            }

            string itemName = JeiCatalog.Items.TryGetValue(_selectedItemId, out var entry)
                ? entry.DisplayName
                : ("#" + _selectedItemId);

            if (addedToInv > 0 && droppedAtFeet == 0)
            {
                ModService.NotifySuccess(JeiLoc.NotifySpawnedInv(addedToInv, itemName));
            }
            else if (addedToInv > 0 && droppedAtFeet > 0)
            {
                ModService.NotifyInfo(JeiLoc.NotifySpawnedMixed(addedToInv, droppedAtFeet, itemName));
            }
            else if (droppedAtFeet > 0)
            {
                ModService.NotifyInfo(JeiLoc.NotifySpawnedFeet(droppedAtFeet, itemName));
            }
        }
        catch (Exception e)
        {
            _log?.LogError($"[JEI] Spawn failed: {e}");
            ModService.NotifyError(JeiLoc.NotifySpawnError);
        }
    }
}
