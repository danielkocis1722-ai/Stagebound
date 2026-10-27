using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class StageboundMenuUI
{
    public static readonly Color Mint = new Color32(91, 255, 191, 255);
    public static readonly Color Muted = new Color32(157, 174, 193, 255);
    public static Canvas Canvas(Transform parent, string title)
    {
        var root = new GameObject(title, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        EnsureEventSystem(); return canvas;
    }
    public static void EnsureEventSystem()
    {
        var events = EventSystem.current;
        if (events == null) events = Object.FindAnyObjectByType<EventSystem>();
        if (events == null) events = new GameObject("Stagebound Menu EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        var input = events.GetComponent<InputSystemUIInputModule>();
        if (input == null)
        {
            foreach (var old in events.GetComponents<BaseInputModule>()) old.enabled = false;
            input = events.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        if (input.actionsAsset == null) input.AssignDefaultActions();
    }
    public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)obj.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }
    public static TMP_Text Text(Transform parent, string value, float size, Vector2 position, Vector2 bounds, Color color)
    {
        var obj = new GameObject(value, typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = obj.GetComponent<TextMeshProUGUI>(); text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = bounds;
        text.text = value; text.fontSize = size; text.color = color; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    public static Button Button(Transform parent, string title, Vector2 position, Vector2 size, UnityAction action)
    {
        var rect = Panel(parent, title, new Vector2(0, 1), position, size, Color.white);
        var image = rect.GetComponent<Image>(); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color32(13, 24, 35, 150);
        colors.highlightedColor = colors.selectedColor = new Color32(30, 58, 65, 230);
        colors.pressedColor = new Color32(40, 78, 75, 245); colors.fadeDuration = .12f; button.colors = colors;
        var label = Text(rect, "  " + title, 27, new Vector2(14, -8), new Vector2(size.x - 28, size.y - 8), Color.white);
        var highlight = rect.gameObject.AddComponent<MenuSelectionHighlight>(); highlight.Initialize(label, title);
        button.onClick.AddListener(action); return button;
    }
    public static void Select(Selectable target)
    { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject); }
}
