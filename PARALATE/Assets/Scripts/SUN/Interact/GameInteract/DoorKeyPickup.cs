using UnityEngine;

/// <summary>
/// กุญแจที่วางในฉาก กด E เพื่อเก็บ (ระบบเดียวกับ KeycardPickup: ต้องมี Collider และอยู่ใน Layer "Items")
/// ใส่ไฟล์กุญแจ (DoorKeyData) ช่อง Key ให้ตรงกับช่อง Required Key ของประตูที่ต้องการให้เปิด
/// Prefab: Assets/Prefabs/Items/DoorKey.prefab
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorKeyPickup : MonoBehaviour, IInteractable
{
    public DoorKeyData key;

    [Tooltip("{0} = ชื่อกุญแจ")]
    public string promptTemplate = "[ E ] Pick up {0}";

    public AudioClip pickupSound;

    public string PromptText => string.Format(promptTemplate, key != null ? key.displayName : "Key");

    public void Interact(GameObject interactor)
    {
        if (key == null)
        {
            Debug.LogWarning($"{name}: ยังไม่ได้ใส่ไฟล์กุญแจ (DoorKeyData) ในช่อง Key", this);
            return;
        }

        PlayerKeycards.GetOrAdd(interactor).GrantKey(key);
        HudToast.Show("+ " + key.displayName);

        // เสียงเก็บของเสียงเดียวกันทั้งเกม
        if (!SfxLibrary.PlayPickup() && pickupSound != null)
            AudioManager.PlayPickup(pickupSound, transform.position);

        Destroy(gameObject);
    }
}
