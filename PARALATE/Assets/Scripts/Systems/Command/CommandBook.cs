using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวกลางของสูตร CommandLine: อ่านสูตรจาก CommandBookData, จำว่าผู้เล่นรู้สูตรไหนแล้ว (PlayerPrefs), นับ Cooldown ปืน
/// สูตรของไอเทมจะ "รู้" เมื่อเก็บไอเทมนั้นครั้งแรก (Learn) → ขึ้นในหน้า Esc > Logs > Command
/// </summary>
public static class CommandBook
{
    private const string LearnedKey = "Commands_Learned_v1";

    private static CommandBookData data;
    private static List<CommandRecipe> fallback;
    private static HashSet<string> learned;
    private static readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();

    /// <summary>รู้สูตรใหม่ (หน้า Logs ใช้รีเฟรช)</summary>
    public static event Action<CommandRecipe> OnLearned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        fallback = null;
        learned = null;
        readyAt.Clear();
        OnLearned = null;
    }

    public static int MaxLength => Data != null ? Mathf.Max(3, Data.maxLength) : 8;

    private static CommandBookData Data
    {
        get
        {
            if (data == null)
                data = Resources.Load<CommandBookData>("CommandBook");
            return data;
        }
    }

    public static IReadOnlyList<CommandRecipe> All
    {
        get
        {
            if (Data != null && Data.recipes != null && Data.recipes.Count > 0)
                return Data.recipes;

            return fallback ??= CommandBookData.Defaults();
        }
    }

    // ------------------------------------------------------------ สูตรที่รู้แล้ว

    private static HashSet<string> Learned
    {
        get
        {
            if (learned != null)
                return learned;

            learned = new HashSet<string>();
            string saved = PlayerPrefs.GetString(LearnedKey, string.Empty);
            foreach (string id in saved.Split(';'))
            {
                if (!string.IsNullOrEmpty(id))
                    learned.Add(id);
            }

            return learned;
        }
    }

    public static bool IsKnown(CommandRecipe recipe) =>
        recipe != null && (recipe.knownFromStart || Learned.Contains(recipe.id));

    /// <summary>เก็บไอเทม itemId ได้: ปลดล็อกทุกสูตรที่ใช้ไอเทมนั้น</summary>
    public static void LearnFromItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        IReadOnlyList<CommandRecipe> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            CommandRecipe recipe = all[i];
            if (recipe.itemId != itemId || IsKnown(recipe))
                continue;

            Learned.Add(recipe.id);
            SaveLearned();
            OnLearned?.Invoke(recipe);
        }
    }

    public static IEnumerable<string> LearnedIds => Learned;

    /// <summary>แทนที่ชุดสูตรที่รู้ทั้งหมด (ใช้ตอนโหลดเซฟ)</summary>
    public static void SetLearned(IEnumerable<string> ids)
    {
        Learned.Clear();
        if (ids != null)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                    Learned.Add(id);
            }
        }

        SaveLearned();
    }

    private static void SaveLearned()
    {
        PlayerPrefs.SetString(LearnedKey, string.Join(";", Learned));
        PlayerPrefs.Save();
    }

    public static void ResetSave()
    {
        PlayerPrefs.DeleteKey(LearnedKey);
        learned = null;
    }

    // ------------------------------------------------------------ จับคู่สูตร

    /// <summary>หาสูตรที่ลูกศรตรงกันทุกตัว (ต้องรู้สูตรแล้ว) ไม่เจอ = null</summary>
    public static CommandRecipe Match(IReadOnlyList<CommandArrow> input)
    {
        IReadOnlyList<CommandRecipe> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            CommandRecipe recipe = all[i];
            if (recipe == null || recipe.sequence == null || recipe.sequence.Length != input.Count || !IsKnown(recipe))
                continue;

            bool same = true;
            for (int k = 0; k < input.Count && same; k++)
                same = recipe.sequence[k] == input[k];

            if (same)
                return recipe;
        }

        return null;
    }

    public static CommandRecipe Find(string id)
    {
        IReadOnlyList<CommandRecipe> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].id == id)
                return all[i];
        }

        return null;
    }

    // ------------------------------------------------------------ Cooldown ปืน (นับเวลาในด่าน)

    public static float CooldownRemaining(CommandRecipe recipe)
    {
        return recipe != null && readyAt.TryGetValue(recipe.id, out float time) ? Mathf.Max(0f, time - Time.time) : 0f;
    }

    public static void StartCooldown(CommandRecipe recipe, float seconds)
    {
        if (recipe != null && seconds > 0f)
            readyAt[recipe.id] = Time.time + seconds;
    }
}
