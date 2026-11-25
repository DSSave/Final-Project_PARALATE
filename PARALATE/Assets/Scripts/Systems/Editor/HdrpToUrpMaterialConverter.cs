using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// แปลง Material ที่ค้างจาก HDRP (และ Built-in Standard) ให้เป็น URP Lit ครบทุกช่อง
///
/// หลักการ: ตอนเปลี่ยน Shader ของ Material Unity ไม่ลบค่าเดิมทิ้ง (ยังอยู่ใน m_SavedProperties)
/// เราจึงอ่านค่าช่อง HDRP เดิมด้วย SerializedObject แล้วย้ายไปช่อง URP ที่ความหมายตรงกัน:
///   _BaseColorMap      → _BaseMap
///   _NormalMap/_NormalScale → _BumpMap/_BumpScale
///   _MaskMap (R=Metallic G=AO A=Smoothness) → _MetallicGlossMap + _OcclusionMap
///     (URP อ่าน Metallic จาก R, Smoothness จาก A, AO จาก G ของไฟล์เดียวกันได้พอดี ไม่ต้องแยกไฟล์ใหม่)
///   _EmissiveColorMap / _EmissiveColorLDR → _EmissionMap / _EmissionColor
///   _SurfaceType / _AlphaCutoffEnable / _AlphaCutoff → _Surface / _AlphaClip / _Cutoff
/// ส่วน Standard (Built-in): _MainTex/_Color/_Glossiness/_Mode → _BaseMap/_BaseColor/_Smoothness/_Surface
///
/// ไม่ทับงานที่ตั้งไว้แล้วใน URP: ช่องที่มีค่าอยู่แล้วจะไม่ถูกแตะ
/// ยกเว้น BaseMap ที่ชื่อเทกซ์เจอร์ไม่ตรงกับชื่อ Material (บั๊กของแพ็ก Gridbox เช่น Green3 ใช้รูป green2)
/// </summary>
public static class HdrpToUrpMaterialConverter
{
    private const string LitShaderName = "Universal Render Pipeline/Lit";

    /// <summary>ค่า Emission ใน URP ที่สูงกว่านี้ถือว่าเป็นค่า nits ที่ติดมาจาก HDRP (ค่าปกติใน URP อยู่แถว 0–10)</summary>
    private const float MaxUrpEmission = 100f;

    [MenuItem("Tools/Paralate/URP/Convert HDRP Materials (Dry Run)")]
    private static void DryRunMenu() => Debug.Log(Run(true));

    [MenuItem("Tools/Paralate/URP/Convert HDRP Materials")]
    private static void RunMenu() => Debug.Log(Run(false));

    /// <summary>dryRun = true: แค่รายงานว่าจะเปลี่ยนอะไร ไม่บันทึก</summary>
    public static string Run(bool dryRun)
    {
        Shader lit = Shader.Find(LitShaderName);
        if (lit == null)
            return "ไม่พบ Shader " + LitShaderName;

        StringBuilder report = new StringBuilder();
        int changedCount = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/TextMesh Pro/") || !path.EndsWith(".mat"))
                continue; // ไฟล์ตัวอย่างของ TMP ไม่ใช่ของเกม

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            List<string> changes = Convert(material, lit, dryRun);
            if (changes.Count == 0)
                continue;

            changedCount++;
            report.AppendLine(path + ": " + string.Join(", ", changes));
            if (!dryRun)
                EditorUtility.SetDirty(material);
        }

        if (!dryRun)
            AssetDatabase.SaveAssets();

        return (dryRun ? "[Dry Run] " : "") + "HDRP → URP Material: " + changedCount + " ไฟล์\n" + report;
    }

    private static List<string> Convert(Material m, Shader lit, bool dryRun)
    {
        List<string> changes = new List<string>();
        SerializedObject saved = new SerializedObject(m);

        string shaderName = m.shader != null ? m.shader.name : "";
        bool fromStandard = shaderName.StartsWith("Standard");
        bool fromHdrp = shaderName.StartsWith("HDRP/") || shaderName == "Hidden/InternalErrorShader";
        bool isLit = shaderName == LitShaderName;

        if (!fromStandard && !fromHdrp && !isLit)
            return changes; // Shader อื่น (UI, Unlit, Custom) ไม่ยุ่ง

        // ---- เปลี่ยน Shader (ค่าเก่ายังอยู่ใน saved ที่อ่านไว้ก่อนแล้ว)
        if (!isLit)
        {
            changes.Add(shaderName + " → URP Lit");
            if (dryRun)
                return changes; // Dry Run: Shader ยังไม่เปลี่ยน อ่านช่อง URP ไม่ได้ รายงานแค่นี้พอ
            m.shader = lit;
        }

        // ---- Base Color
        Texture hdBase = ReadTexture(saved, "_BaseColorMap", out Vector2 baseScale, out Vector2 baseOffset);
        Texture stdBase = ReadTexture(saved, "_MainTex", out Vector2 stdScale, out Vector2 stdOffset);
        Texture wantBase = fromStandard ? (stdBase != null ? stdBase : hdBase) : hdBase;
        if (fromStandard && stdBase != null) { baseScale = stdScale; baseOffset = stdOffset; }

        Texture currentBase = m.GetTexture("_BaseMap");
        if (wantBase != null && wantBase != currentBase && (currentBase == null || NameMismatch(m, currentBase, wantBase)))
        {
            changes.Add("BaseMap " + (currentBase ? currentBase.name : "-") + " → " + wantBase.name);
            if (!dryRun)
            {
                m.SetTexture("_BaseMap", wantBase);
                m.SetTextureScale("_BaseMap", baseScale);
                m.SetTextureOffset("_BaseMap", baseOffset);
                m.SetTexture("_MainTex", wantBase);
            }
        }

        if (fromStandard && ReadColor(saved, "_Color", out Color stdColor) && !dryRun)
            m.SetColor("_BaseColor", stdColor);
        if (fromStandard && ReadFloat(saved, "_Glossiness", out float gloss) && !dryRun)
            m.SetFloat("_Smoothness", gloss);

        // ---- Normal
        Texture hdNormal = ReadTexture(saved, "_NormalMap", out _, out _);
        if (hdNormal != null && m.GetTexture("_BumpMap") == null)
        {
            changes.Add("Normal " + hdNormal.name);
            if (!dryRun)
            {
                m.SetTexture("_BumpMap", hdNormal);
                if (ReadFloat(saved, "_NormalScale", out float normalScale))
                    m.SetFloat("_BumpScale", normalScale);
            }
        }

        // ---- Mask Map: R = Metallic, G = AO, A = Smoothness
        Texture mask = ReadTexture(saved, "_MaskMap", out _, out _);
        if (mask != null && m.GetTexture("_MetallicGlossMap") == null)
        {
            changes.Add("MaskMap " + mask.name + " → Metallic/Smoothness + Occlusion");
            if (!dryRun)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetFloat("_SmoothnessTextureChannel", 0f); // อ่าน Smoothness จาก Alpha ของ Metallic Map
                m.SetFloat("_Smoothness", ReadFloat(saved, "_SmoothnessRemapMax", out float smax) ? smax : 1f);
                if (m.GetTexture("_OcclusionMap") == null)
                {
                    m.SetTexture("_OcclusionMap", mask); // URP อ่าน AO จากช่อง G
                    m.SetFloat("_OcclusionStrength", 1f);
                }
            }
        }

        // ---- Emission
        Texture hdEmissionMap = ReadTexture(saved, "_EmissiveColorMap", out _, out _);
        bool hasLdr = ReadColor(saved, "_EmissiveColorLDR", out Color emissiveLdr);
        bool hdEmits = hdEmissionMap != null || (hasLdr && emissiveLdr.maxColorComponent > 0.001f);
        Color urpEmission = m.GetColor("_EmissionColor");
        bool urpEmits = m.IsKeywordEnabled("_EMISSION") && urpEmission.maxColorComponent > 0.001f;

        // ตอนแปลงครั้งก่อนบางไฟล์ได้ค่า Emission เป็นหน่วย nits ของ HDRP ติดมาตรงๆ (เช่น 10,000,000)
        // ใน URP ค่านี้สว่างจนขาวโพลน: ค่าเกิน MaxUrpEmission ถือว่าผิด ให้คิดใหม่จากสี LDR
        bool urpEmissionBroken = urpEmission.maxColorComponent > MaxUrpEmission;
        if (urpEmissionBroken)
            urpEmits = false;

        if (hdEmits && !urpEmits || urpEmissionBroken)
        {
            Color color = hasLdr && emissiveLdr.maxColorComponent > 0.001f ? emissiveLdr
                : urpEmissionBroken ? (urpEmission / urpEmission.maxColorComponent) // ไม่มีสี LDR: เก็บโทนสีเดิม ปรับให้สว่างสุด = 1
                : Color.white;
            color.a = 1f;
            changes.Add((urpEmissionBroken ? "Emission เกินจริง " + urpEmission.maxColorComponent.ToString("0") + " → " : "Emission ") +(hdEmissionMap ? hdEmissionMap.name + " " : "") + ColorUtility.ToHtmlStringRGB(color));
            if (!dryRun)
            {
                // HDRP เก็บความสว่างเป็นหน่วยแสงจริง (nits) URP ไม่มีหน่วยนี้ จึงใช้สี LDR ตรงๆ (ความสว่าง 1)
                // ถ้าอยากให้เรืองแสง/Bloom แรงขึ้น เพิ่ม Intensity ที่ช่อง Emission ใน Inspector
                if (hdEmissionMap != null && m.GetTexture("_EmissionMap") == null)
                    m.SetTexture("_EmissionMap", hdEmissionMap);
                m.SetColor("_EmissionColor", color);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }

        // ---- โปร่งใส / ตัดขอบ: แปลงเฉพาะ Material ที่เพิ่งเปลี่ยน Shader (URP Lit เดิมคงค่าที่ผู้ใช้ตั้งไว้)
        if (!isLit && !dryRun)
            ApplySurface(m, saved, fromStandard, changes);

        if (!dryRun && changes.Count > 0)
            UpdateLitKeywords(m);

        return changes;
    }

    private static void ApplySurface(Material m, SerializedObject saved, bool fromStandard, List<string> changes)
    {
        bool transparent = false;
        bool clip = false;
        float cutoff = 0.5f;

        if (fromStandard)
        {
            float mode = ReadFloat(saved, "_Mode", out float v) ? v : 0f; // 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent
            clip = Mathf.Approximately(mode, 1f);
            transparent = mode >= 2f;
            if (ReadFloat(saved, "_Cutoff", out float c)) cutoff = c;
        }
        else
        {
            transparent = ReadFloat(saved, "_SurfaceType", out float s) && s >= 1f;
            clip = ReadFloat(saved, "_AlphaCutoffEnable", out float a) && a >= 1f;
            if (ReadFloat(saved, "_AlphaCutoff", out float c)) cutoff = c;
        }

        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f); // Alpha
        m.SetFloat("_AlphaClip", clip ? 1f : 0f);
        m.SetFloat("_Cutoff", cutoff);
        if (transparent) changes.Add("Transparent");
        if (clip) changes.Add("AlphaClip " + cutoff);
    }

    /// <summary>ให้ URP ตั้ง Keyword / Blend / RenderQueue ตามค่าที่ใส่ (เหมือนกดแก้ใน Inspector)</summary>
    private static void UpdateLitKeywords(Material m)
    {
        BaseShaderGUI.SetMaterialKeywords(m, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
        MaterialEditor.FixupEmissiveFlag(m);
    }

    // ชื่อเทกซ์เจอร์ปัจจุบันไม่ตรงกับชื่อ Material แต่เทกซ์เจอร์จาก HDRP ตรง → ถือว่าตอนแปลงครั้งก่อนใส่รูปผิด
    private static bool NameMismatch(Material m, Texture current, Texture wanted)
    {
        string name = m.name.ToLowerInvariant();
        return !name.EndsWith(Suffix(current)) && name.EndsWith(Suffix(wanted));
    }

    private static string Suffix(Texture t)
    {
        string n = t.name.ToLowerInvariant();
        int i = n.LastIndexOf('_');
        return i >= 0 ? n.Substring(i + 1) : n;
    }

    // ---------------------------------------------------------------- อ่านค่าเดิมใน m_SavedProperties

    private static SerializedProperty FindEntry(SerializedObject so, string list, string key)
    {
        SerializedProperty array = so.FindProperty("m_SavedProperties." + list);
        if (array == null)
            return null;

        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == key)
                return entry.FindPropertyRelative("second");
        }
        return null;
    }

    private static Texture ReadTexture(SerializedObject so, string key, out Vector2 scale, out Vector2 offset)
    {
        scale = Vector2.one;
        offset = Vector2.zero;
        SerializedProperty value = FindEntry(so, "m_TexEnvs", key);
        if (value == null)
            return null;

        scale = value.FindPropertyRelative("m_Scale").vector2Value;
        offset = value.FindPropertyRelative("m_Offset").vector2Value;
        return value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
    }

    private static bool ReadFloat(SerializedObject so, string key, out float value)
    {
        SerializedProperty p = FindEntry(so, "m_Floats", key);
        value = p != null ? p.floatValue : 0f;
        return p != null;
    }

    private static bool ReadColor(SerializedObject so, string key, out Color value)
    {
        SerializedProperty p = FindEntry(so, "m_Colors", key);
        value = p != null ? p.colorValue : Color.black;
        return p != null;
    }
}
