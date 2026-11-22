using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>ส่งต่อ event ของเมาส์ (ชี้ / ออก / คลิกขวา) ให้โค้ดที่สร้าง UI และเล่นเสียงปุ่ม</summary>
public class UIPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onEnter;
    public Action onExit;
    public Action onRightClick;
    public bool playSounds;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playSounds)
            SfxLibrary.PlayUI("ui_hover", true);

        onEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            onRightClick?.Invoke();
            return;
        }

        if (playSounds && eventData.button == PointerEventData.InputButton.Left)
            SfxLibrary.PlayUI("ui_click");
    }

    private void OnDisable() => onExit?.Invoke();
}
