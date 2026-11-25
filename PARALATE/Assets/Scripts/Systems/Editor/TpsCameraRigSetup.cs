using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สร้างกล้อง TPS (PlayerTpsCameraRig) ใต้ตัวผู้เล่นที่เลือก: เลือก Player ใน Hierarchy แล้วกด
/// Tools > Paralate > Camera > Add TPS Camera Rig
///
/// โครงที่ได้ (ปรับทุกค่าใน Inspector ได้):
///   Player
///   └ TPSCameraRig           (PlayerTpsCameraRig: ความไวเมาส์, Priority, กติกาการเล็ง)
///     ├ TPS_NormalCam        (OrbitalFollow = ระยะ/ความสูงรอบตัว, RotationComposer = ตำแหน่งผู้เล่นบนจอ, Deoccluder = กันทะลุกำแพง)
///     ├ TPS_AimCam           (ThirdPersonFollow = ตำแหน่งหลังไหล่ตอนเล็ง)
///     ├ AimPivot             (ความสูงไหล่ = จุดหมุนกล้องเล็ง)
///     └ TPS_CrosshairCanvas  (AimDot = จุดเป้าเล็ง เปลี่ยนรูป/สี/ขนาดได้)
/// </summary>
public static class TpsCameraRigSetup
{
    public const string RigName = "TPSCameraRig";

    [MenuItem("Tools/Paralate/Camera/Add TPS Camera Rig (Selected Player)")]
    private static void AddToSelection()
    {
        GameObject selected = Selection.activeGameObject;
        TopDownPlayerController player = selected != null ? selected.GetComponentInParent<TopDownPlayerController>() : null;
        if (player == null)
        {
            EditorUtility.DisplayDialog("TPS Camera Rig", "เลือกตัวผู้เล่น (ที่มี TopDownPlayerController) ใน Hierarchy ก่อน", "OK");
            return;
        }

        PlayerTpsCameraRig rig = Build(player);
        Selection.activeGameObject = rig.gameObject;
    }

    [MenuItem("Tools/Paralate/Camera/Add TPS Camera Rig (Selected Player)", true)]
    private static bool AddToSelectionValidate() => !Application.isPlaying;

    /// <summary>สร้าง (หรือคืนตัวเดิมถ้ามีอยู่แล้ว) กล้อง TPS ใต้ผู้เล่น</summary>
    public static PlayerTpsCameraRig Build(TopDownPlayerController player)
    {
        PlayerTpsCameraRig existing = player.GetComponentInChildren<PlayerTpsCameraRig>(true);
        if (existing != null)
            return existing;

        Transform playerT = player.transform;

        // ความสูงอ้างอิงจาก CharacterController: Pivot ของผู้เล่นอาจอยู่กลางตัว (ไม่ใช่ที่เท้า)
        CharacterController cc = player.GetComponent<CharacterController>();
        float feetY = cc != null ? cc.center.y - cc.height * 0.5f : 0f;
        float height = cc != null ? cc.height : 2f;
        float shoulderY = feetY + height * 0.78f; // ระดับไหล่ ~78% ของความสูงตัว
        float lookY = feetY + height * 0.75f;     // จุดที่กล้องปกติเล็ง (ผู้เล่นอยู่กลางจอ)

        int cameraMask = LayerMask.GetMask("Default", "Hurdle", "Ladder", "Obstacle", "Ground");
        if (cameraMask == 0)
            cameraMask = 1;

        GameObject rigGo = new GameObject(RigName);
        Undo.RegisterCreatedObjectUndo(rigGo, "Add TPS Camera Rig");
        rigGo.transform.SetParent(playerT, false);
        PlayerTpsCameraRig rig = rigGo.AddComponent<PlayerTpsCameraRig>();
        rig.player = player;

        // ---- กล้องปกติ: ผู้เล่นกลางจอ หมุนรอบตัวด้วยเมาส์
        GameObject normalGo = new GameObject("TPS_NormalCam");
        normalGo.transform.SetParent(rigGo.transform, false);
        CinemachineCamera normal = normalGo.AddComponent<CinemachineCamera>();
        normal.Target = new CameraTarget { TrackingTarget = playerT, LookAtTarget = playerT, CustomLookAtTarget = false };
        normal.Priority.Value = 0; // สคริปต์ตั้ง Normal Priority ให้ตอนเริ่มเล่น
        LensSettings lens = normal.Lens;
        lens.FieldOfView = 55f;
        lens.NearClipPlane = 0.1f;
        normal.Lens = lens;

        CinemachineOrbitalFollow orbit = normalGo.AddComponent<CinemachineOrbitalFollow>();
        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.Radius = 4.2f;
        orbit.TargetOffset = new Vector3(0f, lookY, 0f);
        TrackerSettings tracker = orbit.TrackerSettings;
        tracker.BindingMode = BindingMode.WorldSpace; // กล้องไม่หมุนตามการหันตัวของผู้เล่น (หมุนด้วยเมาส์เท่านั้น)
        tracker.PositionDamping = new Vector3(0.15f, 0.15f, 0.15f);
        orbit.TrackerSettings = tracker;
        orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
        orbit.HorizontalAxis.Wrap = true;
        orbit.HorizontalAxis.Recentering.Enabled = false;
        orbit.VerticalAxis.Range = new Vector2(-10f, 55f);
        orbit.VerticalAxis.Value = 12f;
        orbit.VerticalAxis.Recentering.Enabled = false;
        orbit.RadialAxis.Recentering.Enabled = false;

        CinemachineRotationComposer composer = normalGo.AddComponent<CinemachineRotationComposer>();
        composer.TargetOffset = new Vector3(0f, lookY, 0f);
        composer.Damping = new Vector2(0.05f, 0.05f);
        ScreenComposerSettings composition = composer.Composition;
        composition.ScreenPosition = Vector2.zero; // กลางจอ
        composition.DeadZone.Enabled = false;
        composer.Composition = composition;

        CinemachineDeoccluder deoccluder = normalGo.AddComponent<CinemachineDeoccluder>();
        deoccluder.CollideAgainst = cameraMask;
        deoccluder.IgnoreTag = "Player";
        deoccluder.MinimumDistanceFromTarget = 0.3f;
        CinemachineDeoccluder.ObstacleAvoidance avoid = deoccluder.AvoidObstacles;
        avoid.Enabled = true;
        avoid.DistanceLimit = 0f;
        avoid.MinimumOcclusionTime = 0f;
        avoid.CameraRadius = 0.2f;
        avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
        avoid.MaximumEffort = 4;
        avoid.SmoothingTime = 0.2f;
        avoid.Damping = 0.3f;
        avoid.DampingWhenOccluded = 0f;
        deoccluder.AvoidObstacles = avoid;

        // ---- จุดหมุนกล้องเล็ง (ระดับไหล่)
        GameObject pivotGo = new GameObject("AimPivot");
        pivotGo.transform.SetParent(rigGo.transform, false);
        pivotGo.transform.localPosition = new Vector3(0f, shoulderY, 0f);

        // ---- กล้องเล็งไหล่แบบ RE4
        GameObject aimGo = new GameObject("TPS_AimCam");
        aimGo.transform.SetParent(rigGo.transform, false);
        CinemachineCamera aim = aimGo.AddComponent<CinemachineCamera>();
        aim.Target = new CameraTarget { TrackingTarget = pivotGo.transform };
        aim.Priority.Value = 0;
        LensSettings aimLens = aim.Lens;
        aimLens.FieldOfView = 45f;
        aimLens.NearClipPlane = 0.05f;
        aim.Lens = aimLens;

        CinemachineThirdPersonFollow shoulder = aimGo.AddComponent<CinemachineThirdPersonFollow>();
        shoulder.ShoulderOffset = new Vector3(0.55f, 0.1f, 0f); // X บวก = ไหล่ขวา
        shoulder.VerticalArmLength = 0.1f;
        shoulder.CameraSide = 1f;
        shoulder.CameraDistance = 1.6f;
        shoulder.Damping = new Vector3(0.02f, 0.05f, 0.1f);
        CinemachineThirdPersonFollow.ObstacleSettings shoulderAvoid = shoulder.AvoidObstacles;
        shoulderAvoid.Enabled = true;
        shoulderAvoid.CollisionFilter = cameraMask;
        shoulderAvoid.IgnoreTag = "Player";
        shoulderAvoid.CameraRadius = 0.15f;
        shoulder.AvoidObstacles = shoulderAvoid;

        aimGo.AddComponent<CinemachineRotateWithFollowTarget>();

        // ---- เป้าเล็ง (จุดแดง) บน Canvas ของตัวเอง: เปลี่ยนรูป/สี/ขนาดได้ใน Inspector
        GameObject canvasGo = new GameObject("TPS_CrosshairCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(rigGo.transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dotGo = new GameObject("AimDot", typeof(RectTransform));
        dotGo.transform.SetParent(canvasGo.transform, false);
        Image dot = dotGo.AddComponent<Image>();
        dot.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/UI_Circle.png");
        dot.color = rig.autoDotColor;
        dot.raycastTarget = false;
        RectTransform dotRect = dot.rectTransform;
        dotRect.anchorMin = dotRect.anchorMax = Vector2.zero;
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(rig.autoDotSize, rig.autoDotSize);
        dotGo.SetActive(false);

        rig.normalCamera = normal;
        rig.aimCamera = aim;
        rig.aimPivot = pivotGo.transform;
        rig.crosshair = dotRect;

        EditorUtility.SetDirty(rig);
        return rig;
    }
}
