using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Các hàm dựng UI dùng chung cho HUD (UIManager) và màn hình chính (MainMenu): tạo bằng code, không cần prefab.
/// </summary>
public static class UIFactory
{
    // Bảng màu chung
    public static readonly Color Panel = new Color(0.07f, 0.09f, 0.15f, 0.92f);
    public static readonly Color PanelLight = new Color(0.13f, 0.16f, 0.25f, 0.95f);
    public static readonly Color Accent = new Color(1f, 0.47f, 0.69f, 1f);      // Hồng Jelly
    public static readonly Color AccentHover = new Color(1f, 0.6f, 0.78f, 1f);
    public static readonly Color Button = new Color(0.16f, 0.2f, 0.31f, 1f);
    public static readonly Color ButtonHover = new Color(0.25f, 0.31f, 0.46f, 1f);
    public static readonly Color Danger = new Color(0.45f, 0.17f, 0.22f, 1f);
    public static readonly Color DangerHover = new Color(0.62f, 0.22f, 0.28f, 1f);
    public static readonly Color TextMain = new Color(0.95f, 0.96f, 1f, 1f);
    public static readonly Color TextDim = new Color(0.62f, 0.67f, 0.78f, 1f);
    public static readonly Color Success = new Color(0.36f, 0.86f, 0.55f, 1f);

    private static Font font;
    private static Sprite solidSprite;

    public static Font Font
    {
        get
        {
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma" }, 24);
            }
            return font;
        }
    }

    public static Sprite SolidSprite
    {
        get
        {
            if (solidSprite == null)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                solidSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            }
            return solidSprite;
        }
    }

    /// <summary>
    /// Canvas Overlay co giãn theo độ phân giải 1920x1080
    /// </summary>
    public static Canvas CreateCanvas(GameObject host, int sortingOrder)
    {
        Canvas canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        host.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject esObj = new GameObject("EventSystem");
        Object.DontDestroyOnLoad(esObj);
        esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esObj.AddComponent<InputSystemUIInputModule>();
#else
        esObj.AddComponent<StandaloneInputModule>();
#endif
    }

    /// <summary>
    /// RectTransform con, neo tại anchor (pivot cùng vị trí neo)
    /// </summary>
    public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>
    /// RectTransform con phủ kín cha (trừ lề padding)
    /// </summary>
    public static RectTransform CreateFill(string name, Transform parent, float padding = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * padding;
        rect.offsetMax = -Vector2.one * padding;
        return rect;
    }

    public static Image AddImage(RectTransform rect, Color color)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.sprite = SolidSprite;
        img.color = color;
        return img;
    }

    public static Text CreateText(Transform parent, string content, int fontSize, Color color,
        TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        RectTransform rect = CreateFill("Text", parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position,
        Vector2 size, Color normal, Color hover, UnityEngine.Events.UnityAction onClick, int fontSize = 22)
    {
        RectTransform rect = CreateRect(name, parent, anchor, position, size);
        Image img = AddImage(rect, Color.white);

        Button btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = normal;
        colors.highlightedColor = hover;
        colors.selectedColor = normal;
        colors.pressedColor = normal * 0.8f;
        colors.disabledColor = new Color(normal.r * 0.5f, normal.g * 0.5f, normal.b * 0.5f, 0.6f);
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(onClick);

        CreateText(rect, label, fontSize, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        return btn;
    }
}
