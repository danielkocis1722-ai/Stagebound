using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RaceHUD : MonoBehaviour
{
    public CarController car;
    [Range(0f, 1f)]
    public float timingBackgroundOpacity = .4f;
    [Range(0f, 1f)]
    public float vehicleBackgroundOpacity = .4f;
    [Min(0f)]
    public float resultDisplayDuration = 5f;
    [SerializeField]
    private RaceTimer race;
    private TMP_Text checkpoints, clock, delta, best, status, result;
    private InstrumentCluster cluster;
    private CarController boundCar;
    private VehicleRigLoader rigLoader;
    private GameObject results;
    private GameObject hudRoot;
    private RectTransform safeArea;
    private DraggableHUDPanel timingPanel, vehiclePanel;
    private DriftHUD driftHUD;
    private TMP_Text editLabel;
    private bool hudEditMode;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private const float PanelScale = .65f;
    private static readonly Color Mint = new Color32(91, 255, 191, 255);
    private static readonly Color Red = new Color32(255, 102, 111, 255);
    private static readonly Color Muted = new Color32(157, 174, 193, 255);

    private void EnsureHUD()
    {
        if (hudRoot != null)
            return;
        if (car == null)
            car = FindAnyObjectByType<CarController>();
        if (race == null)
            race = GetComponent<RaceTimer>();
        if (race == null)
            race = FindAnyObjectByType<RaceTimer>();
        if (race == null)
            race = gameObject.AddComponent<RaceTimer>();
        var root = new GameObject("Stagebound Race HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // The owner lives on an existing Canvas. A nested Canvas defaults to
        // a 100x100 rect, which clamps both panels in place. Use a full-screen root.
        hudRoot = root;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 20;
        if (Application.isPlaying) StageboundMenuUI.EnsureEventSystem(false);
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
        var top = Panel(safe, "Timing", new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(600, 182));
        Text(top, "STAGEBOUND  /  TIME ATTACK", 20, new Vector2(24, -16), Muted);
        clock = Text(top, "00:00.000", 56, new Vector2(24, -43), Color.white);
        delta = Text(top, "SPLIT  --", 23, new Vector2(350, -65), Muted);
        best = Text(top, "BEST  --:--.---", 20, new Vector2(24, -119), Muted);
        status = Text(top, "READY", 18, new Vector2(24, -150), Mint);
        top.localScale = Vector3.one * PanelScale;
        var right = Panel(safe, "Instrument cluster", new Vector2(1, 0), new Vector2(-28, 28), new Vector2(680, 310));
        right.GetComponent<Image>().color = new Color32(7, 14, 22, 0);
        cluster = right.gameObject.AddComponent<InstrumentCluster>();
        BindCluster();
        checkpoints = InstrumentCluster.Label(top, "CHECKPOINT  0 / 0", 17, new Vector2(165, -39), new Vector2(240, 24), Mint);
        if (race.driftScore != null)
        {
            driftHUD = root.AddComponent<DriftHUD>();
            driftHUD.Initialize(safe, race, race.driftScore);
        }

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
        if (Application.isPlaying) timingPanel.Initialize("Stagebound.HUD.Timing.v1");
        vehiclePanel = right.gameObject.AddComponent<DraggableHUDPanel>();
        vehiclePanel.backgroundOpacity = 0f;
        if (Application.isPlaying) vehiclePanel.Initialize("Stagebound.HUD.Cluster.v2");
        if (Application.isPlaying) SetHUDEditMode(false);
        else editLabel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (hudRoot == null) return;
        UpdateHUDEditor();
        if (race == null)
            return;
        if (boundCar != car) BindCluster();
        if (car != null)
        {
            var definition = rigLoader != null ? rigLoader.CurrentDefinition : null;
            cluster.Configure(definition != null && definition.config != null ? definition.config.hudDisplay : null,
                car.redlineRPM, definition != null ? definition.displayName : "STAGEBOUND");
            cluster.Present(car.SpeedKmh, car.EngineRPM, car.CurrentGearText, car.redlineRPM, Time.unscaledDeltaTime);
        }
        clock.text = RaceTimer.FormatTime(race.elapsedTime);
        best.text = "BEST  " + (race.HasBest ? RaceTimer.FormatTime(race.BestTime) : "--:--.---");
        checkpoints.text = $"CHECKPOINT  {race.PassedCheckpoints} / {race.TotalCheckpoints}";
        delta.text = "SPLIT  " + (race.Delta.HasValue ? DeltaText(race.Delta) : race.LatestSplit.HasValue ? RaceTimer.FormatTime(race.LatestSplit.Value) : "--");
        delta.color = !race.Delta.HasValue ? Muted : race.Delta.Value <= 0 ? Mint : Red;
        status.text = !race.TrackReady ? "SETUP REQUIRED: START + CHECKPOINTS" : race.raceRunning ? "STAGE IN PROGRESS" : race.LastResult != null ? "STAGE COMPLETE / RESTART TO RUN AGAIN" : "CROSS START TO BEGIN";
        UpdateResults(Time.unscaledTime);
    }

    private void BindCluster()
    {
        boundCar = car;
        rigLoader = car != null ? car.GetComponent<VehicleRigLoader>() : null;
        var definition = rigLoader != null ? rigLoader.CurrentDefinition : null;
        cluster.Configure(definition != null && definition.config != null ? definition.config.hudDisplay : null,
            car != null ? car.redlineRPM : 7500f, definition != null ? definition.displayName : "STAGEBOUND");
    }

    private void UpdateResults(float now)
    {
        var completed = race.LastResult;
        bool visible = completed != null && now - completed.CompletedAt < Mathf.Max(0f, resultDisplayDuration);
        results.SetActive(visible);
        if (visible)
            result.text = $"<size=23>RAW TIME    {RaceTimer.FormatTime(completed.RawTime)}\nDRIFT SCORE    {completed.DriftScore:N0}\nSTYLE CREDIT    -{completed.StyleCredit:00.000} s</size>\n<size=34>FINAL TIME    {RaceTimer.FormatTime(completed.FinalAdjustedTime)}</size>\n<size=23>BEST    {RaceTimer.FormatTime(completed.Best)}</size>\n<size=20><color=#5BFFBF>{(completed.IsNewBest ? "NEW BEST" : race.raceRunning ? "NEXT RUN IN PROGRESS" : "STAGE COMPLETE")}</color></size>";
    }

    private void UpdateHUDEditor()
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
        if (driftHUD != null)
            driftHUD.Panel.RefreshLayout();
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
        {
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                ResetHUDPositions();
            else
                SetHUDEditMode(!hudEditMode);
        }

        if (hudEditMode && keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            bool transparent = !timingPanel.BackgroundTransparent;
            timingPanel.SetBackgroundTransparent(transparent);
            vehiclePanel.SetBackgroundTransparent(transparent);
            if (driftHUD != null)
                driftHUD.Panel.SetBackgroundTransparent(transparent);
            PlayerPrefs.Save();
        }
    }

    public void SetHUDEditMode(bool enabled)
    {
        hudEditMode = enabled;
        if (editLabel != null)
            editLabel.gameObject.SetActive(enabled);
        if (timingPanel != null)
            timingPanel.SetEditMode(enabled);
        if (vehiclePanel != null)
            vehiclePanel.SetEditMode(enabled);
        if (driftHUD != null)
            driftHUD.Panel.SetEditMode(enabled);
        Cursor.lockState = enabled ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = enabled;
    }

    public void ResetHUDPositions()
    {
        if (timingPanel != null)
            timingPanel.ResetPosition();
        if (vehiclePanel != null)
            vehiclePanel.ResetPosition();
        if (driftHUD != null)
            driftHUD.Panel.ResetPosition();
        PlayerPrefs.Save();
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (hudEditMode)
            SetHUDEditMode(false);
        if (hudRoot != null)
            hudRoot.SetActive(false);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        // A runtime canvas may have been destroyed between Play sessions or
        // when the owner was disabled. Rebuild once instead of keeping stale UI references.
        if (Application.isPlaying)
            EnsureHUD();
        if (hudRoot != null)
            hudRoot.SetActive(true);
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (car == null) car = FindAnyObjectByType<CarController>();
        if (cluster != null) BindCluster();
    }

    private void OnDestroy()
    {
        if (hudRoot != null)
        {
            if (Application.isPlaying) Destroy(hudRoot);
            else DestroyImmediate(hudRoot);
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused && editLabel != null)
            SetHUDEditMode(hudEditMode);
    }

    private static string DeltaText(float? value) => value.HasValue ? (value.Value <= 0 ? "-" : "+") + Mathf.Abs(value.Value).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s" : "--";

    private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size) =>
        StageboundMenuUI.Panel(parent, name, anchor, position, size, new Color32(10, 18, 29, 235));

    private static TMP_Text Text(Transform parent, string content, float size, Vector2 position, Color color)
    {
        var text = StageboundMenuUI.Text(parent, content, size, position, new Vector2(530 - position.x, 110), color);
        // Keep glyphs readable against the road even when the panel is invisible.
        text.outlineColor = new Color32(5, 10, 16, 230);
        text.outlineWidth = .12f;
        return text;
    }
}
