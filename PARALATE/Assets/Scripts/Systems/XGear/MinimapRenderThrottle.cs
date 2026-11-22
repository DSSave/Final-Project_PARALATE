using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ลดภาระของกล้อง Minimap สำหรับ URP:
///   1) เรนเดอร์เป็นช่วงๆ (ค่าเริ่มต้น 10 ครั้ง/วินาที)
///   2) ปิดเงา, Post-processing และ texture เสริมใน UniversalAdditionalCameraData
///   3) SetVisible(false) = ปิดกล้องสนิท
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MinimapRenderThrottle : MonoBehaviour
{
    [Tooltip("จำนวนครั้งที่เรนเดอร์ต่อวินาที (0 = ทุกเฟรม)")]
    public float rendersPerSecond = 10f;

    private Camera targetCamera;
    private bool visible = true;
    private float nextRenderTime;

    public static MinimapRenderThrottle EnsureOn(Camera camera)
    {
        if (camera == null)
            return null;

        MinimapRenderThrottle throttle = camera.GetComponent<MinimapRenderThrottle>();
        if (throttle == null)
            throttle = camera.gameObject.AddComponent<MinimapRenderThrottle>();

        return throttle;
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyCheapFrameSettings();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        nextRenderTime = 0f; // เปิดกลับมาแล้วเรนเดอร์ทันที 1 ครั้ง

        if (!value && targetCamera != null)
            targetCamera.enabled = false;
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

        if (!visible)
        {
            if (targetCamera.enabled)
                targetCamera.enabled = false;

            return;
        }

        if (rendersPerSecond <= 0f)
        {
            targetCamera.enabled = true;
            return;
        }

        // เปิดกล้องเฉพาะเฟรมที่ถึงรอบ (กล้องที่ enabled ตอนจบ Update จะถูกเรนเดอร์ในเฟรมนั้น)
        bool due = Time.unscaledTime >= nextRenderTime;
        if (due)
            nextRenderTime = Time.unscaledTime + 1f / rendersPerSecond;

        if (targetCamera.enabled != due)
            targetCamera.enabled = due;
    }

    private void ApplyCheapFrameSettings()
    {
        UniversalAdditionalCameraData data = GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
            return;

        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.requiresDepthTexture = false;
        data.requiresColorTexture = false;
    }
}
