using UnityEditor;
using UnityEngine;

/// <summary>
/// เปลี่ยน Material ที่เลือกเป็น Shader "Paralate/PSX Lit" โดยคง Texture / สี / Emission / Alpha Clip เดิมไว้
/// เมนู: Tools > Paralate > PSX > Convert Selected Materials (หรือคลิกขวาที่ Material > Paralate > Convert to PSX Lit)
/// ย้อนกลับได้ด้วย Ctrl+Z
/// </summary>
public static class PsxMaterialConverter
{
    private const string ShaderName = "Paralate/PSX Lit";

    [MenuItem("Tools/Paralate/PSX/Convert Selected Materials")]
    [MenuItem("Assets/Paralate/Convert to PSX Lit")]
    private static void ConvertSelected()
    {
        Shader psx = Shader.Find(ShaderName);
        if (psx == null)
        {
            Debug.LogError("[PSX] ไม่พบ Shader " + ShaderName);
            return;
        }

        int count = 0;
        foreach (Object obj in Selection.objects)
        {
            if (!(obj is Material mat) || mat.shader == psx)
                continue;

            Undo.RecordObject(mat, "Convert to PSX Lit");

            // จำค่าเดิมก่อนเปลี่ยน Shader (ชื่อ property ต่างกันระหว่าง URP Lit / Unlit / Built-in)
            Texture baseMap = FirstTexture(mat, "_BaseMap", "_MainTex", "_BaseColorMap");
            Vector2 scale = baseMap != null ? FirstScale(mat) : Vector2.one;
            Vector2 offset = baseMap != null ? FirstOffset(mat) : Vector2.zero;
            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture emissionMap = FirstTexture(mat, "_EmissionMap", "_EmissiveColorMap");
            Color emission = mat.HasProperty("_EmissionColor") && mat.IsKeywordEnabled("_EMISSION")
                ? mat.GetColor("_EmissionColor") : Color.black;
            bool alphaClip = mat.IsKeywordEnabled("_ALPHATEST_ON");
            float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

            mat.shader = psx;

            mat.SetTexture("_BaseMap", baseMap);
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetColor("_BaseColor", color);
            if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
            mat.SetColor("_EmissionColor", emission);
            mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            mat.SetFloat("_Cutoff", cutoff);
            if (alphaClip) mat.EnableKeyword("_ALPHATEST_ON");
            else mat.DisableKeyword("_ALPHATEST_ON");

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PSX] เปลี่ยนเป็น PSX Lit แล้ว " + count + " Material");
    }

    [MenuItem("Tools/Paralate/PSX/Convert Selected Materials", true)]
    [MenuItem("Assets/Paralate/Convert to PSX Lit", true)]
    private static bool ValidateSelected()
    {
        foreach (Object obj in Selection.objects)
            if (obj is Material)
                return true;
        return false;
    }

    private static Texture FirstTexture(Material mat, params string[] names)
    {
        foreach (string n in names)
            if (mat.HasProperty(n) && mat.GetTexture(n) != null)
                return mat.GetTexture(n);
        return null;
    }

    private static Vector2 FirstScale(Material mat)
    {
        if (mat.HasProperty("_BaseMap")) return mat.GetTextureScale("_BaseMap");
        if (mat.HasProperty("_MainTex")) return mat.GetTextureScale("_MainTex");
        return Vector2.one;
    }

    private static Vector2 FirstOffset(Material mat)
    {
        if (mat.HasProperty("_BaseMap")) return mat.GetTextureOffset("_BaseMap");
        if (mat.HasProperty("_MainTex")) return mat.GetTextureOffset("_MainTex");
        return Vector2.zero;
    }
}
