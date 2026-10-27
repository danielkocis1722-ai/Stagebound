using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuSelectionHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    TMP_Text label; string title; bool hovered, selected;
    public void Initialize(TMP_Text value, string text) { label = value; title = text; Refresh(); }
    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(); }
    public void OnSelect(BaseEventData eventData) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { selected = false; Refresh(); }
    void Refresh()
    {
        if (label == null) return;
        bool active = hovered || selected;
        label.text = (active ? "> " : "  ") + title;
        label.color = active ? StageboundMenuUI.Mint : Color.white;
    }
}
