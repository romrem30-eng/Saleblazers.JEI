using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Saleblazers.ModBase;

internal static class UiKit
{
    private static readonly Dictionary<KeyCode, bool> _keyPrev = new();
    private static readonly Dictionary<KeyCode, float> _keyDebounceUntil = new();
    private static TMP_FontAsset _cachedFont;
    private static Sprite _whiteSprite;
    private static ManualLogSource _log;

    public static void SetLogger(ManualLogSource log) => _log = log;

    public static bool KeyPressed(KeyCode key)
    {
        bool held = false;
        try
        {
            var kb = ReInput.controllers?.Keyboard;
            if (kb != null && (kb.GetKey(key) || kb.GetKeyDown(key)))
                held = true;
        }
        catch (Exception) { }

        try
        {
            if (Input.GetKey(key) || Input.GetKeyDown(key))
                held = true;
        }
        catch (Exception) { }

        _keyPrev.TryGetValue(key, out bool prev);
        _keyPrev[key] = held;
        if (!held || prev) return false;

        if (_keyDebounceUntil.TryGetValue(key, out float blockedUntil) &&
            Time.unscaledTime < blockedUntil)
            return false;

        _keyDebounceUntil[key] = Time.unscaledTime + 0.25f;
        return true;
    }

    public static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite != null) return _whiteSprite;
            try
            {
                var tex = Texture2D.whiteTexture;
                if (tex != null)
                {
                    _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (Exception) { }
            return _whiteSprite;
        }
    }

    public static TMP_FontAsset LoadFont()
    {
        if (_cachedFont != null && _cachedFont.material != null)
            return _cachedFont;

        try
        {
            var texts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true);
            if (texts != null)
            {
                foreach (var t in texts)
                {
                    if (t != null && t.font != null && t.font.material != null &&
                        t.font.name != null && !t.font.name.Contains("Emoji"))
                    {
                        _cachedFont = t.font;
                        return _cachedFont;
                    }
                }
            }
        }
        catch (Exception) { }

        try
        {
            var f = TMP_Settings.defaultFontAsset;
            if (f != null && f.material != null)
            {
                _cachedFont = f;
                return _cachedFont;
            }
        }
        catch (Exception) { }

        try
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts != null && fonts.Length > 0)
            {
                foreach (var font in fonts)
                {
                    if (font != null && font.material != null && font.name != null && !font.name.Contains("Emoji"))
                    {
                        _cachedFont = font;
                        return _cachedFont;
                    }
                }
                if (fonts[0] != null && fonts[0].material != null)
                {
                    _cachedFont = fonts[0];
                    return _cachedFont;
                }
            }
        }
        catch (Exception) { }

        try
        {
            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                           ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (builtinFont != null)
            {
                var created = TMP_FontAsset.CreateFontAsset(builtinFont);
                if (created != null)
                {
                    _cachedFont = created;
                    return _cachedFont;
                }
            }
        }
        catch (Exception) { }

        try
        {
            var osFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (osFont != null)
            {
                var created = TMP_FontAsset.CreateFontAsset(osFont);
                if (created != null)
                {
                    _cachedFont = created;
                    return _cachedFont;
                }
            }
        }
        catch (Exception) { }

        return null;
    }

    public static Canvas MakeCanvas(string name)
    {
        var root = new GameObject(name);
        root.layer = 5;
        UnityEngine.Object.DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;
        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal |
            AdditionalCanvasShaderChannels.Tangent;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform MakeRect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.layer = 5;
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition3D = new Vector3(pos.x, pos.y, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform MakeRect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        Vector2 pivot = (aMin == aMax) ? aMin : new Vector2(0.5f, 0.5f);
        return MakeRect(parent, name, aMin, aMax, pivot, pos, size);
    }

    public static Image MakeImage(Transform parent, string name, Color color, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = MakeRect(parent, name, aMin, aMax, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        if (WhiteSprite != null) img.sprite = WhiteSprite;
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    public static Image MakeImage(Transform parent, string name, Color color, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        Vector2 pivot = (aMin == aMax) ? aMin : new Vector2(0.5f, 0.5f);
        return MakeImage(parent, name, color, aMin, aMax, pivot, pos, size);
    }

    public static TMP_Text MakeText(Transform parent, string name, string text, int size, Color color, TextAnchor align,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta, TMP_FontAsset font)
    {
        var rt = MakeRect(parent, name, aMin, aMax, pivot, pos, sizeDelta);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text ?? "";
        t.fontSize = size;
        t.color = color;
        t.alignment = align switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.TopLeft
        };
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static TMP_Text MakeText(Transform parent, string name, string text, int size, Color color, TextAnchor align,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta, TMP_FontAsset font)
    {
        Vector2 pivot = (aMin == aMax) ? aMin : new Vector2(0.5f, 0.5f);
        return MakeText(parent, name, text, size, color, align, aMin, aMax, pivot, pos, sizeDelta, font);
    }

    public static RectTransform MakeClickable(Transform parent, string name, string label, Action onClick,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, TMP_FontAsset font, List<ValueTuple<RectTransform, Action>> clickables)
    {
        var img = MakeImage(parent, name, new Color(0.22f, 0.5f, 0.28f, 0.95f), aMin, aMax, pivot, pos, size);
        var rt = img.rectTransform;
        MakeText(rt, name + "_lbl", label, 13, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, font);
        clickables.Add(new ValueTuple<RectTransform, Action>(rt, onClick));
        return rt;
    }

    public static RectTransform MakeClickable(Transform parent, string name, string label, Action onClick,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, TMP_FontAsset font, List<ValueTuple<RectTransform, Action>> clickables)
    {
        Vector2 pivot = (aMin == aMax) ? aMin : new Vector2(0.5f, 0.5f);
        return MakeClickable(parent, name, label, onClick, aMin, aMax, pivot, pos, size, font, clickables);
    }
}
