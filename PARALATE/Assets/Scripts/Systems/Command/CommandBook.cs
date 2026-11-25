using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวกลางของสูตร CommandLine: อ่านสูตรจาก CommandBookData, จำว่าผู้เล่นรู้สูตรไหนแล้ว (PlayerPrefs)
/// สูตรของไอเทมจะ "รู้" เมื่อเก็บไอเทมนั้นครั้งแรก (Learn) → ขึ้นในหน้า Esc > Logs > Command
/// </summary>
public static class CommandBook
{
    private const string LearnedKey = "Commands_Learned_v1";

    private static CommandBookData data;
    private static List<CommandRecipe> fallback;
    private static HashSet<string> learned;

    /// <summary>รู้สูตรใหม่ (หน้า Logs ใช้รีเฟรช)</summary>
    public static event Action<CommandRecipe> OnLearned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        fallback = null;
        all = null;
        learned = null;
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

    /// <summary>โฟลเดอร์ใน Resources ที่เก็บ ItemData (สูตร Command ของแต่ละไอเทมอยู่ในไฟล์ของไอเทมนั้น)</summary>
    public const string ItemsFolder = "Items";

    private static List<CommandRecipe> all;

    /// <summary>
    /// สูตรทั้งหมด = สูตรใน ItemData ของแต่ละไอเทม (Resources/Items) + สูตรที่ไม่ใช่ไอเทมใน CommandBook.asset (เช่น SaveGame)
    /// Id ซ้ำกัน: ใช้ของ ItemData (ไฟล์ของไอเทมคือที่แก้สูตรหลัก)
    /// </summary>
    public static IReadOnlyList<CommandRecipe> All
    {
        get
        {
            if (all != null)
                return all;

            all = new List<CommandRecipe>();
            HashSet<string> ids = new HashSet<string>();

            // 1) สูตรของไอเทม
            List<CommandRecipe> itemRecipes = new List<CommandRecipe>();
            foreach (ItemData item in Resources.LoadAll<ItemData>(ItemsFolder))
            {
                if (item == null || !item.hasCommand || item.command == null || string.IsNullOrEmpty(item.command.id))
                    continue;

                if (ids.Add(item.command.id))
                    itemRecipes.Add(item.command);
                else
                    Debug.LogWarning($"CommandBook: Id สูตร '{item.command.id}' ซ้ำกันใน ItemData หลายไฟล์ ใช้ไฟล์แรกที่เจอ", item);
            }

            // 2) สูตรที่ไม่ใช่ไอเทม (CommandBook.asset / ค่าเริ่มต้นในโค้ดถ้าไม่มีไฟล์) — มาก่อนในรายการ
            IReadOnlyList<CommandRecipe> book = Data != null && Data.recipes != null && Data.recipes.Count > 0
                ? Data.recipes
                : (fallback ??= CommandBookData.Defaults());

            for (int i = 0; i < book.Count; i++)
            {
                if (book[i] != null && !string.IsNullOrEmpty(book[i].id) && ids.Add(book[i].id))
                    all.Add(book[i]);
            }

            // ของใช้ก่อนอาวุธ แล้วเรียงตามชื่อ (ลำดับในหน้า Esc > Logs > Command)
            itemRecipes.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind) : string.CompareOrdinal(a.displayName, b.displayName));
            all.AddRange(itemRecipes);
            return all;
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
}
