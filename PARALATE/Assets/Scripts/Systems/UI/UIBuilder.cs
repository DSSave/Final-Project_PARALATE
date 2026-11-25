using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ตัวช่วยสร้าง UI ด้วยโค้ด (แนวเดียวกับ SceneFader): หน้าต่างใหม่ทั้งหมด (Pause, Upgrade, วงล้ออาวุธ, Scope)
/// สร้างตัวเองตอนรัน ไม่ต้องวาง/ลากอะไรใน Scene ขนาดอ้างอิง 1920x1080
/// </summary>
public static class UIBuilder
{
    public static readonly Color WindowColor = new Color(0.06f, 0.07f, 0.09f, 0.96f);
    public static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color AccentColor = new Color(0.25f, 0.7f, 1f, 1f);
    public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    public static readonly Color TextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
    public static readonly Color MutedText = new Color(0.92f, 0.94f, 0.96f, 0.55f);

    private static Sprite circleSprite;

    /// <summary>ล้าง Sprite ที่จำไว้ (เครื่องมือสร้าง Prefab เรียกหลังเขียนไฟล์รูปลง Resources/UI)</summary>
    public static void ClearSpriteCache()
    {
        circleSprite = null;
        triangleSprite = null;
    }

    public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);

        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>ยืดเต็มพ่อ (เว้นขอบได้)</summary>
    public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// <summary>ขนาดคงที่ ยึดจุด anchor (0-1) ของพ่อ</summary>
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image Panel(string name, Transform parent, Color color, bool raycastTarget = true)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, Color? color = null)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color ?? TextColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    public static Button Button(string name, Transform parent, string label, Action onClick, float fontSize = 24f, Color? color = null)
    {
        Image image = Panel(name, parent, color ?? ButtonColor);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.8f, 1.8f, 1.8f, 1f);
        colors.pressedColor = new Color(2.4f, 2.4f, 2.4f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = Text("Label", image.transform, label, fontSize, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
        }

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        image.gameObject.AddComponent<UIPointerRelay>().playSounds = true;
        return button;
    }

    public static TextMeshProUGUI LabelOf(Button button) => button.GetComponentInChildren<TextMeshProUGUI>(true);

    public static VerticalLayoutGroup Vertical(RectTransform rect, float spacing = 8f, int padding = 0, TextAnchor align = TextAnchor.UpperLeft)
    {
        VerticalLayoutGroup group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        group.spacing = spacing;
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.childAlignment = align;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        return group;
    }

    public static HorizontalLayoutGroup Horizontal(RectTransform rect, float spacing = 8f, int padding = 0, TextAnchor align = TextAnchor.MiddleLeft)
    {
        HorizontalLayoutGroup group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.childAlignment = align;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        return group;
    }

    /// <summary>กำหนดขนาดของลูกใน Layout Group (ค่า ≤ 0 = ไม่กำหนด)</summary>
    public static LayoutElement Size(Component target, float width = -1f, float height = -1f, float flexibleWidth = -1f)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null)
            element = target.gameObject.AddComponent<LayoutElement>();

        if (width > 0f) { element.minWidth = width; element.preferredWidth = width; }
        if (height > 0f) { element.minHeight = height; element.preferredHeight = height; }
        if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
        return element;
    }

    public static Slider Slider(string name, Transform parent, float value, Action<float> onChanged)
    {
        RectTransform root = Rect(name, parent);
        Slider slider = root.gameObject.AddComponent<Slider>();

        Image background = Panel("Background", root, new Color(1f, 1f, 1f, 0.15f));
        background.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.65f);
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        RectTransform fillArea = Rect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        Image fill = Panel("Fill", fillArea, AccentColor, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = Stretch(Rect("Handle Slide Area", root), 10f, 0f, 10f, 0f);
        Image handle = Panel("Handle", handleArea, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(Mathf.Clamp01(value));

        if (onChanged != null)
            slider.onValueChanged.AddListener(v => onChanged(v));

        return slider;
    }

    /// <summary>Sprite วงกลมสีขาว (สร้างครั้งเดียว) ใช้ทำปุ่มวงกลมของ X-Gear</summary>
    public static Sprite CircleSprite
    {
        get
        {
            if (circleSprite != null)
                return circleSprite;

            // ใช้ไฟล์รูปใน Resources/UI ก่อน (Prefab ที่สร้างใน Editor อ้างถึงไฟล์นี้ได้)
            circleSprite = Resources.Load<Sprite>("UI/UI_Circle");
            if (circleSprite != null)
                return circleSprite;

            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            Vector2 center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f); // ขอบนุ่ม 1 พิกเซล
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            circleSprite.name = "UIBuilder Circle";
            return circleSprite;
        }
    }

    private static Sprite triangleSprite;

    /// <summary>Sprite สามเหลี่ยมชี้ลง (▽ แบบทึบ) ใช้เป็นลูกศรพับ/เปิด หมุน 180 องศา = ชี้ขึ้น</summary>
    public static Sprite TriangleSprite
    {
        get
        {
            if (triangleSprite != null)
                return triangleSprite;

            triangleSprite = Resources.Load<Sprite>("UI/UI_Triangle");
            if (triangleSprite != null)
                return triangleSprite;

            const int width = 64;
            const int height = 48;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                // y = 0 คือแถวล่าง (ปลายแหลม) / y = height-1 คือฐานด้านบน
                float halfWidth = (y + 0.5f) / height * (width * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float fromCenter = Mathf.Abs(x + 0.5f - width * 0.5f);
                    float alpha = Mathf.Clamp01(halfWidth - fromCenter + 0.5f); // ขอบนุ่ม 1 พิกเซล
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            triangleSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            triangleSprite.name = "UIBuilder Triangle";
            return triangleSprite;
        }
    }

    public static void Clear(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false); // ออกจาก Layout ทันที (Destroy จริงตอนท้ายเฟรม)
            UnityEngine.Object.Destroy(child);
        }
    }
}

// HudToast ย้ายไปอยู่ไฟล์ HudToast.cs (MonoBehaviour ต้องอยู่ไฟล์ชื่อเดียวกับคลาส)
