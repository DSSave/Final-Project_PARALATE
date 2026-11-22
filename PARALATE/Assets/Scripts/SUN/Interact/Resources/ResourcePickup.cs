using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResourcePickup : MonoBehaviour, IInteractable
{
    public ResourceData resourceData;
    public int amount = 1;

    public string PromptText =>
        resourceData != null
            ? string.Format(resourceData.promptFormat, resourceData.resourceName, amount)
            : "[ E ] to collect";

    public void Interact(GameObject interactor)
    {
        if (resourceData == null || RunInventory.Instance == null)
            return;

        RunInventory.Instance.Add(resourceData, amount);
        // เสียงเก็บของหลัก (เสียงเดียวกันทั้งเกม) + เสียงแหลมเล็กๆ เดิมของ Scrap / Metal ซ้อนกัน
        SfxLibrary.PlayPickup();
        AudioManager.PlayPickup(resourceData.pickupSound, transform.position);
        HudToast.Show("+ " + resourceData.resourceName + " x" + amount);

        Destroy(gameObject);
    }
}
