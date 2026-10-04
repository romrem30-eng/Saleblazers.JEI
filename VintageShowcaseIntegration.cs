using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Saleblazers.ModBase;

/// <summary>
/// Integrates the custom 3D Vintage Showcase into Saleblazers:
/// - Loads the 3D model (from VintageShowcase.obj or procedural geometry).
/// - Clones an in-game store display prefab to retain base game networking, placement, and customer AI.
/// - Injects custom visual mesh, materials (polished walnut, glass, brass), and 4 display sockets.
/// - Registers the new item (#19950) in HRItemDatabase and its crafting recipe in HRCraftingDatabase.
/// </summary>
public static class VintageShowcaseIntegration
{
    public static int VintageShowcaseItemID { get; internal set; } = -1;
    public const string ShowcaseNameEn = "Vintage Showcase";
    public const string ShowcaseNameRu = "Винтажная витрина";
    public const string ShowcaseDescEn = "An exquisite vintage confectionery showcase with a glass shelf, polished wooden frame, and brass hardware. Perfect for displaying chocolates, jewelry, and prized goods.";
    public const string ShowcaseDescRu = "Изысканная винтажная кондитерская витрина со стеклянной полкой, деревянным каркасом и латунной фурнитурой. Идеально для демонстрации сладостей, украшений и ценных товаров.";

    private static bool _registered;
    private static GameObject _showcasePrefab;
    private static Mesh _showcaseMesh;
    private static Sprite _showcaseSprite;

    public static bool IsRegistered => _registered;

    public static void EnsureRegistered(HRItemDatabase itemDb, HRCraftingDatabase craftDb, ManualLogSource log)
    {
        if (_registered) return;
        if (itemDb == null || itemDb.ItemArray == null) return;

        try
        {
            // Check if already registered
            foreach (var it in itemDb.ItemArray)
            {
                if (it != null && (it.ItemName == ShowcaseNameEn || (VintageShowcaseItemID > 0 && it.ItemID == VintageShowcaseItemID)))
                {
                    VintageShowcaseItemID = it.ItemID;
                    _registered = true;
                    return;
                }
            }

            // Assign next contiguous ItemID matching its exact index in ItemArray to satisfy direct array lookups
            VintageShowcaseItemID = itemDb.ItemArray.Length;
            log?.LogInfo($"[VintageShowcase] Assigning ItemID {VintageShowcaseItemID} (next contiguous array index)...");

            log?.LogInfo("[VintageShowcase] Searching for base display template in HRItemDatabase...");
            HRItemDatabase.HRItemData templateItem = null;
            HRDisplayContainer templateDisplay = null;
            HRItemDatabase.HRItemData fallbackItem = null;
            HRDisplayContainer fallbackDisplay = null;

            foreach (var it in itemDb.ItemArray)
            {
                if (it == null || it.ItemPrefab == null) continue;
                var dc = it.ItemPrefab.GetComponentInChildren<HRDisplayContainer>();
                var placeable = it.ItemPrefab.GetComponentInChildren<BaseItemPlaceable>();
                if (dc != null && placeable != null)
                {
                    string name = it.ItemName ?? "";
                    if (name.IndexOf("Shelf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Display", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Counter", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        templateItem = it;
                        templateDisplay = dc;
                        log?.LogInfo($"[VintageShowcase] Selected display template: '{it.ItemName}' (ID {it.ItemID})");
                        break;
                    }
                    if (fallbackItem == null)
                    {
                        fallbackItem = it;
                        fallbackDisplay = dc;
                    }
                }
            }

            if (templateItem == null)
            {
                templateItem = fallbackItem;
                templateDisplay = fallbackDisplay;
                if (templateItem != null)
                {
                    log?.LogInfo($"[VintageShowcase] Using fallback display template: '{templateItem.ItemName}' (ID {templateItem.ItemID})");
                }
            }

            if (templateItem == null || templateDisplay == null)
            {
                log?.LogWarning("[VintageShowcase] Could not find any existing display template in item database.");
                return;
            }

            // 1. Build or load 3D Mesh
            _showcaseMesh = LoadShowcaseMesh(log);
            if (_showcaseMesh == null)
            {
                log?.LogWarning("[VintageShowcase] Failed to construct showcase mesh.");
                return;
            }

            // 2. Clone the template prefab
            _showcasePrefab = UnityEngine.Object.Instantiate(templateItem.ItemPrefab);
            _showcasePrefab.name = "PF_VintageShowcase";
            UnityEngine.Object.DontDestroyOnLoad(_showcasePrefab);
            _showcasePrefab.SetActive(false);

            var weapon = _showcasePrefab.GetComponent<BaseWeapon>() ?? _showcasePrefab.GetComponentInChildren<BaseWeapon>();
            if (weapon != null)
            {
                weapon.ItemID = VintageShowcaseItemID;
            }

            // 3. Setup visual model & materials
            SetupVisualModel(_showcasePrefab, _showcaseMesh, templateItem.ItemPrefab, log);

            // 4. Setup BoxCollider
            var col = _showcasePrefab.GetComponent<BoxCollider>();
            if (col == null) col = _showcasePrefab.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.425f, 0f);
            col.size = new Vector3(0.60f, 0.85f, 0.50f);

            // 5. Configure DisplaySockets on HRDisplayContainer
            var displayContainer = _showcasePrefab.GetComponentInChildren<HRDisplayContainer>();
            if (displayContainer != null)
            {
                SetupDisplaySockets(displayContainer, _showcasePrefab.transform, templateDisplay, log);
            }

            // 6. Setup icon
            _showcaseSprite = LoadShowcaseSprite(log);

            // 7. Create & register HRItemData in HRItemDatabase
            var newItem = CreateItemData(itemDb, _showcasePrefab, _showcaseSprite, templateItem);
            AppendItemToDatabase(itemDb, newItem);

            // 8. Register crafting recipe in HRCraftingDatabase
            if (craftDb != null)
            {
                RegisterCraftingRecipe(craftDb, log);
            }

            _registered = true;
            log?.LogInfo($"[VintageShowcase] Successfully registered '{ShowcaseNameEn}' (#{VintageShowcaseItemID}) with 4 display sockets and crafting recipe!");
        }
        catch (Exception ex)
        {
            log?.LogError($"[VintageShowcase] Registration failed: {ex}");
        }
    }

    private static Mesh LoadShowcaseMesh(ManualLogSource log)
    {
        string[] candidatePaths = {
            Path.Combine(Paths.PluginPath, "VintageShowcase", "VintageShowcase.obj"),
            @"C:\Users\Тралалело\saleblazers-modding\VintageShowcase\VintageShowcase.obj"
        };

        foreach (var p in candidatePaths)
        {
            if (File.Exists(p))
            {
                try
                {
                    log?.LogInfo($"[VintageShowcase] Loading 3D model from OBJ: {p}");
                    return ParseObj(File.ReadAllLines(p));
                }
                catch (Exception e)
                {
                    log?.LogWarning($"[VintageShowcase] OBJ parse failed: {e.Message}, falling back to procedural generation.");
                }
            }
        }

        log?.LogInfo("[VintageShowcase] Generating 3D showcase mesh procedurally...");
        return BuildProceduralShowcaseMesh();
    }

    private static Mesh ParseObj(string[] lines)
    {
        var rawPos = new List<Vector3>();
        var rawUv = new List<Vector2>();
        var rawNorm = new List<Vector3>();

        var submeshIndices = new List<int>[] {
            new List<int>(), // 0: Wood
            new List<int>(), // 1: Glass
            new List<int>()  // 2: Brass
        };

        var finalVerts = new List<Vector3>();
        var finalUvs = new List<Vector2>();
        var finalNorms = new List<Vector3>();
        var vertMap = new Dictionary<string, int>();

        int curSubmesh = 0;

        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (string.IsNullOrEmpty(l) || l.StartsWith("#")) continue;

            if (l.StartsWith("usemtl "))
            {
                string mtl = l.Substring(7).Trim();
                if (mtl.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0) curSubmesh = 1;
                else if (mtl.IndexOf("Brass", StringComparison.OrdinalIgnoreCase) >= 0) curSubmesh = 2;
                else curSubmesh = 0;
                continue;
            }

            if (l.StartsWith("v "))
            {
                var p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                float x = float.Parse(p[1], CultureInfo.InvariantCulture);
                float y = float.Parse(p[2], CultureInfo.InvariantCulture);
                float z = float.Parse(p[3], CultureInfo.InvariantCulture);
                rawPos.Add(new Vector3(x, y, z));
            }
            else if (l.StartsWith("vt "))
            {
                var p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                float u = float.Parse(p[1], CultureInfo.InvariantCulture);
                float v = float.Parse(p[2], CultureInfo.InvariantCulture);
                rawUv.Add(new Vector2(u, v));
            }
            else if (l.StartsWith("vn "))
            {
                var p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                float nx = float.Parse(p[1], CultureInfo.InvariantCulture);
                float ny = float.Parse(p[2], CultureInfo.InvariantCulture);
                float nz = float.Parse(p[3], CultureInfo.InvariantCulture);
                rawNorm.Add(new Vector3(nx, ny, nz));
            }
            else if (l.StartsWith("f "))
            {
                var p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var faceIdx = new List<int>();
                for (int i = 1; i < p.Length; i++)
                {
                    string key = p[i];
                    if (!vertMap.TryGetValue(key, out int idx))
                    {
                        var s = key.Split('/');
                        int vI = int.Parse(s[0]) - 1;
                        int vtI = s.Length > 1 && !string.IsNullOrEmpty(s[1]) ? int.Parse(s[1]) - 1 : -1;
                        int vnI = s.Length > 2 && !string.IsNullOrEmpty(s[2]) ? int.Parse(s[2]) - 1 : -1;

                        idx = finalVerts.Count;
                        finalVerts.Add(rawPos[vI]);
                        finalUvs.Add(vtI >= 0 && vtI < rawUv.Count ? rawUv[vtI] : Vector2.zero);
                        finalNorms.Add(vnI >= 0 && vnI < rawNorm.Count ? rawNorm[vnI] : Vector3.up);
                        vertMap[key] = idx;
                    }
                    faceIdx.Add(idx);
                }

                for (int i = 1; i < faceIdx.Count - 1; i++)
                {
                    submeshIndices[curSubmesh].Add(faceIdx[0]);
                    submeshIndices[curSubmesh].Add(faceIdx[i]);
                    submeshIndices[curSubmesh].Add(faceIdx[i + 1]);
                }
            }
        }

        var mesh = new Mesh();
        mesh.name = "VintageShowcase_Mesh";
        mesh.subMeshCount = 3;
        mesh.vertices = new Il2CppStructArray<Vector3>(finalVerts.ToArray());
        mesh.uv = new Il2CppStructArray<Vector2>(finalUvs.ToArray());
        mesh.normals = new Il2CppStructArray<Vector3>(finalNorms.ToArray());

        for (int i = 0; i < 3; i++)
        {
            var arr = new Il2CppStructArray<int>(submeshIndices[i].ToArray());
            mesh.SetTriangles(arr, i);
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildProceduralShowcaseMesh()
    {
        var mesh = new Mesh { name = "VintageShowcase_ProceduralMesh", subMeshCount = 3 };
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var norms = new List<Vector3>();
        var trisWood = new List<int>();
        var trisGlass = new List<int>();
        var trisBrass = new List<int>();

        void AddBox(Vector3 center, Vector3 size, List<int> triList)
        {
            Vector3 h = size * 0.5f;
            Vector3[] fVerts = {
                // Front
                new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z),
                // Back
                new(h.x, -h.y, h.z), new(-h.x, -h.y, h.z), new(-h.x, h.y, h.z), new(h.x, h.y, h.z),
                // Left
                new(-h.x, -h.y, h.z), new(-h.x, -h.y, -h.z), new(-h.x, h.y, -h.z), new(-h.x, h.y, h.z),
                // Right
                new(h.x, -h.y, -h.z), new(h.x, -h.y, h.z), new(h.x, h.y, h.z), new(h.x, h.y, -h.z),
                // Top
                new(-h.x, h.y, -h.z), new(h.x, h.y, -h.z), new(h.x, h.y, h.z), new(-h.x, h.y, h.z),
                // Bottom
                new(-h.x, -h.y, h.z), new(h.x, -h.y, h.z), new(h.x, -h.y, -h.z), new(-h.x, -h.y, -h.z)
            };

            Vector3[] fNorms = {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down
            };

            for (int f = 0; f < 6; f++)
            {
                int baseIdx = verts.Count;
                for (int v = 0; v < 4; v++)
                {
                    verts.Add(center + fVerts[f * 4 + v]);
                    norms.Add(fNorms[f * 4 + v]);
                    uvs.Add(v switch { 0 => new(0, 0), 1 => new(1, 0), 2 => new(1, 1), _ => new(0, 1) });
                }
                triList.Add(baseIdx); triList.Add(baseIdx + 1); triList.Add(baseIdx + 2);
                triList.Add(baseIdx); triList.Add(baseIdx + 2); triList.Add(baseIdx + 3);
            }
        }

        // 1. Wood Frame: Base plinth
        AddBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.66f, 0.04f, 0.56f), trisWood);
        AddBox(new Vector3(0f, 0.05f, 0f), new Vector3(0.63f, 0.03f, 0.53f), trisWood);
        AddBox(new Vector3(0f, 0.07f, 0f), new Vector3(0.60f, 0.02f, 0.50f), trisWood);

        // 4 corner posts
        float cx = 0.28f, cz = 0.23f;
        AddBox(new Vector3(-cx, 0.44f, -cz), new Vector3(0.04f, 0.72f, 0.04f), trisWood);
        AddBox(new Vector3(cx, 0.44f, -cz), new Vector3(0.04f, 0.72f, 0.04f), trisWood);
        AddBox(new Vector3(-cx, 0.44f, cz), new Vector3(0.04f, 0.72f, 0.04f), trisWood);
        AddBox(new Vector3(cx, 0.44f, cz), new Vector3(0.04f, 0.72f, 0.04f), trisWood);

        // Top crown
        AddBox(new Vector3(0f, 0.82f, 0f), new Vector3(0.63f, 0.04f, 0.53f), trisWood);

        // 2. Glass: Single middle shelf
        AddBox(new Vector3(0f, 0.44f, 0f), new Vector3(0.53f, 0.008f, 0.43f), trisGlass);
        // Glass side walls
        AddBox(new Vector3(-cx, 0.44f, 0f), new Vector3(0.006f, 0.70f, 0.44f), trisGlass);
        AddBox(new Vector3(cx, 0.44f, 0f), new Vector3(0.006f, 0.70f, 0.44f), trisGlass);
        AddBox(new Vector3(0f, 0.44f, cz), new Vector3(0.53f, 0.70f, 0.006f), trisGlass);

        // 3. Brass knob & hardware
        AddBox(new Vector3(0.24f, 0.44f, -cz - 0.02f), new Vector3(0.03f, 0.03f, 0.03f), trisBrass);

        mesh.vertices = new Il2CppStructArray<Vector3>(verts.ToArray());
        mesh.uv = new Il2CppStructArray<Vector2>(uvs.ToArray());
        mesh.normals = new Il2CppStructArray<Vector3>(norms.ToArray());

        mesh.SetTriangles(new Il2CppStructArray<int>(trisWood.ToArray()), 0);
        mesh.SetTriangles(new Il2CppStructArray<int>(trisGlass.ToArray()), 1);
        mesh.SetTriangles(new Il2CppStructArray<int>(trisBrass.ToArray()), 2);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetupVisualModel(GameObject prefab, Mesh mesh, GameObject templatePrefab, ManualLogSource log)
    {
        // Hide original mesh renderers
        foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.enabled = false;
        }

        // Create new child object
        var visualObj = new GameObject("VintageShowcaseVisual");
        visualObj.transform.SetParent(prefab.transform, false);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localRotation = Quaternion.identity;
        visualObj.transform.localScale = Vector3.one;

        var mf = visualObj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = visualObj.AddComponent<MeshRenderer>();

        // Materials setup
        Material woodMat = null;
        Material glassMat = null;

        var templateRenderers = templatePrefab.GetComponentsInChildren<MeshRenderer>(true);
        if (templateRenderers.Length > 0 && templateRenderers[0].sharedMaterial != null)
        {
            woodMat = new Material(templateRenderers[0].sharedMaterial);
            woodMat.color = new Color(0.42f, 0.24f, 0.12f, 1.0f);
        }
        else
        {
            woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.42f, 0.24f, 0.12f, 1.0f)
            };
        }

        foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (mat == null) continue;
            string n = mat.name.ToLowerInvariant();
            if (glassMat == null && (n.Contains("glass") || n.Contains("window") || n.Contains("bottle")))
            {
                glassMat = mat;
            }
        }

        if (glassMat == null)
        {
            glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            glassMat.color = new Color(0.85f, 0.94f, 1.0f, 0.25f);
        }

        var brassMat = new Material(woodMat.shader);
        brassMat.color = new Color(0.85f, 0.65f, 0.22f, 1.0f);
        if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.9f);
        if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.75f);

        mr.sharedMaterials = new Il2CppReferenceArray<Material>(new[] { woodMat, glassMat, brassMat });

        var placeable = prefab.GetComponentInChildren<BaseItemPlaceable>();
        if (placeable != null)
        {
            placeable.SetMeshRendererGameObject(visualObj);
            placeable.GridSize = 0.5f;
            placeable.bPlacementSnapToGrid = true;
        }
    }

    private static void SetupDisplaySockets(HRDisplayContainer container, Transform root, HRDisplayContainer templateContainer, ManualLogSource log)
    {
        var socketPositions = new Vector3[] {
            new(-0.14f, 0.09f, 0.0f),  // Bottom shelf left
            new(0.14f, 0.09f, 0.0f),   // Bottom shelf right
            new(-0.14f, 0.45f, 0.0f),  // Middle glass shelf left
            new(0.14f, 0.45f, 0.0f)   // Middle glass shelf right
        };

        var sockets = new List<DisplaySocket>();
        var holder = container.Cast<IDisplaySocketHolder>();

        for (int i = 0; i < socketPositions.Length; i++)
        {
            var socketObj = new GameObject($"DisplaySocket_{i}");
            socketObj.transform.SetParent(root, false);
            socketObj.transform.localPosition = socketPositions[i];
            socketObj.transform.localRotation = Quaternion.identity;

            var col = socketObj.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.15f, 0f);
            col.size = new Vector3(0.24f, 0.30f, 0.22f);

            var s = new DisplaySocket();
            s.Start = socketObj.transform;
            s.SocketHolder = holder;
            s.SocketIndex = i;
            s.Capacity = 1;
            s.MaxStackCount = 5;
            s.Dimension = new Vector3(0.24f, 0.30f, 0.22f);
            s.PriceTagOffset = new Vector3(0f, 0.22f, -0.12f);
            s.AggregateCollider = col;

            if (templateContainer != null && templateContainer.DisplaySockets != null && templateContainer.DisplaySockets.Length > 0)
            {
                var src = templateContainer.DisplaySockets[0];
                s.SocketAlignStrategy = src.SocketAlignStrategy;
                s.Alignment = src.Alignment;
                s.Spacing = src.Spacing;
                s.StackingRule = src.StackingRule;
            }

            sockets.Add(s);
        }

        container.DisplaySockets = new Il2CppReferenceArray<DisplaySocket>(sockets.ToArray());
        log?.LogInfo($"[VintageShowcase] Configured {sockets.Count} display sockets on showcase.");
    }

    private static Sprite LoadShowcaseSprite(ManualLogSource log)
    {
        string[] iconPaths = {
            Path.Combine(Paths.PluginPath, "VintageShowcase", "preview.png"),
            @"C:\Users\Тралалело\saleblazers-modding\VintageShowcase\preview.png"
        };

        foreach (var p in iconPaths)
        {
            if (File.Exists(p))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    var tex = new Texture2D(2, 2);
                    if (ImageConversion.LoadImage(tex, bytes))
                    {
                        log?.LogInfo($"[VintageShowcase] Loaded showcase sprite from {p}");
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
                catch (Exception e)
                {
                    log?.LogWarning($"[VintageShowcase] Could not load sprite: {e.Message}");
                }
            }
        }
        return null;
    }

    private static HRItemDatabase.HRItemData CreateItemData(HRItemDatabase itemDb, GameObject prefab, Sprite icon, HRItemDatabase.HRItemData templateItem)
    {
        var itemData = new HRItemDatabase.HRItemData();
        itemData.ItemID = VintageShowcaseItemID;
        itemData.ItemName = ShowcaseNameEn;
        itemData.ItemPrefab = prefab;

        // Populate Data fields
        if (itemData.Data == null && templateItem?.Data != null)
        {
            itemData.Data = new ItemDatabaseEntryData();
        }

        if (itemData.Data != null)
        {
            itemData.Data.ItemDescription = ShowcaseDescEn;
            itemData.Data.ItemValue = 280f;
            itemData.Data.CraftedItemValue = 280f;
            itemData.Data.ItemSprite = icon;
            itemData.Data.ItemSize = HRItemSize.LargeBox;
            itemData.Data.MaterialType = HRMaterialType.Wood;
        }

        var catList = new Il2CppSystem.Collections.Generic.List<HRItemCategorySO>();
        if (itemDb.ItemDescriptorDB != null)
        {
            var buildingCat = itemDb.ItemDescriptorDB.GetCategoryFromID("Building");
            if (buildingCat != null) catList.Add(buildingCat);
            var furnitureCat = itemDb.ItemDescriptorDB.GetCategoryFromID("Furniture");
            if (furnitureCat != null) catList.Add(furnitureCat);
        }
        itemData.CategoryList = catList;

        return itemData;
    }

    private static void AppendItemToDatabase(HRItemDatabase itemDb, HRItemDatabase.HRItemData newItem)
    {
        var oldArray = itemDb.ItemArray;
        var newArray = new Il2CppReferenceArray<HRItemDatabase.HRItemData>(oldArray.Length + 1);
        for (int i = 0; i < oldArray.Length; i++)
        {
            newArray[i] = oldArray[i];
        }
        newArray[oldArray.Length] = newItem;
        itemDb.ItemArray = newArray;
    }

    private static void RegisterCraftingRecipe(HRCraftingDatabase craftDb, ManualLogSource log)
    {
        var craftInfo = new HRCraftingInfo();
        craftInfo.ItemName = ShowcaseNameEn;
        craftInfo.CraftingID = VintageShowcaseItemID;
        craftInfo.ResultItemID = VintageShowcaseItemID;
        craftInfo.Flag = HRCraftingFlag.Sawmill | HRCraftingFlag.WoodenCrafting;
        craftInfo.bStartUnlocked = true;

        var recipe = new HRCraftingInfo.HRCraftingRecipe();
        recipe.ResultItemID = VintageShowcaseItemID;
        recipe.NumToCraft = 1;
        recipe.TimeToCraft = 5.0f;

        // Ingredients: 6x WoodLog (#102), 4x StoneOre (#89), 2x CopperOre (#93)
        var ing1 = new HRCraftingInfo.HRCraftingIngredient { ItemID = 102, AmountRequired = 6 };
        var ing2 = new HRCraftingInfo.HRCraftingIngredient { ItemID = 89, AmountRequired = 4 };
        var ing3 = new HRCraftingInfo.HRCraftingIngredient { ItemID = 93, AmountRequired = 2 };

        recipe.Ingredients = new Il2CppReferenceArray<HRCraftingInfo.HRCraftingIngredient>(new[] { ing1, ing2, ing3 });
        craftInfo.Recipes = new Il2CppReferenceArray<HRCraftingInfo.HRCraftingRecipe>(new[] { recipe });

        var oldCraft = craftDb.CraftableItems;
        if (oldCraft != null)
        {
            var newCraft = new Il2CppReferenceArray<HRCraftingInfo>(oldCraft.Length + 1);
            for (int i = 0; i < oldCraft.Length; i++)
            {
                newCraft[i] = oldCraft[i];
            }
            newCraft[oldCraft.Length] = craftInfo;
            craftDb.CraftableItems = newCraft;
            craftDb.UpdateCache();
            log?.LogInfo("[VintageShowcase] Registered crafting recipe in HRCraftingDatabase (Craftable at Sawmill / Wood Crafting Table).");
        }
    }
}
