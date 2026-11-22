using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// การทำงานของหน้าต่างเซฟ (หน้าตาอยู่ใน SaveSlotsView / Prefab Resources/UI/SaveSlots)
///   ในเกม (Main Menu Mode = ปิด)  : เปิดด้วยสูตร SaveGame — ปุ่ม Save / Load / X ลบ, หยุดเวลาและล็อกผู้เล่น
///   Main Menu (Main Menu Mode = เปิด): ปุ่ม Load Game — กดแถวเพื่อโหลด, X ลบ (Yes / No), X มุมขวาบน = กลับเมนู
/// ใน Scene MainMenu วางตัวนี้ไว้แล้ว (วัตถุ LoadGamePanel) จึงเห็นและปรับตำแหน่งใน Editor ได้
/// เซฟทับ / ลบ ต้องกดยืนยันในกล่อง Yes / No ก่อน
/// </summary>
[RequireComponent(typeof(SaveSlotsView))]
public class SaveLoadUI : MonoBehaviour
{
    [Tooltip("เปิด = ใช้ใน Main Menu (โหลด/ลบได้อย่างเดียว ไม่หยุดเวลา ไม่ล็อกผู้เล่น)")]
    public bool mainMenuMode;

    private static SaveLoadUI inGame;

    private SaveSlotsView view;
    private TopDownPlayerController lockedPlayer;
    private float timeScaleBefore = 1f;
    private System.Action pendingConfirm;
    private bool bound;

    public static bool IsOpen => inGame != null && inGame.gameObject.activeSelf;

    /// <summary>เปิดหน้าต่างเซฟในเกม (จากสูตร SaveGame)</summary>
    public static void Open()
    {
        if (inGame == null)
        {
            SaveSlotsView view = UIPrefabs.Spawn(SaveSlotsView.PrefabName, SaveSlotsView.BuildDefault);
            inGame = view.GetComponent<SaveLoadUI>();
            if (inGame == null)
                inGame = view.gameObject.AddComponent<SaveLoadUI>();
            inGame.mainMenuMode = false;
        }

        inGame.Show();
    }

    private void Awake()
    {
        Bind();
    }

    private void OnDestroy()
    {
        if (inGame == this)
            inGame = null;
    }

    private void Bind()
    {
        if (bound)
            return;

        bound = true;
        view = GetComponent<SaveSlotsView>();

        if (view.closeButton != null)
            view.closeButton.onClick.AddListener(Close);
        if (view.confirmYes != null)
            view.confirmYes.onClick.AddListener(() => { System.Action action = pendingConfirm; HideConfirm(); action?.Invoke(); });
        if (view.confirmNo != null)
            view.confirmNo.onClick.AddListener(HideConfirm);
    }

    private void Update()
    {
        if (!PlayerControls.WasPressed(PlayerControls.Pause, Key.Escape))
            return;

        // Esc: ปิดกล่องยืนยันก่อน แล้วค่อยปิดหน้าต่าง
        if (view.confirmDialog != null && view.confirmDialog.activeSelf)
            HideConfirm();
        else
            Close();
    }

    /// <summary>เปิดหน้าต่าง (ปุ่ม Load Game ใน Main Menu ผูกกับฟังก์ชันนี้ได้)</summary>
    public void Show()
    {
        Bind();
        gameObject.SetActive(true);
        HideConfirm();

        if (view.title != null)
            view.title.text = mainMenuMode ? view.mainMenuTitle : view.inGameTitle;

        if (!mainMenuMode)
        {
            lockedPlayer = FindAnyObjectByType<TopDownPlayerController>();
            if (lockedPlayer != null)
                lockedPlayer.SetMovementLocked(this, true);

            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SfxLibrary.PlayUI("menu_open");
        SetStatus(string.Empty);
        Refresh();
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
            return;

        HideConfirm();
        gameObject.SetActive(false);

        if (!mainMenuMode)
        {
            Time.timeScale = timeScaleBefore;

            if (lockedPlayer != null)
                lockedPlayer.SetMovementLocked(this, false);
            lockedPlayer = null;

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }

        SfxLibrary.PlayUI("menu_close");
    }

    // ------------------------------------------------------------ ช่องเซฟ

    private void Refresh()
    {
        if (view.list == null || view.slotTemplate == null)
            return;

        UIPrefabs.ClearClones(view.list, view.slotTemplate);

        for (int i = 0; i < SaveSystem.SlotCount; i++)
            AddSlot(i);
    }

    private void AddSlot(int slot)
    {
        SaveSystem.Data data = SaveSystem.Peek(slot);
        bool hasData = data != null;

        SaveSlotView row = UIPrefabs.Clone(view.slotTemplate, view.list);
        row.name = "Slot" + (slot + 1);

        if (row.info != null)
        {
            row.info.gameObject.SetActive(hasData);
            if (hasData)
                row.info.text = $"<b>{data.scene}</b>   <size=18><color=#FFFFFF88>{data.savedAt}</color></size>\n" +
                                $"<size=20>Slot {slot + 1}  •  HP {data.health:0}  •  X-Gear {data.xgearCharges}</size>";
        }

        if (row.emptyText != null)
            row.emptyText.gameObject.SetActive(!hasData);

        // ในเกม: ปุ่ม Save / Load / Main Menu: กดแถวเพื่อโหลด
        if (row.saveButton != null)
        {
            row.saveButton.gameObject.SetActive(!mainMenuMode);
            row.saveButton.onClick.AddListener(() => OnSave(slot, hasData));
        }

        if (row.loadButton != null)
        {
            row.loadButton.gameObject.SetActive(!mainMenuMode);
            row.loadButton.interactable = hasData;
            row.loadButton.onClick.AddListener(() => OnLoad(slot));
        }

        if (row.rowButton != null)
        {
            row.rowButton.enabled = mainMenuMode && hasData;
            if (mainMenuMode && hasData)
                row.rowButton.onClick.AddListener(() => OnLoad(slot));
        }

        if (row.deleteButton != null)
        {
            row.deleteButton.gameObject.SetActive(hasData);
            row.deleteButton.onClick.AddListener(() =>
                AskConfirm($"Delete save in slot {slot + 1}?", () =>
                {
                    SaveSystem.Delete(slot);
                    SetStatus($"Slot {slot + 1} deleted");
                    Refresh();
                }));
        }
    }

    private void OnSave(int slot, bool hasData)
    {
        if (hasData)
        {
            AskConfirm($"Overwrite slot {slot + 1}?", () => DoSave(slot));
            return;
        }

        DoSave(slot);
    }

    private void DoSave(int slot)
    {
        if (SaveSystem.Save(slot, out string error))
        {
            SfxLibrary.PlayUI("upgrade_success");
            SetStatus($"Saved to slot {slot + 1}");
        }
        else
        {
            SfxLibrary.PlayUI("ui_error");
            SetStatus(error);
        }

        Refresh();
    }

    private void OnLoad(int slot)
    {
        Close(); // คืน timeScale และปลดล็อกผู้เล่นก่อนเปลี่ยน Scene

        if (!SaveSystem.Load(slot, out string error))
        {
            SfxLibrary.PlayUI("ui_error");
            Show();
            SetStatus(error);
        }
    }

    // ------------------------------------------------------------ กล่องยืนยัน Yes / No

    private void AskConfirm(string message, System.Action onYes)
    {
        if (view.confirmDialog == null)
        {
            onYes();
            return;
        }

        pendingConfirm = onYes;
        if (view.confirmText != null)
            view.confirmText.text = message;
        view.confirmDialog.SetActive(true);
        view.confirmDialog.transform.SetAsLastSibling();
    }

    private void HideConfirm()
    {
        pendingConfirm = null;
        if (view != null && view.confirmDialog != null)
            view.confirmDialog.SetActive(false);
    }

    private void SetStatus(string text)
    {
        if (view.status != null)
            view.status.text = text;
    }
}
