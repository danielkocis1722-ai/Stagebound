using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CarController), typeof(RespawnManager))]
public class PauseMenu : MonoBehaviour
{
    public string mainMenuScene = "MainMenu";
    public static bool IsPaused { get; private set; }
    CarController car;
    RespawnManager respawn;
    RaceHUD hud;
    GameObject canvasRoot, menu;
    AudioSettingsPanel settings;
    Button resumeButton;
    TMP_Text notice;
    bool paused, carWasEnabled, hudWasEnabled, hudWasEditing;
    bool cursorWasVisible;
    CursorLockMode previousCursorLock;
    float previousTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { ResetPauseState(); }
    public static void ResetPauseState() { IsPaused = false; Time.timeScale = 1f; }

    void Awake()
    {
        car = GetComponent<CarController>();
        respawn = GetComponent<RespawnManager>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (pad != null && pad.startButton.wasPressedThisFrame))
        {
            if (paused) Resume(); else Pause();
        }
    }

    public void Pause()
    {
        if (paused) return;
        if (canvasRoot == null) BuildMenu();
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        cursorWasVisible = Cursor.visible;
        carWasEnabled = car.enabled;
        hud = FindAnyObjectByType<RaceHUD>();
        hudWasEnabled = hud != null && hud.enabled;
        // The HUD owns the cursor while editing. Restore its edit state on resume.
        hudWasEditing = hudWasEnabled && cursorWasVisible && previousCursorLock == CursorLockMode.None;
        paused = IsPaused = true;
        car.enabled = false;
        if (hudWasEnabled) hud.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        canvasRoot.SetActive(true);
        ShowMainPanel();
    }

    public void Resume()
    {
        if (!paused) return;
        paused = IsPaused = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (car != null) car.enabled = carWasEnabled;
        if (hud != null && hudWasEnabled)
        {
            hud.enabled = true;
            hud.SetHUDEditMode(hudWasEditing);
        }
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
    }

    public void RestartStage()
    {
        respawn.RestartStage();
        Resume();
    }

    public void ShowSettings()
    {
        menu.SetActive(false); settings.Show();
    }

    void ShowMainPanel()
    {
        settings.gameObject.SetActive(false); menu.SetActive(true);
        notice.text = "";
        Select(resumeButton);
    }

    public void QuitToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            notice.text = "Main Menu scene is unavailable in the build configuration.";
            return;
        }
        Resume();
        ResetPauseState();
        StageboundAudioSettings.Save();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    void BuildMenu()
    {
        var events = EventSystem.current;
        if (events == null) events = FindAnyObjectByType<EventSystem>();
        if (events == null) events = new GameObject("Pause EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
        if (events.GetComponent<InputSystemUIInputModule>() == null)
        {
            foreach (var module in events.GetComponents<BaseInputModule>()) module.enabled = false;
            events.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        canvasRoot = new GameObject("Stagebound Pause Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var shade = Panel(canvasRoot.transform, "Pause shade", Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = new Color(0, 0, 0, .5f);
        menu = Panel(shade, "Pause", new Vector2(480, 430)).gameObject;
        Label(menu.transform, "PAUSED", 30, 160);
        resumeButton = MenuButton(menu.transform, "Resume", 90, Resume);
        MenuButton(menu.transform, "Restart Stage", 25, RestartStage);
        MenuButton(menu.transform, "Settings", -40, ShowSettings);
        MenuButton(menu.transform, "Main Menu", -105, QuitToMainMenu);
        notice = Label(menu.transform, "", 16, -178);
        settings = AudioSettingsPanel.Create(shade, ShowMainPanel);
        canvasRoot.SetActive(false);
    }

    static RectTransform Panel(Transform parent, string name, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        obj.GetComponent<Image>().color = new Color32(10, 18, 29, 245);
        return rect;
    }

    static TMP_Text Label(Transform parent, string value, float fontSize, float y)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
        text.rectTransform.anchoredPosition = new Vector2(0, y);
        text.rectTransform.sizeDelta = new Vector2(440, 50);
        text.text = value; text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white; text.raycastTarget = false;
        return text;
    }

    static Button MenuButton(Transform parent, string title, float y, UnityEngine.Events.UnityAction action)
    {
        var rect = Panel(parent, title, new Vector2(390, 52));
        rect.anchoredPosition = new Vector2(0, y);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.targetGraphic.color = Color.white;
        var colors = button.colors;
        colors.normalColor = new Color32(20, 35, 50, 255);
        colors.highlightedColor = colors.selectedColor = new Color32(54, 126, 107, 255);
        colors.pressedColor = new Color32(35, 90, 75, 255);
        button.colors = colors;
        Label(rect, title, 22, 0);
        button.onClick.AddListener(action);
        return button;
    }

    static void Select(Button button)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    void OnDisable() { Resume(); }
    void OnDestroy() { if (canvasRoot != null) Destroy(canvasRoot); }
}
