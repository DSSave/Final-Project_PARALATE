using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ข้อความแจ้งเตือนสั้นๆ กลางจอด้านล่าง (เก็บของ, กระสุนหมด, เปลี่ยนอาวุธ ฯลฯ) สร้างตัวเองอัตโนมัติ</summary>
public class HudToast : MonoBehaviour
{
    private static HudToast instance;

    /// <summary>
    /// ปิดแถบข้อความแจ้งเตือนกลางล่างจอทั้งเกม (Locked, X-Gear depleted, + Scrap x1 ฯลฯ) ตามที่ขอ
    /// โค้ดที่เรียก HudToast.Show ยังอยู่ครบ ถ้าอยากเปิดกลับ เปลี่ยนเป็น true ที่บรรทัดนี้บรรทัดเดียว
    /// </summary>
    public static bool Enabled = false;

    private TextMeshProUGUI label;
    private CanvasGroup group;
    private float hideAt;

    public static void Show(string message, float duration = 1.6f)
    {
        if (!Enabled || !Application.isPlaying || string.IsNullOrEmpty(message))
            return;

        if (instance == null)
        {
            Canvas canvas = UIBuilder.CreateCanvas("HudToast (Auto)", 500);
            DontDestroyOnLoad(canvas.gameObject);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            instance = canvas.gameObject.AddComponent<HudToast>();
            instance.group = canvas.gameObject.AddComponent<CanvasGroup>();
            instance.group.blocksRaycasts = false;

            Image background = UIBuilder.Panel("Background", canvas.transform, new Color(0f, 0f, 0f, 0.55f), false);
            UIBuilder.Place(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(620f, 54f));
            instance.label = UIBuilder.Text("Label", background.transform, string.Empty, 26f, TextAlignmentOptions.Center);
            UIBuilder.Stretch(instance.label.rectTransform, 12f, 4f, 12f, 4f);
        }

        instance.label.text = message;
        instance.hideAt = Time.unscaledTime + duration;
        instance.group.alpha = 1f;
    }

    private void Update()
    {
        if (group.alpha <= 0f)
            return;

        float remaining = hideAt - Time.unscaledTime;
        group.alpha = Mathf.Clamp01(remaining / 0.35f);
    }
}
