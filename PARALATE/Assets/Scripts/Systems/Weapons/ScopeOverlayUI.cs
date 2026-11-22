using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// กรอบกล้องเล็งของ Sniper (จอดำ + ช่องวงกลม + เส้นกากบาท) แสดงตอนคลิกขวาซูมด้วยปืนชนิด Sniper
/// สร้างตัวเองด้วยโค้ดครั้งแรกที่ใช้ ภาพช่องวงกลมสร้างเป็น Texture ตอนรัน จึงไม่ต้องมีไฟล์ภาพ
/// อยากใช้ภาพของตัวเอง: วาง Sprite ไว้ที่ Resources/UI/sniper_scope
/// </summary>
public class ScopeOverlayUI : MonoBehaviour
{
    private static ScopeOverlayUI instance;

    public static void SetVisible(bool visible)
    {
        if (!Application.isPlaying)
            return;

        if (instance == null)
        {
            if (!visible)
                return;

            Canvas canvas = UIBuilder.CreateCanvas("ScopeOverlay (Auto)", 100);
            DontDestroyOnLoad(canvas.gameObject);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            instance = canvas.gameObject.AddComponent<ScopeOverlayUI>();
            instance.Build();
        }

        instance.gameObject.SetActive(visible);
    }

    private void Build()
    {
        const float lensSize = 1080f; // เต็มความสูงของจออ้างอิง
        Color black = Color.black;

        // เลนส์กลางจอ
        Image lens = UIBuilder.Panel("Lens", transform, Color.white, false);
        UIBuilder.Place(lens.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(lensSize, lensSize));

        Sprite custom = Resources.Load<Sprite>("UI/sniper_scope");
        lens.sprite = custom != null ? custom : CreateLensSprite();

        // แถบดำซ้าย/ขวา ปิดส่วนที่เหลือของจอ (เผื่อจอกว้างกว่า 16:9)
        Image left = UIBuilder.Panel("Left", transform, black, false);
        left.rectTransform.anchorMin = new Vector2(0f, 0f);
        left.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        left.rectTransform.offsetMin = new Vector2(-2000f, -200f);
        left.rectTransform.offsetMax = new Vector2(-lensSize * 0.5f + 1f, 200f);

        Image right = UIBuilder.Panel("Right", transform, black, false);
        right.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        right.rectTransform.anchorMax = new Vector2(1f, 1f);
        right.rectTransform.offsetMin = new Vector2(lensSize * 0.5f - 1f, -200f);
        right.rectTransform.offsetMax = new Vector2(2000f, 200f);

        if (custom != null)
            return;

        // เส้นกากบาท
        Image horizontal = UIBuilder.Panel("CrossH", transform, black, false);
        UIBuilder.Place(horizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(lensSize, 2f));

        Image vertical = UIBuilder.Panel("CrossV", transform, black, false);
        UIBuilder.Place(vertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, lensSize));
    }

    // สี่เหลี่ยมดำที่มีรูวงกลมโปร่งใสตรงกลาง + ขอบมืดไล่เข้าหาขอบเลนส์
    private static Sprite CreateLensSprite()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        float radius = size * 0.47f;
        Vector2 center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outside = Mathf.Clamp01(distance - radius + 0.5f);              // นอกวงกลม = ดำทึบ
                float vignette = Mathf.InverseLerp(radius * 0.75f, radius, distance); // ขอบในเลนส์มืดลงเล็กน้อย
                float alpha = Mathf.Max(outside, vignette * vignette * 0.6f);
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
