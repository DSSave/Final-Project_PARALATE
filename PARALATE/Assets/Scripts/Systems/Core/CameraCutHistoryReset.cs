using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// รีเซ็ต History เฟรมก่อนหน้าตอน Cinemachine ตัดกล้อง (Cut) ใน URP
/// ป้องกัน Motion Blur / TAA เบลอป้ายทั้งจอเมื่อเปลี่ยนห้อง
/// </summary>
public static class CameraCutHistoryReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        CinemachineCore.CameraActivatedEvent.RemoveListener(OnCameraActivated);
        CinemachineCore.CameraActivatedEvent.AddListener(OnCameraActivated);
    }

    private static void OnCameraActivated(ICinemachineCamera.ActivationEventParams evt)
    {
        // เปลี่ยนแบบ Blend ไม่ต้องรีเซ็ต
        if (!evt.IsCut || evt.OutgoingCamera == null)
            return;

        CinemachineBrain brain = evt.Origin as CinemachineBrain;
        Camera camera = brain != null ? brain.OutputCamera : null;
        if (camera == null)
            return;

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data != null)
            data.resetHistory = true;
    }
}
