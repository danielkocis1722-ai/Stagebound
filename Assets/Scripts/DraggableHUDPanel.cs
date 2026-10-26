using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Reusable for unrotated panels on a Screen Space - Overlay canvas.
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class DraggableHUDPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Range(0f, 1f)] public float backgroundOpacity = .4f;
    RectTransform panel, container;
    Image background;
    Color normalColor;
    string preferenceKey;
    Vector2 defaultPosition, pointerOffset, normalizedPosition;
    Rect previousBounds;
    bool editMode, initialized;
    bool hovered, backgroundTransparent;
    int dragPointerId;
    GameObject hoverBorder;
    public bool IsDragging { get; private set; }

    public void Initialize(string key)
    {
        panel = (RectTransform)transform;
        container = (RectTransform)panel.parent;
        background = GetComponent<Image>();
        normalColor = background.color;
        // An alpha-zero Image must still receive raycasts while editing.
        background.canvasRenderer.cullTransparentMesh = false;
        CreateHoverBorder();
        preferenceKey = key;
        defaultPosition = panel.anchoredPosition;
        previousBounds = container.rect;
        initialized = true;
        backgroundTransparent = PlayerPrefs.GetInt(key + ".Transparent", 0) != 0;
        if (PlayerPrefs.HasKey(key + ".X") && PlayerPrefs.HasKey(key + ".Y"))
        {
            normalizedPosition = new Vector2(PlayerPrefs.GetFloat(key + ".X"), PlayerPrefs.GetFloat(key + ".Y"));
            bool invalid = !Finite(normalizedPosition.x) || !Finite(normalizedPosition.y) ||
                normalizedPosition.x < 0 || normalizedPosition.x > 1 || normalizedPosition.y < 0 || normalizedPosition.y > 1;
            if (!invalid)
            {
                RestoreNormalizedPosition();
                invalid = !FitsInsideContainer();
            }
            if (invalid)
            {
                panel.anchoredPosition = defaultPosition;
                ClampPosition();
                // Repair just the invalid coordinates, retaining background preferences.
                PlayerPrefs.SetFloat(key + ".X", normalizedPosition.x);
                PlayerPrefs.SetFloat(key + ".Y", normalizedPosition.y);
                PlayerPrefs.Save();
            }
        }
        ClampPosition();
        background.raycastTarget = editMode;
        UpdateAppearance();
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    bool FitsInsideContainer()
    {
        Rect bounds = container.rect;
        Vector2 size = Vector2.Scale(panel.rect.size, panel.localScale);
        Vector2 position = panel.localPosition;
        return bounds.width > 0 && bounds.height > 0 &&
            position.x - size.x * panel.pivot.x >= bounds.xMin - .01f &&
            position.x + size.x * (1 - panel.pivot.x) <= bounds.xMax + .01f &&
            position.y - size.y * panel.pivot.y >= bounds.yMin - .01f &&
            position.y + size.y * (1 - panel.pivot.y) <= bounds.yMax + .01f;
    }

    public void SetEditMode(bool enabled)
    {
        StopDragging();
        editMode = enabled;
        hovered = false;
        if (background != null) background.raycastTarget = enabled;
        UpdateAppearance();
    }

    public void SetBackgroundTransparent(bool transparent)
    {
        backgroundTransparent = transparent;
        if (initialized)
            PlayerPrefs.SetInt(preferenceKey + ".Transparent", transparent ? 1 : 0);
        UpdateAppearance();
    }

    public bool BackgroundTransparent => backgroundTransparent;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!initialized || !editMode || eventData.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, null, out var localPointer)) return;
        IsDragging = true;
        dragPointerId = eventData.pointerId;
        pointerOffset = (Vector2)panel.localPosition - localPointer;
        UpdateAppearance();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;
    public void OnBeginDrag(PointerEventData eventData) => OnDrag(eventData);

    public void OnDrag(PointerEventData eventData)
    {
        if (!editMode || !IsDragging || eventData.pointerId != dragPointerId ||
            eventData.button != PointerEventData.InputButton.Left) return;
        // Screen Space - Overlay uses a null camera. Convert into the parent,
        // rather than mixing screen pixels with scaled anchored coordinates.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, null, out var localPointer))
        {
            SetLocalPosition(localPointer + pointerOffset);
            ClampPosition();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.pointerId != dragPointerId) return;
        OnDrag(eventData);
        StopDragging();
        UpdateAppearance();
    }

    public void OnEndDrag(PointerEventData eventData) => OnPointerUp(eventData);

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        UpdateAppearance();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        UpdateAppearance();
    }

    public void RefreshLayout()
    {
        if (!initialized) return;
        if (previousBounds != container.rect)
        {
            StopDragging();
            previousBounds = container.rect;
            RestoreNormalizedPosition();
        }
        ClampPosition();
    }

    public void ResetPosition()
    {
        if (!initialized) return;
        // Discard an in-progress drag before restoring defaults.
        IsDragging = false;
        panel.anchoredPosition = defaultPosition;
        ClampPosition();
        PlayerPrefs.DeleteKey(preferenceKey + ".X");
        PlayerPrefs.DeleteKey(preferenceKey + ".Y");
        hovered = false;
        UpdateAppearance();
    }

    void ClampPosition()
    {
        Rect bounds = container.rect;
        Vector2 size = Vector2.Scale(panel.rect.size, panel.localScale);
        float minX = bounds.xMin + size.x * panel.pivot.x;
        float maxX = bounds.xMax - size.x * (1 - panel.pivot.x);
        float minY = bounds.yMin + size.y * panel.pivot.y;
        float maxY = bounds.yMax - size.y * (1 - panel.pivot.y);
        Vector2 position = panel.localPosition;
        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (minX + maxX) * .5f;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : (minY + maxY) * .5f;
        SetLocalPosition(position);
        normalizedPosition = new Vector2(
            Mathf.InverseLerp(bounds.xMin, bounds.xMax, position.x),
            Mathf.InverseLerp(bounds.yMin, bounds.yMax, position.y));
    }

    void RestoreNormalizedPosition()
    {
        Rect bounds = container.rect;
        SetLocalPosition(new Vector2(
            Mathf.Lerp(bounds.xMin, bounds.xMax, normalizedPosition.x),
            Mathf.Lerp(bounds.yMin, bounds.yMax, normalizedPosition.y)));
    }

    void SetLocalPosition(Vector2 position) =>
        panel.localPosition = new Vector3(position.x, position.y, panel.localPosition.z);

    void UpdateAppearance()
    {
        if (background == null) return;
        bool highlighted = editMode && (hovered || IsDragging);
        float opacity = backgroundTransparent ? 0f : Mathf.Clamp01(backgroundOpacity);
        background.color = highlighted
            ? new Color(normalColor.r + .035f, normalColor.g + .055f, normalColor.b + .05f, opacity)
            : new Color(normalColor.r, normalColor.g, normalColor.b, opacity);
        if (hoverBorder != null) hoverBorder.SetActive(highlighted);
    }

    void CreateHoverBorder()
    {
        hoverBorder = new GameObject("Edit hover outline", typeof(RectTransform));
        var rect = (RectTransform)hoverBorder.transform;
        rect.SetParent(panel, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        AddBorderEdge(rect, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0, 1.5f));
        AddBorderEdge(rect, "Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0, 1.5f));
        AddBorderEdge(rect, "Left", Vector2.zero, new Vector2(0, 1), new Vector2(1.5f, 0));
        AddBorderEdge(rect, "Right", new Vector2(1, 0), Vector2.one, new Vector2(1.5f, 0));
        hoverBorder.SetActive(false);
    }

    static void AddBorderEdge(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 thickness)
    {
        var edge = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)edge.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = thickness;
        var image = edge.GetComponent<Image>();
        image.color = new Color32(91, 255, 191, 140);
        image.raycastTarget = false;
    }

    void StopDragging()
    {
        if (!IsDragging) return;
        IsDragging = false;
        PlayerPrefs.SetFloat(preferenceKey + ".X", normalizedPosition.x);
        PlayerPrefs.SetFloat(preferenceKey + ".Y", normalizedPosition.y);
        PlayerPrefs.Save();
    }

    void OnDisable()
    {
        StopDragging();
        hovered = false;
        UpdateAppearance();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) { StopDragging(); hovered = false; UpdateAppearance(); }
    }

    void OnValidate() => UpdateAppearance();
}
