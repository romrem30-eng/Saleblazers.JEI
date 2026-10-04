using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Saleblazers.ModBase;

/// <summary>
/// Integrates the custom 3D Vintage Showcase into Saleblazers:
/// - Loads the 3D model (from VintageShowcase.obj or procedural geometry).
/// - Clones an in-game store display prefab to retain base game networking, placement, and customer AI.
/// - Injects custom visual mesh, materials (polished walnut, glass, brass), and 4 display sockets.
/// - Registers the new item in HRItemDatabase and its crafting recipe in HRCraftingDatabase.
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

    public static bool IsRussianLanguage()
    {
        if (JeiLoc.IsRu) return true;
        try
        {
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }
        return Application.systemLanguage == SystemLanguage.Russian;
    }

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

            // Find optimal store display table template
            var (templateItem, templateDisplay) = FindBestDisplayTemplate(itemDb, log);
            if (templateItem == null || templateDisplay == null)
            {
                log?.LogWarning("[VintageShowcase] Could not find any existing store display table template in item database.");
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

            // 3. Setup visual model, materials & physical colliders
            SetupVisualModelAndColliders(_showcasePrefab, _showcaseMesh, templateItem.ItemPrefab, log);

            // 4. Configure native DisplaySockets on HRDisplayContainer for selling items
            var displayContainer = _showcasePrefab.GetComponentInChildren<HRDisplayContainer>();
            if (displayContainer != null)
            {
                SetupDisplaySockets(displayContainer, log);
            }

            // 5. Setup icon
            _showcaseSprite = LoadShowcaseSprite(log);

            // 6. Create & register HRItemData in HRItemDatabase
            var newItem = CreateItemData(itemDb, _showcasePrefab, _showcaseSprite, templateItem);
            AppendItemToDatabase(itemDb, newItem);

            // 7. Register crafting recipe in HRCraftingDatabase
            if (craftDb != null)
            {
                RegisterCraftingRecipe(craftDb, log);
            }

            _registered = true;
            log?.LogInfo($"[VintageShowcase] Successfully registered '{ShowcaseNameEn}' (#{VintageShowcaseItemID}) with {displayContainer?.DisplaySockets?.Length ?? 0} display sockets and crafting recipe!");
        }
        catch (Exception ex)
        {
            log?.LogError($"[VintageShowcase] Registration failed: {ex}");
        }
    }

    private static (HRItemDatabase.HRItemData item, HRDisplayContainer display) FindBestDisplayTemplate(HRItemDatabase itemDb, ManualLogSource log)
    {
        HRItemDatabase.HRItemData bestItem = null;
        HRDisplayContainer bestDisplay = null;
        int bestScore = int.MinValue;

        log?.LogInfo("[VintageShowcase] Scanning HRItemDatabase for optimal store display table...");

        foreach (var it in itemDb.ItemArray)
        {
            if (it == null || it.ItemPrefab == null) continue;
            var dc = it.ItemPrefab.GetComponentInChildren<HRDisplayContainer>();
            var placeable = it.ItemPrefab.GetComponentInChildren<BaseItemPlaceable>();
            if (dc == null || placeable == null) continue;

            string name = it.ItemName ?? "";
            int socketCount = dc.DisplaySockets != null ? dc.DisplaySockets.Length : 0;

            int score = 0;

            // Specific store display table keywords
            if (name.IndexOf("Display Table", StringComparison.OrdinalIgnoreCase) >= 0) score += 400;
            else if (name.IndexOf("Display Case", StringComparison.OrdinalIgnoreCase) >= 0) score += 350;
            else if (name.IndexOf("Display Stand", StringComparison.OrdinalIgnoreCase) >= 0) score += 300;
            else if (name.IndexOf("Showcase", StringComparison.OrdinalIgnoreCase) >= 0) score += 300;
            else if (name.IndexOf("Sales Table", StringComparison.OrdinalIgnoreCase) >= 0) score += 250;
            else if (name.IndexOf("Shop Table", StringComparison.OrdinalIgnoreCase) >= 0) score += 250;
            else if (name.IndexOf("Store Table", StringComparison.OrdinalIgnoreCase) >= 0) score += 250;
            else if (name.IndexOf("Shop Counter", StringComparison.OrdinalIgnoreCase) >= 0) score += 200;
            else if (name.IndexOf("Display", StringComparison.OrdinalIgnoreCase) >= 0) score += 150;
            else if (name.IndexOf("Counter", StringComparison.OrdinalIgnoreCase) >= 0) score += 100;
            else if (name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0) score += 50;
            else if (name.IndexOf("Shelf", StringComparison.OrdinalIgnoreCase) >= 0) score += 10;

            // Socket count weighting
            if (socketCount == 4) score += 100; // Exact match for 4-shelf vintage showcase
            else if (socketCount > 4) score += 60;
            else if (socketCount >= 2) score += 40;
            else if (socketCount == 0) score -= 200;

            // Heavy penalties for non-merchandise / wall / liquid / weird containers
            if (name.IndexOf("Ladder", StringComparison.OrdinalIgnoreCase) >= 0) score -= 500;
            if (name.IndexOf("Portrait", StringComparison.OrdinalIgnoreCase) >= 0) score -= 500;
            if (name.IndexOf("FishTank", StringComparison.OrdinalIgnoreCase) >= 0) score -= 500;
            if (name.IndexOf("Crate", StringComparison.OrdinalIgnoreCase) >= 0) score -= 300;
            if (name.IndexOf("Basket", StringComparison.OrdinalIgnoreCase) >= 0) score -= 300;
            if (name.IndexOf("Fridge", StringComparison.OrdinalIgnoreCase) >= 0) score -= 200;

            if (score > 0)
            {
                log?.LogInfo($"[VintageShowcase] Candidate: '{name}' (ID {it.ItemID}), Sockets={socketCount}, Score={score}");
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestItem = it;
                bestDisplay = dc;
            }
        }

        if (bestItem != null)
        {
            log?.LogInfo($"[VintageShowcase] Selected best store display template: '{bestItem.ItemName}' (ID {bestItem.ItemID}) with {bestDisplay.DisplaySockets?.Length ?? 0} sockets (Score {bestScore})");
        }
        return (bestItem, bestDisplay);
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
            new List<int>(), // 2: Brass
            new List<int>()  // 3: White display floor
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
                else if (mtl.IndexOf("White", StringComparison.OrdinalIgnoreCase) >= 0 || mtl.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0) curSubmesh = 3;
                else curSubmesh = 0;
                continue;
            }

            if (l.StartsWith("v "))
            {
                var p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                float x = float.Parse(p[1], CultureInfo.InvariantCulture);
                float y = float.Parse(p[2], CultureInfo.InvariantCulture);
                float z = float.Parse(p[3], CultureInfo.InvariantCulture);
                // Convert right-handed OBJ coordinates to Unity left-handed: flip Z
                rawPos.Add(new Vector3(x, y, -z));
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
                // Convert right-handed OBJ normal to Unity left-handed: flip nz
                rawNorm.Add(new Vector3(nx, ny, -nz));
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
                        finalVerts.Add(vI >= 0 && vI < rawPos.Count ? rawPos[vI] : Vector3.zero);
                        finalUvs.Add(vtI >= 0 && vtI < rawUv.Count ? rawUv[vtI] : Vector2.zero);
                        finalNorms.Add(vnI >= 0 && vnI < rawNorm.Count ? rawNorm[vnI] : Vector3.up);
                        vertMap[key] = idx;
                    }
                    faceIdx.Add(idx);
                }

                // Reverse winding order (faceIdx[0], faceIdx[i + 1], faceIdx[i]) for Unity clockwise front faces
                for (int i = 1; i < faceIdx.Count - 1; i++)
                {
                    submeshIndices[curSubmesh].Add(faceIdx[0]);
                    submeshIndices[curSubmesh].Add(faceIdx[i + 1]);
                    submeshIndices[curSubmesh].Add(faceIdx[i]);
                }
            }
        }

        var mesh = new Mesh();
        mesh.name = "VintageShowcase_Mesh";
        mesh.subMeshCount = 4;
        mesh.vertices = new Il2CppStructArray<Vector3>(finalVerts.ToArray());
        mesh.uv = new Il2CppStructArray<Vector2>(finalUvs.ToArray());
        mesh.normals = new Il2CppStructArray<Vector3>(finalNorms.ToArray());

        for (int i = 0; i < 4; i++)
        {
            var arr = new Il2CppStructArray<int>(submeshIndices[i].ToArray());
            mesh.SetTriangles(arr, i);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildProceduralShowcaseMesh()
    {
        var mesh = new Mesh { name = "VintageShowcase_ProceduralMesh", subMeshCount = 4 };
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var norms = new List<Vector3>();
        var trisWood = new List<int>();
        var trisGlass = new List<int>();
        var trisBrass = new List<int>();
        var trisFloor = new List<int>();

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

        // Display floor
        AddBox(new Vector3(0f, 0.082f, 0f), new Vector3(0.54f, 0.005f, 0.44f), trisFloor);

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
        mesh.SetTriangles(new Il2CppStructArray<int>(trisFloor.ToArray()), 3);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetMaterialColor(Material mat, Color col)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        mat.color = col;
    }

    private static Material CreateLitMaterial(Shader shader, Color col, float metallic, float smoothness)
    {
        var mat = new Material(shader);
        SetMaterialColor(mat, col);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Roughness")) mat.SetFloat("_Roughness", 1f - smoothness);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
        return mat;
    }

    private static Material CreateGlassMaterial(Shader shader)
    {
        var mat = new Material(shader);
        mat.name = "VintageShowcase_Glass";

        var glassColor = new Color(0.88f, 0.96f, 1.0f, 0.16f);
        SetMaterialColor(mat, glassColor);

        // Standard URP Lit transparency setup
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1.0f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0.0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.98f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);

        return mat;
    }

    private static void SetupVisualModelAndColliders(GameObject prefab, Mesh mesh, GameObject templatePrefab, ManualLogSource log)
    {
        // 1. Hide original mesh renderers
        foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = false;
        }

        // 2. Convert all old solid physical colliders from the template into disabled triggers
        // so even if BaseWeapon re-enables its cached Colliders array while held, they never push the player.
        foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
        {
            // Keep interaction socket trigger colliders intact
            if (c.isTrigger) continue;
            if (c is MeshCollider mc)
            {
                mc.convex = true;
            }
            c.isTrigger = true;
            c.enabled = false;
        }

        // 3. Make any Rigidbody kinematic and disable collisions to prevent physics interference
        foreach (var rb in prefab.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // 4. Create new visual root
        var visualObj = new GameObject("VintageShowcaseVisual");
        visualObj.transform.SetParent(prefab.transform, false);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localRotation = Quaternion.identity;
        visualObj.transform.localScale = Vector3.one;

        var mf = visualObj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = visualObj.AddComponent<MeshRenderer>();

        // Materials setup
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            var templateRenderers = templatePrefab.GetComponentsInChildren<MeshRenderer>(true);
            if (templateRenderers.Length > 0 && templateRenderers[0].sharedMaterial != null)
            {
                litShader = templateRenderers[0].sharedMaterial.shader;
            }
        }
        if (litShader == null) litShader = Shader.Find("Standard");

        // 1. Rich dark walnut wood frame
        var woodMat = CreateLitMaterial(litShader, new Color(0.38f, 0.20f, 0.10f, 1.0f), 0.05f, 0.50f);
        woodMat.name = "VintageShowcase_Wood";

        // 2. Crystal clear glass
        Material glassMat = null;
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (m == null || string.IsNullOrEmpty(m.name)) continue;
            string n = m.name.ToLowerInvariant();
            if ((n.Contains("glass") || n.Contains("window")) && (m.renderQueue >= 3000 || m.HasProperty("_Surface")))
            {
                glassMat = new Material(m);
                log?.LogInfo($"[VintageShowcase] Cloned game glass material: '{m.name}'");
                break;
            }
        }

        if (glassMat == null)
        {
            glassMat = CreateGlassMaterial(litShader);
        }
        else
        {
            SetMaterialColor(glassMat, new Color(0.88f, 0.96f, 1.0f, 0.16f));
            if (glassMat.HasProperty("_Cull")) glassMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            glassMat.renderQueue = 3000;
        }

        // 3. Polished brass knob
        var brassMat = CreateLitMaterial(litShader, new Color(0.95f, 0.78f, 0.28f, 1.0f), 0.95f, 0.88f);
        brassMat.name = "VintageShowcase_Brass";

        // 4. Warm cream display floor
        var floorMat = CreateLitMaterial(litShader, new Color(0.92f, 0.90f, 0.86f, 1.0f), 0.05f, 0.35f);
        floorMat.name = "VintageShowcase_Floor";

        mr.sharedMaterials = new Il2CppReferenceArray<Material>(new[] { woodMat, glassMat, brassMat, floorMat });

        // 5. Setup BoxCollider - Trigger while held in hands (zero physical pushback), solid when placed on plot
        var mainCol = prefab.GetComponent<BoxCollider>();
        if (mainCol == null) mainCol = prefab.AddComponent<BoxCollider>();
        mainCol.center = new Vector3(0f, 0.425f, 0f);
        mainCol.size = new Vector3(0.60f, 0.85f, 0.50f);
        mainCol.isTrigger = true;
        mainCol.enabled = false;

        var placeable = prefab.GetComponentInChildren<BaseItemPlaceable>();
        if (placeable != null)
        {
            placeable.SetMeshRendererGameObject(visualObj);
            placeable.GridSize = 0.5f;
            placeable.bPlacementSnapToGrid = true;

            // When placed on plot, enable solid physical collision
            var existingPlaced = placeable.OnPlacedDelegate;
            placeable.OnPlacedDelegate = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<BaseItemPlaceable.BaseItemPlacedSignature>(new Action<BaseItemPlaceable, Vector3, Quaternion>((p, pos, rot) =>
            {
                try
                {
                    if (p != null)
                    {
                        var bCol = p.GetComponent<BoxCollider>() ?? p.GetComponentInChildren<BoxCollider>();
                        if (bCol != null)
                        {
                            bCol.isTrigger = false;
                            bCol.enabled = true;
                        }
                    }
                    existingPlaced?.Invoke(p, pos, rot);
                }
                catch (Exception ex)
                {
                    log?.LogWarning($"[VintageShowcase] OnPlaced error: {ex.Message}");
                }
            }));
        }

        var weapon = prefab.GetComponent<BaseWeapon>() ?? prefab.GetComponentInChildren<BaseWeapon>();
        if (weapon != null)
        {
            weapon.ItemID = VintageShowcaseItemID;
        }
    }

    private static void SetupDisplaySockets(HRDisplayContainer container, ManualLogSource log)
    {
        var socketPositions = new Vector3[] {
            new(-0.14f, 0.09f, 0.0f),  // Bottom shelf left
            new(0.14f, 0.09f, 0.0f),   // Bottom shelf right
            new(-0.14f, 0.45f, 0.0f),  // Middle glass shelf left
            new(0.14f, 0.45f, 0.0f)   // Middle glass shelf right
        };

        if (container.DisplaySockets != null && container.DisplaySockets.Length > 0)
        {
            log?.LogInfo($"[VintageShowcase] Repositioning {container.DisplaySockets.Length} native display sockets to showcase shelves...");
            for (int i = 0; i < container.DisplaySockets.Length; i++)
            {
                var s = container.DisplaySockets[i];
                if (s == null) continue;

                var targetPos = socketPositions[i % socketPositions.Length];
                if (s.Start != null)
                {
                    s.Start.localPosition = targetPos;
                    s.Start.localRotation = Quaternion.identity;
                }

                if (s.AggregateCollider != null)
                {
                    s.AggregateCollider.transform.localPosition = targetPos;
                    s.AggregateCollider.isTrigger = true; // Sockets are triggers for interaction raycasts
                }

                s.Dimension = new Vector3(0.24f, 0.30f, 0.22f);
                s.PriceTagOffset = new Vector3(0f, 0.22f, -0.12f);
            }
        }
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
        itemData.OwningDatabase = itemDb;
        if (templateItem != null)
        {
            itemData.SpreadsheetLabel = templateItem.SpreadsheetLabel;
        }

        // Populate Data fields
        if (itemData.Data == null && templateItem?.Data != null)
        {
            itemData.Data = new ItemDatabaseEntryData();
        }

        if (itemData.Data != null)
        {
            if (templateItem?.Data != null)
            {
                itemData.Data.WeaponDamage = templateItem.Data.WeaponDamage;
                itemData.Data.Beauty = templateItem.Data.Beauty;
                itemData.Data.EffectRange = templateItem.Data.EffectRange;
                itemData.Data.ItemShape = templateItem.Data.ItemShape;
                itemData.Data.FakeType = templateItem.Data.FakeType;
            }
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

// ============================================================================
// Harmony Patches for Safe Item Localization & Tooltip Rendering
// ============================================================================

[HarmonyPatch(typeof(HRItemDatabase), nameof(HRItemDatabase.GetLocalizedItemNameByID))]
public static class Patch_HRItemDatabase_GetLocalizedItemNameByID
{
    [HarmonyPrefix]
    public static bool Prefix(int itemID, ref string __result)
    {
        if (VintageShowcaseIntegration.VintageShowcaseItemID > 0 && itemID == VintageShowcaseIntegration.VintageShowcaseItemID)
        {
            __result = VintageShowcaseIntegration.IsRussianLanguage()
                ? VintageShowcaseIntegration.ShowcaseNameRu
                : VintageShowcaseIntegration.ShowcaseNameEn;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(HRItemDatabase), nameof(HRItemDatabase.GetLocalizedItemDescriptionByID))]
public static class Patch_HRItemDatabase_GetLocalizedItemDescriptionByID
{
    [HarmonyPrefix]
    public static bool Prefix(int itemID, ref string __result)
    {
        if (VintageShowcaseIntegration.VintageShowcaseItemID > 0 && itemID == VintageShowcaseIntegration.VintageShowcaseItemID)
        {
            __result = VintageShowcaseIntegration.IsRussianLanguage()
                ? VintageShowcaseIntegration.ShowcaseDescRu
                : VintageShowcaseIntegration.ShowcaseDescEn;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(HRItemDatabase), nameof(HRItemDatabase.GetLocalizedItemName))]
public static class Patch_HRItemDatabase_GetLocalizedItemName
{
    [HarmonyPrefix]
    public static bool Prefix(string Name, ref string __result)
    {
        if (!string.IsNullOrEmpty(Name) && Name == VintageShowcaseIntegration.ShowcaseNameEn)
        {
            __result = VintageShowcaseIntegration.IsRussianLanguage()
                ? VintageShowcaseIntegration.ShowcaseNameRu
                : VintageShowcaseIntegration.ShowcaseNameEn;
            return false;
        }
        return true;
    }
}


