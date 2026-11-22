#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// ต่อแอนิเมชันใหม่ของ Mige เข้า PlayerAnim.controller (เมนู Tools > Paralate > Setup Player Animations)
/// รันเองอัตโนมัติ 1 ครั้งหลัง Unity คอมไพล์สคริปต์ ถ้ายังไม่เคยต่อ และรันซ้ำได้ไม่สร้างของซ้ำ
///
///   Crouch Idle : ย่อแล้วยืนนิ่ง → state "CrouchIdle" (เดิมย่อนิ่งๆ ก็เล่นท่าเดินย่อง)
///   Cover       : พิงกำแพง → state "WallHug" เป็น Blend Tree 1 มิติ คุมด้วย float "WallHugMove"
///                 -1 = MigeLeftCoverSneak, 0 = MigeCoverIdle, +1 = MigeRightCoverSneak
///                 (โค้ดส่ง WallHugMove = ทิศที่เดินตามแนวกำแพง ฝั่งขวาของตัวละครเป็นบวก)
///
/// พารามิเตอร์ที่โค้ดส่งมา (TopDownPlayerController): isWalk, isSneak (= กำลังย่อ), isWallHugging, WallHugMove
/// ให้ Unity เป็นคนเขียนไฟล์ .controller / .meta เอง (ผ่าน AnimatorController / ModelImporter API) ไม่แก้ YAML ด้วยมือ
/// </summary>
public static class PlayerAnimSetup
{
    private const string Folder = "Assets/ArtAssets/Model/Character/";
    private const string ControllerPath = Folder + "PlayerAnim.controller";
    private const string CrouchIdlePath = Folder + "NewMige/MigeCrouchIdle.fbx";
    private const string CoverLeftPath = Folder + "NewMige/MigeLeftCoverSneak.fbx";
    private const string CoverRightPath = Folder + "NewMige/MigeRightCoverSneak.fbx";
    private const string CoverIdlePath = Folder + "NewMige/MigeCoverIdle.fbx";

    /// <summary>ถ้าท่าซ้าย/ขวาสลับกันตอนเล่นจริง เปลี่ยนเป็น true แล้วรันเมนูอีกครั้ง</summary>
    private const bool SwapCoverSides = false;

    /// <summary>
    /// หมุนคลิปชุด Cover รอบแกนตั้ง (องศา): ไฟล์ชุดนี้ถูกทำมาโดยหันหน้ากลับด้านกับทิศ "หน้า" ของตัวละครในเกม
    /// (ตอนพิงกำแพงโค้ดให้ตัวละครหันหน้าออกจากกำแพง) จึงหมุนคลิป 180 องศาตอน import
    /// </summary>
    private const float CoverYawOffset = 180f;

    /// <summary>
    /// ค่าหมุนของท่าพิงนิ่ง (MigeCoverIdle) แยกจากคลิปเดินซ้าย/ขวา เผื่อไฟล์หันคนละทิศ
    /// ถ้าท่าพิงนิ่งหันหน้าเข้ากำแพง ให้สลับค่านี้ระหว่าง 180 กับ 0 แล้วรันเมนูอีกครั้ง
    /// </summary>
    private const float CoverIdleYawOffset = 0f;

    private const float TransitionTime = 0.25f;

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !NeedsSetup())
                return;

            Run();
        };
    }

    [MenuItem("Tools/Paralate/Setup Player Animations")]
    public static void Run()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogWarning("[PlayerAnimSetup] ไม่พบ " + ControllerPath);
            return;
        }

        AnimationClip crouchIdle = PrepareClip(CrouchIdlePath, "MigeCrouchIdle", true, 0f);
        AnimationClip coverLeft = PrepareClip(CoverLeftPath, "MigeLeftCoverSneak", false, CoverYawOffset);
        AnimationClip coverRight = PrepareClip(CoverRightPath, "MigeRightCoverSneak", false, CoverYawOffset);
        AnimationClip coverIdle = PrepareClip(CoverIdlePath, "MigeCoverIdle", true, CoverIdleYawOffset);

        if (crouchIdle == null || coverLeft == null || coverRight == null)
        {
            Debug.LogWarning("[PlayerAnimSetup] ไม่พบคลิปแอนิเมชันใหม่ของ Mige ครบทุกไฟล์ จึงยังไม่ได้ต่อ Animator");
            return;
        }

        EnsureParameter(controller, "isWalk", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "isSneak", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "isWallHugging", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "WallHugMove", AnimatorControllerParameterType.Float);
        // โค้ดปีนบันไดส่ง 3 ตัวนี้อยู่แล้ว: เพิ่มไว้กัน Unity เตือน "Parameter does not exist" (ยังไม่มี state ปีน)
        EnsureParameter(controller, "IsClimbing", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "ClimbSpeed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "ClimbTopExit", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = FindState(machine, "Idle");
        AnimatorState walk = FindState(machine, "Walk");
        AnimatorState sneak = FindState(machine, "Sneak");

        if (idle == null || walk == null || sneak == null)
        {
            Debug.LogWarning("[PlayerAnimSetup] PlayerAnim.controller ต้องมี state Idle / Walk / Sneak");
            return;
        }

        AnimatorState crouch = FindState(machine, "CrouchIdle");
        if (crouch == null)
            crouch = machine.AddState("CrouchIdle", new Vector3(300f, 200f, 0f));
        crouch.motion = crouchIdle;

        AnimatorState wallHug = FindState(machine, "WallHug");
        if (wallHug == null)
            wallHug = machine.AddState("WallHug", new Vector3(550f, 120f, 0f));

        // Blend Tree ของท่าพิงกำแพง (ใช้ตัวเดิมถ้ามี ไม่งั้นสร้างใหม่แล้วฝังไว้ในไฟล์ controller)
        BlendTree tree = wallHug.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree { name = "CoverBlend", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
            wallHug.motion = tree;
        }

        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "WallHugMove";
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            Child(SwapCoverSides ? coverRight : coverLeft, -1f),
            Child(coverIdle != null ? coverIdle : crouchIdle, 0f),
            Child(SwapCoverSides ? coverLeft : coverRight, 1f),
        };

        // สร้าง Transition ใหม่ทั้งชุด (ลบของเดิมก่อน จึงรันซ้ำได้)
        AnimatorState[] ground = { idle, walk, sneak, crouch };
        for (int i = 0; i < ground.Length; i++)
            ClearTransitions(ground[i]);
        ClearTransitions(wallHug);

        // พิงกำแพงมาก่อนเสมอ (Transition ที่อยู่บนสุดถูกเช็กก่อน)
        for (int i = 0; i < ground.Length; i++)
            Link(ground[i], wallHug, ("isWallHugging", true));
        Link(wallHug, idle, ("isWallHugging", false));

        // ยืน/เดิน/ย่อนิ่ง/เดินย่อง เลือกจาก 2 ค่า: isWalk (กำลังเดิน) x isSneak (กำลังย่อ)
        Link(idle, walk, ("isWalk", true), ("isSneak", false));
        Link(idle, sneak, ("isWalk", true), ("isSneak", true));
        Link(idle, crouch, ("isWalk", false), ("isSneak", true));

        Link(walk, idle, ("isWalk", false), ("isSneak", false));
        Link(walk, sneak, ("isWalk", true), ("isSneak", true));
        Link(walk, crouch, ("isWalk", false), ("isSneak", true));

        Link(sneak, crouch, ("isWalk", false), ("isSneak", true));
        Link(sneak, walk, ("isWalk", true), ("isSneak", false));
        Link(sneak, idle, ("isWalk", false), ("isSneak", false));

        Link(crouch, sneak, ("isWalk", true), ("isSneak", true));
        Link(crouch, idle, ("isWalk", false), ("isSneak", false));
        Link(crouch, walk, ("isWalk", true), ("isSneak", false));

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerAnimSetup] DONE: CrouchIdle + WallHug (cover left / idle / right) ต่อเข้า PlayerAnim.controller แล้ว");
    }

    // ยังไม่ได้ต่อ = ค่า import ของคลิปยังไม่ตรง, ไม่มี state CrouchIdle หรือ Blend Tree ของ WallHug ยังมีช่องว่าง
    private static bool NeedsSetup()
    {
        if (ClipOutdated(CrouchIdlePath, true, 0f) || ClipOutdated(CoverLeftPath, false, CoverYawOffset) ||
            ClipOutdated(CoverRightPath, false, CoverYawOffset) || ClipOutdated(CoverIdlePath, true, CoverIdleYawOffset))
            return true;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length == 0)
            return false;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(CrouchIdlePath) == null)
            return false;

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState crouch = FindState(machine, "CrouchIdle");
        AnimatorState wallHug = FindState(machine, "WallHug");

        if (crouch == null || crouch.motion == null || wallHug == null)
            return true;

        BlendTree tree = wallHug.motion as BlendTree;
        if (tree == null || tree.children.Length < 3)
            return true;

        for (int i = 0; i < tree.children.Length; i++)
        {
            if (tree.children[i].motion == null)
                return true;
        }

        // ช่องกลางของ Blend Tree (ท่าพิงนิ่ง) ยังชี้ไฟล์เก่าอยู่
        return AssetDatabase.GetAssetPath(tree.children[1].motion) != CoverIdlePath;
    }

    // มีไฟล์อยู่ แต่ค่า import ยังไม่ตรงกับที่ PrepareClip จะตั้ง
    private static bool ClipOutdated(string path, bool bakePositionXZ, float yawOffset)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return false;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            return true;

        ModelImporterClipAnimation clip = clips[0];
        return !clip.loopTime || !clip.lockRootRotation || !clip.keepOriginalOrientation ||
               !clip.lockRootHeightY || !clip.keepOriginalPositionY ||
               clip.lockRootPositionXZ != bakePositionXZ || !Mathf.Approximately(clip.rotationOffset, yawOffset);
    }

    /// <summary>
    /// ตั้งค่า import ของไฟล์ FBX แอนิเมชัน แล้วคืนคลิปที่ได้: ตั้งชื่อคลิป, Loop Time, Bake Into Pose ของการหมุนและความสูง
    ///
    /// bakePositionXZ (Root Transform Position XZ > Bake Into Pose):
    ///   true  = การเลื่อนตัวในคลิปถูกนับเป็น "ท่า" → ใช้กับท่ายืนนิ่ง
    ///   false = การเลื่อนตัวถูกนับเป็น Root Motion → ใช้กับท่าเดิน เพราะ Animator ของผู้เล่นปิด Apply Root Motion
    ///           การเลื่อนจึงถูกทิ้ง ตัวละครย่ำอยู่กับที่ แล้วโค้ด (CharacterController) เป็นคนพาเดินจริง
    ///           ถ้าตั้ง true กับท่าเดิน โมเดลจะเดินหนีออกจากจุดยืนของตัวละครแล้ววาร์ปกลับทุกรอบลูป
    /// yawOffset: หมุนคลิปรอบแกนตั้ง (Root Transform Rotation > Offset)
    /// </summary>
    private static AnimationClip PrepareClip(string path, string clipName, bool bakePositionXZ, float yawOffset)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return null;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        bool changed = false;

        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
            changed = true;
        }

        if (clips.Length > 0)
        {
            ModelImporterClipAnimation clip = clips[0];

            if (clip.name != clipName) { clip.name = clipName; changed = true; }
            if (!clip.loopTime) { clip.loopTime = true; changed = true; }
            if (!clip.lockRootRotation) { clip.lockRootRotation = true; changed = true; }
            if (!clip.keepOriginalOrientation) { clip.keepOriginalOrientation = true; changed = true; }
            if (!clip.lockRootHeightY) { clip.lockRootHeightY = true; changed = true; }
            if (!clip.keepOriginalPositionY) { clip.keepOriginalPositionY = true; changed = true; }
            if (clip.lockRootPositionXZ != bakePositionXZ) { clip.lockRootPositionXZ = bakePositionXZ; changed = true; }
            if (!Mathf.Approximately(clip.rotationOffset, yawOffset)) { clip.rotationOffset = yawOffset; changed = true; }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip found && !found.name.StartsWith("__preview__"))
                return found;
        }

        return null;
    }

    private static ChildMotion Child(Motion motion, float threshold)
    {
        return new ChildMotion { motion = motion, threshold = threshold, timeScale = 1f };
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
                return;
        }

        controller.AddParameter(name, type);
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == name)
                return states[i].state;
        }

        return null;
    }

    private static void ClearTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        for (int i = 0; i < transitions.Length; i++)
            state.RemoveTransition(transitions[i]);
    }

    private static void Link(AnimatorState from, AnimatorState to, params (string parameter, bool value)[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = TransitionTime;

        for (int i = 0; i < conditions.Length; i++)
        {
            transition.AddCondition(
                conditions[i].value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f, conditions[i].parameter);
        }
    }
}
#endif
