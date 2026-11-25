using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NoteReader : MonoBehaviour, IInteractable
{
    [Header("เนื้อหาโน้ต")]
    [TextArea(3, 10)]
    public string noteText = "เนื้อหาโน้ตตรงนี้...";

    private bool isReading = false;
    private GameObject currentReader;

    public string PromptText => isReading ? "[ E ] / [ Esc ] to close" : "[ E ] to read";

    // ปิดโน้ตด้วย Esc (ปุ่ม Pause ใน PlayerInput.inputactions)
    // เมนู Pause ไม่เปิดซ้อนในเฟรมเดียวกัน: PauseMenuUI ไม่รับ Esc ถ้าผู้เล่นถูกล็อกอยู่ในเฟรมก่อน (ตอนอ่านโน้ตผู้เล่นถูกล็อก)
    private void Update()
    {
        if (isReading && PlayerControls.WasPressed(PlayerControls.Pause, UnityEngine.InputSystem.Key.Escape))
            CloseNote();
    }

    private void OnDisable()
    {
        // โน้ตถูกปิด/ลบระหว่างอ่าน: ปลดล็อกผู้เล่นและซ่อนหน้าต่าง ไม่ให้ค้าง
        if (isReading)
            CloseNote();
    }

    public void Interact(GameObject interactor)
    {
        if (isReading)
            CloseNote();
        else
            OpenNote(interactor);
    }

    void OpenNote(GameObject interactor)
    {
        isReading = true;
        currentReader = interactor;

        TopDownPlayerController player = interactor.GetComponent<TopDownPlayerController>();
        if (player != null) player.SetMovementLocked(this, true);

        NoteUIManager.Instance.ShowNote(noteText);
        SfxLibrary.PlayUI("note_open");
    }

    void CloseNote()
    {
        isReading = false;

        if (currentReader != null)
        {
            TopDownPlayerController player = currentReader.GetComponent<TopDownPlayerController>();
            if (player != null) player.SetMovementLocked(this, false);
        }

        currentReader = null;
        if (NoteUIManager.Instance != null)
            NoteUIManager.Instance.HideNote();
        SfxLibrary.PlayUI("note_close");
    }
}