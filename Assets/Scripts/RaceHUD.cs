using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Installed by the existing Speedometer component; no manual Canvas wiring needed.
public class RaceHUD : MonoBehaviour
{
    public CarController car;
    [Range(0f, 1f)] public float timingBackgroundOpacity = .4f;
    [Range(0f, 1f)] public float vehicleBackgroundOpacity = .4f;
    [Min(0f)] public float resultDisplayDuration = 5f;
    [SerializeField] RaceTimer race;
    TMP_Text speed, rpm, gear, checkpoints, clock, delta, best, status, result;
    Image revFill;
    GameObject results;
    GameObject hudRoot;
    RectTransform safeArea;
    DraggableHUDPanel timingPanel, vehiclePanel;
    TMP_Text editLabel;
    bool hudEditMode;
    Rect lastSafeArea;
    Vector2Int lastScreenSize;
    const float PanelScale = .65f;
    static readonly Color Mint = new Color32(91, 255, 191, 255);
    static readonly Color Red = new Color32(255, 102, 111, 255);
    static readonly Color Muted = new Color32(157, 174, 193, 255);

    void Start()
    {
        EnsureHUD();
    }

    void EnsureHUD()
    {
        if (hudRoot != null) return;
        if (car == null) car = FindAnyObjectByType<CarController>();
        if (race == null) race = GetComponent<RaceTimer>();
        if (race == null) race = FindAnyObjectByType<RaceTimer>();
        if (race == null) race = gameObject.AddComponent<RaceTimer>();
        var root = new GameObject("Stagebound Race HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // Speedometer lives on an existing Canvas. A nested Canvas defaults to
        // a 100x100 rect, which clamps both panels in place. Use a full-screen root.
        hudRoot = root;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 20;
        EnsureEventSystem();
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(root.transform, false);
        safe.anchorMin = new Vector2(Screen.safeArea.xMin / Screen.width, Screen.safeArea.yMin / Screen.height);
        safe.anchorMax = new Vector2(Screen.safeArea.xMax / Screen.width, Screen.safeArea.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        safeArea = safe;

        var top = Panel(safe, "Timing", new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(530, 182));
        Text(top, "STAGEBOUND  /  TIME ATTACK", 20, new Vector2(24, -16), Muted);
        clock = Text(top, "00:00.000", 56, new Vector2(24, -43), Color.white);
        delta = Text(top, "SPLIT  --", 23, new Vector2(310, -65), Muted);
        best = Text(top, "BEST  --:--.---", 20, new Vector2(24, -119), Muted);
        status = Text(top, "READY", 18, new Vector2(24, -150), Mint);
        top.localScale = Vector3.one * PanelScale;

        var right = Panel(safe, "Dashboard", new Vector2(0, 1), new Vector2(20, -20), new Vector2(340, 268));
        Text(right, "VEHICLE  /  LIVE", 18, new Vector2(24, -18), Muted);
        speed = Text(right, "000", 94, new Vector2(18, -40), Color.white);
        Text(right, "KM/H", 22, new Vector2(235, -104), Muted);
        gear = Text(right, "1", 32, new Vector2(250, -50), Mint);
        rpm = Text(right, "0 RPM", 24, new Vector2(24, -155), Color.white);
        var bar = Panel(right, "RPM track", new Vector2(0, 1), new Vector2(24, -197), new Vector2(292, 8));
        bar.pivot = new Vector2(0, 1);
        revFill = bar.GetComponent<Image>(); revFill.color = Mint;
        Text(right, "CHECKPOINTS", 18, new Vector2(24, -227), Muted);
        checkpoints = Text(right, "0 / 0", 30, new Vector2(230, -220), Color.white);
        right.localScale = Vector3.one * PanelScale;

        editLabel = Text(safe, "HUD EDIT MODE • DRAG PANELS • F2 DONE • SHIFT+F2 RESET • T BACKGROUND", 13, Vector2.zero, Mint);
        editLabel.rectTransform.anchorMin = editLabel.rectTransform.anchorMax = Vector2.zero;
        editLabel.rectTransform.pivot = Vector2.zero;
        editLabel.rectTransform.anchoredPosition = new Vector2(12, 10);
        editLabel.rectTransform.sizeDelta = new Vector2(720, 22);
        editLabel.alignment = TextAlignmentOptions.BottomLeft;

        var finish = Panel(safe, "Finish results", new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 320));
        results = finish.gameObject;
        Text(finish, "STAGE COMPLETE", 22, new Vector2(32, -25), Mint);
        result = Text(finish, "", 30, new Vector2(32, -75), Color.white);
        result.rectTransform.sizeDelta = new Vector2(560, 230);
        results.SetActive(false);

        Canvas.ForceUpdateCanvases();
        timingPanel = top.gameObject.AddComponent<DraggableHUDPanel>();
        timingPanel.backgroundOpacity = timingBackgroundOpacity;
        timingPanel.Initialize("Stagebound.HUD.Timing.v1");
        vehiclePanel = right.gameObject.AddComponent<DraggableHUDPanel>();
        vehiclePanel.backgroundOpacity = vehicleBackgroundOpacity;
        vehiclePanel.Initialize("Stagebound.HUD.Vehicle.v1");
        SetHUDEditMode(false);
    }
    void LateUpdate()
    {
        UpdateHUDEditor();
        if (race == null) return;
        if (car != null)
        {
            speed.text = Mathf.RoundToInt(car.SpeedKmh).ToString("000");
            gear.text = car.CurrentGearText;
            rpm.text = $"{car.EngineRPM:0} RPM";
            float rev = Mathf.Clamp01(car.EngineRPM / Mathf.Max(1, car.redlineRPM));
            revFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 292 * rev);
            revFill.color = rev > .88f ? Red : Mint;
        }
        clock.text = race.GetFormattedTime();
        best.text = "BEST  " + (race.BestTime > 0 ? RaceTimer.FormatTime(race.BestTime) : "--:--.---");
        checkpoints.text = $"{race.PassedCheckpoints} / {race.TotalCheckpoints}";
        delta.text = "SPLIT  " + (race.Delta.HasValue ? DeltaText(race.Delta) :
            race.LatestSplit.HasValue ? RaceTimer.FormatTime(race.LatestSplit.Value) : "--");
        delta.color = !race.Delta.HasValue ? Muted : race.Delta.Value <= 0 ? Mint : Red;
        status.text = !race.TrackReady ? "SETUP REQUIRED: START + CHECKPOINTS" : race.raceFinished ? "FINISHED" : race.raceRunning ? "STAGE IN PROGRESS" : "CROSS START TO BEGIN";
        UpdateResults(Time.unscaledTime);
    }

    void UpdateResults(float now)
    {
        var completed = race.LastResult;
        bool visible = completed != null && now - completed.CompletedAt < Mathf.Max(0f, resultDisplayDuration);
        results.SetActive(visible);
        if (visible)
            result.text = $"<size=20>YOUR TIME</size>\n<size=52>{RaceTimer.FormatTime(completed.Time)}</size>\n<size=23>BEST   {RaceTimer.FormatTime(completed.Best)}\nGAP TO PREVIOUS BEST   {DeltaText(completed.Gap)}</size>\n<size=20><color=#5BFFBF>{(completed.IsNewBest ? "NEW PERSONAL BEST" : "NEXT RUN IN PROGRESS")}</color></size>";
    }

    void UpdateHUDEditor()
    {
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        if (lastSafeArea != Screen.safeArea || lastScreenSize != screenSize)
        {
            lastSafeArea = Screen.safeArea;
            lastScreenSize = screenSize;
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            Canvas.ForceUpdateCanvases();
        }
        timingPanel.RefreshLayout();
        vehiclePanel.RefreshLayout();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
        {
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                ResetHUDPositions();
            else SetHUDEditMode(!hudEditMode);
        }

        if (hudEditMode && keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            bool transparent = !timingPanel.BackgroundTransparent;
            timingPanel.SetBackgroundTransparent(transparent);
            vehiclePanel.SetBackgroundTransparent(transparent);
            PlayerPrefs.Save();
        }
    }

    static void EnsureEventSystem()
    {
        var events = EventSystem.current;
        if (events == null) events = FindAnyObjectByType<EventSystem>();
        if (events == null)
            events = new GameObject("HUD EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        if (events.GetComponent<InputSystemUIInputModule>() == null)
        {
            foreach (var module in events.GetComponents<BaseInputModule>()) module.enabled = false;
            events.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    public void SetHUDEditMode(bool enabled)
    {
        hudEditMode = enabled;
        if (editLabel != null) editLabel.gameObject.SetActive(enabled);
        if (timingPanel != null) timingPanel.SetEditMode(enabled);
        if (vehiclePanel != null) vehiclePanel.SetEditMode(enabled);
        Cursor.lockState = enabled ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = enabled;
    }

    public void ResetHUDPositions()
    {
        if (timingPanel != null) timingPanel.ResetPosition();
        if (vehiclePanel != null) vehiclePanel.ResetPosition();
        PlayerPrefs.Save();
    }

    void OnDisable()
    {
        if (hudEditMode) SetHUDEditMode(false);
        if (hudRoot != null) hudRoot.SetActive(false);
    }

    void OnEnable()
    {
        // A runtime canvas may have been destroyed between Play sessions or
        // when the owner was disabled. Rebuild once instead of keeping stale UI references.
        if (Application.isPlaying) EnsureHUD();
        if (hudRoot != null) hudRoot.SetActive(true);
    }

    void OnDestroy()
    {
        if (hudRoot != null) Destroy(hudRoot);
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused && editLabel != null) SetHUDEditMode(hudEditMode);
    }
    static string DeltaText(float? value) => value.HasValue ? (value.Value <= 0 ? "-" : "+") + Mathf.Abs(value.Value).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s" : "--";
    static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        obj.GetComponent<Image>().color = new Color32(10, 18, 29, 235);
        obj.GetComponent<Image>().raycastTarget = false;
        return rect;
    }
    static TMP_Text Text(Transform parent, string content, float size, Vector2 position, Color color)
    {
        var obj = new GameObject(content, typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
        text.rectTransform.pivot = new Vector2(0, 1);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(530 - position.x, 110);
        text.text = content; text.fontSize = size; text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        // Keep glyphs readable against the road even when the panel is invisible.
        text.outlineColor = new Color32(5, 10, 16, 230);
        text.outlineWidth = .12f;
        return text;
    }
}
