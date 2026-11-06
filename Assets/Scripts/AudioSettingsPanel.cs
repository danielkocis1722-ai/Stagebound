using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared settings shell retained at both existing MainMenu/PauseMenu entry points.
public class AudioSettingsPanel : MonoBehaviour
{
    private Action back;
    private RectTransform body;
    private CanvasGroup bodyGroup, tabsGroup;
    private GameObject modal;
    private TMP_Text prompt, device, activeInput, ffbValue, invertValue, shifterValue, strengthValue;
    private Button replaceButton, returnSelection;
    private Slider strength;
    private LogitechWheelInput wheel;
    private bool ownsWheel;
    private int page = 0, pendingButton = -1;
    private WheelSettings.Binding pendingBinding;
    private readonly List<Selectable> wheelControls = new List<Selectable>();
    private readonly Dictionary<WheelSettings.Binding, TMP_Text> bindingLabels = new Dictionary<WheelSettings.Binding, TMP_Text>();
    private string menuSource = "Keyboard";
    private int openedFrame;
    private bool listening;
    private TMP_Text notice;
    private readonly Button[] tabs = new Button[3];

    public static AudioSettingsPanel Create(Transform parent, Action onBack)
    {
        var root = StageboundMenuUI.Panel(parent, "Settings", new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(1140, 900), new Color32(10, 18, 29, 250));
        var panel = root.gameObject.AddComponent<AudioSettingsPanel>();
        panel.back = onBack;
        StageboundMenuUI.Text(root, "STAGEBOUND  /  SETTINGS", 25, new Vector2(36, -24), new Vector2(700, 46), Color.white);
        StageboundMenuUI.Panel(root, "Header rule", new Vector2(0, 1), new Vector2(36, -72), new Vector2(1068, 1), new Color32(91, 255, 191, 75));
        var header = StageboundMenuUI.Panel(root, "Tabs", new Vector2(0, 1), new Vector2(36, -84), new Vector2(1068, 52), Color.clear);
        panel.tabsGroup = header.gameObject.AddComponent<CanvasGroup>();
        string[] names = { "AUDIO", "WHEEL", "CONTROLS" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            panel.tabs[i] = SmallButton(header, names[i], i * 356, 0, 340, () => panel.BuildPage(index));
        }
        panel.body = StageboundMenuUI.Panel(root, "Settings Content", new Vector2(0, 1), new Vector2(36, -154), new Vector2(1068, 710), Color.clear);
        panel.bodyGroup = panel.body.gameObject.AddComponent<CanvasGroup>();
        root.gameObject.SetActive(false);
        return panel;
    }

    private static TMP_Text Text(Transform parent, string text, float x, float y, float width = 500, float size = 20)
        => StageboundMenuUI.Text(parent, text, size, new Vector2(x, -y), new Vector2(width, 38), StageboundMenuUI.Muted);

    private static Button SmallButton(Transform parent, string text, float x, float y, float width, UnityEngine.Events.UnityAction action)
    {
        var button = StageboundMenuUI.Button(parent, text, new Vector2(x, -y), new Vector2(width, 40), action);
        button.GetComponentInChildren<TMP_Text>().fontSize = 20;
        var colors = button.colors;
        colors.disabledColor = new Color32(20, 30, 40, 120);
        button.colors = colors;
        return button;
    }

    private void BuildPage(int index)
    {
        CancelCapture(false);
        page = index;
        for (int i = body.childCount - 1; i >= 0; i--)
        {
            var child = body.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        bindingLabels.Clear();
        wheelControls.Clear();
        device = activeInput = ffbValue = invertValue = shifterValue = strengthValue = notice = null;
        strength = null;
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].GetComponent<MenuSelectionHighlight>().Initialize(tabs[i].GetComponentInChildren<TMP_Text>(),
                (i == index ? "• " : "") + new[] { "AUDIO", "WHEEL", "CONTROLS" }[i]);
        if (index == 0) BuildAudio();
        else if (index == 1) BuildWheel();
        else BuildControls();
        SmallButton(body, "BACK", 0, 650, 220, Back);
        StageboundMenuUI.Select(tabs[index]);
        Refresh();
    }

    private void BuildControls()
    {
        activeInput = Text(body, "", 0, 0, 1050);
        Text(body, "ACTION", 0, 58, 220);
        Text(body, "KEYBOARD", 245, 58, 230);
        Text(body, "GAMEPAD / DUALSENSE", 485, 58, 310);
        Text(body, "G29", 820, 58, 240);
        string[,] rows = {
            { "Steering", "A / D or ← / →", "Left stick", "Steering wheel" },
            { "Throttle", "W", "RT / R2", "Throttle pedal" },
            { "Brake", "S", "LT / L2", "Brake pedal" },
            { "Clutch", "Left Shift", "West / Square", "Clutch pedal" },
            { "Shift Up", "E", "RB / R1", "" },
            { "Shift Down", "Q", "LB / L1", "" },
            { "Handbrake", "Space", "South / Cross", "" },
            { "Respawn", "R", "North / Triangle", "" },
            { "Pause", "Escape", "Start / Options", "" }
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = 108 + i * 47;
            Text(body, rows[i, 0], 0, y, 220);
            Text(body, rows[i, 1], 245, y, 230);
            Text(body, rows[i, 2], 485, y, 310);
            var value = Text(body, rows[i, 3], 820, y, 240);
            if (i >= 4) bindingLabels.Add((WheelSettings.Binding)(i + 3), value);
        }
        Text(body, "Keyboard and gamepad bindings are fixed. Edit G29 buttons in WHEEL.", 0, 565, 1060, 18);
    }

    private void BuildWheel()
    {
        device = Text(body, "", 0, 0, 1060, 21);
        Text(body, "FORCE FEEDBACK", 0, 115, 500, 23);
        var ffb = SmallButton(body, "FORCE FEEDBACK", 0, 160, 295, () => { WheelSettings.Enabled = !WheelSettings.Enabled; Refresh(); });
        ffbValue = Text(body, "", 340, 164, 150);
        wheelControls.Add(ffb);
        Text(body, "STRENGTH", 0, 220, 300);
        strengthValue = Text(body, "", 395, 220, 100);
        strength = Slider(body, 0, 260, 490, WheelSettings.Strength, v => { WheelSettings.Strength = v; Refresh(); });
        wheelControls.Add(strength);
        wheelControls.Add(SmallButton(body, "INVERT FFB", 0, 310, 295, () => { WheelSettings.Invert = !WheelSettings.Invert; Refresh(); }));
        invertValue = Text(body, "", 340, 314, 150);
        Text(body, "WHEEL BUTTONS", 0, 375, 500, 23);
        for (int i = 7; i < 12; i++) BindingRow((WheelSettings.Binding)i, 0, 420 + (i - 7) * 43);
        Text(body, "H-SHIFTER", 565, 115, 480, 23);
        wheelControls.Add(SmallButton(body, "USE H-SHIFTER", 565, 160, 300, () => { WheelSettings.UseShifter = !WheelSettings.UseShifter; Refresh(); }));
        shifterValue = Text(body, "", 905, 164, 140);
        for (int i = 0; i < 7; i++) BindingRow((WheelSettings.Binding)i, 565, 220 + i * 43);
        Text(body, "No mapped position held = Neutral", 565, 531, 495, 18);
        Text(body, "Unsupported gear = Neutral", 565, 560, 495, 18);
        wheelControls.Add(SmallButton(body, "RESET WHEEL BINDINGS", 565, 600, 490, () =>
        {
            WheelSettings.ResetBindings();
            Refresh();
            notice.text = "Defaults restored; H-shifter mappings cleared.";
        }));
        notice = Text(body, "Button numbers are zero-based DirectInput indices.", 245, 657, 820, 17);
    }

    private void BindingRow(WheelSettings.Binding binding, float x, float y)
    {
        Text(body, WheelSettings.Label(binding), x, y + 4, 170, 19);
        bindingLabels[binding] = Text(body, "", x + 171, y + 4, 160, 18);
        Button button = null;
        button = SmallButton(body, "REBIND", x + 337, y, 155, () => BeginCapture(binding, button));
        wheelControls.Add(button);
    }

    private static Slider Slider(Transform parent, float x, float y, float width, float value, Action<float> setter)
    {
        var rect = StageboundMenuUI.Panel(parent, "Slider", new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, 26), Color.clear);
        rect.GetComponent<Image>().raycastTarget = true;
        var slider = rect.gameObject.AddComponent<Slider>();
        var track = StageboundMenuUI.Panel(rect, "Track", new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, 5), new Color32(42, 58, 73, 255));
        var fill = StageboundMenuUI.Panel(rect, "Fill", new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, 5), StageboundMenuUI.Mint);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(0, 10); fill.offsetMax = new Vector2(0, -10);
        var handle = StageboundMenuUI.Panel(rect, "Handle", new Vector2(.5f, .5f), Vector2.zero, new Vector2(14, 26), Color.white);
        track.GetComponent<Image>().raycastTarget = true;
        handle.GetComponent<Image>().raycastTarget = true;
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0; slider.maxValue = 1;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v => setter(v));
        return slider;
    }

    private void BuildAudio()
    {
        string[] titles = { "MASTER VOLUME", "MUSIC / COMING LATER", "SFX VOLUME" };
        float[] values = { StageboundAudioSettings.Master, StageboundAudioSettings.Music, StageboundAudioSettings.Sfx };
        Action<float>[] setters = { StageboundAudioSettings.SetMaster, StageboundAudioSettings.SetMusic, StageboundAudioSettings.SetSfx };
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            Text(body, titles[i], 0, 45 + i * 160, 800, 23);
            var number = Text(body, values[i].ToString("P0"), 940, 45 + i * 160, 120);
            var slider = Slider(body, 0, 100 + i * 160, 1055, values[i], v => { setters[n](v); number.text = v.ToString("P0"); });
            slider.interactable = i != 1;
        }
    }

    public void Show()
    {
        wheel = FindAnyObjectByType<LogitechWheelInput>();
        if (wheel == null)
        {
            // Menu-only reader uses the same backend; never create a second SDK owner.
            var reader = new GameObject("Settings G29 Input");
            wheel = reader.AddComponent<LogitechWheelInput>();
            ownsWheel = true;
        }
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        BuildPage(page);
    }

    private void Refresh()
    {
        foreach (var row in bindingLabels) row.Value.text = WheelSettings.Display(row.Key);
        bool connected = wheel != null && wheel.WheelConnected;
        bool ready = connected && wheel.HasLiveSample && !wheel.CalibrationInProgress;
        foreach (var control in wheelControls) control.interactable = ready;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (!listening && selected != null && selected.TryGetComponent<Selectable>(out var selection) && !selection.IsInteractable())
            StageboundMenuUI.Select(tabs[page]);
        if (device != null)
        {
            string range = connected && wheel.RangeVerified ? wheel.ReportedRange + "°" : (wheel != null ? wheel.Settings.rotationDegrees.ToString("0") : "900") + "° configured";
            device.text = connected ? "DEVICE  " + wheel.WheelName + "\nCONNECTED     STEERING RANGE  " + range +
                "\nINPUT  " + (wheel.Ready ? "Ready" : wheel.Status) : "NO STEERING WHEEL DETECTED";
            device.rectTransform.sizeDelta = new Vector2(1060, 100);
        }
        if (activeInput != null)
        {
            var car = FindAnyObjectByType<CarController>();
            activeInput.text = "ACTIVE INPUT  " + (car != null ? (car.ActiveDrivingSource == CarController.DrivingSource.Wheel ? "Logitech G29" : car.ActiveDrivingSource.ToString()) : menuSource);
        }
        if (ffbValue != null) ffbValue.text = WheelSettings.Enabled ? "ON" : "OFF";
        if (invertValue != null) invertValue.text = WheelSettings.Invert ? "ON" : "OFF";
        if (shifterValue != null) shifterValue.text = WheelSettings.UseShifter ? "ON" : "OFF";
        if (strengthValue != null) strengthValue.text = WheelSettings.Strength.ToString("P0");
        if (strength != null) strength.SetValueWithoutNotify(WheelSettings.Strength);
    }

    private void BeginCapture(WheelSettings.Binding binding, Button source)
    {
        if (wheel == null || !wheel.BeginBindingCapture()) return;
        pendingBinding = binding;
        pendingButton = -1;
        returnSelection = source;
        listening = true;
        bodyGroup.interactable = tabsGroup.interactable = false;
        bodyGroup.blocksRaycasts = tabsGroup.blocksRaycasts = false;
        var overlay = StageboundMenuUI.Panel(transform, "Rebind Overlay", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1140, 900), new Color32(4, 9, 16, 240));
        overlay.GetComponent<Image>().raycastTarget = true;
        modal = overlay.gameObject;
        var card = StageboundMenuUI.Panel(overlay, "Rebind Dialog", new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(1050, 390), new Color32(10, 18, 29, 255));
        string title = (int)binding < 7 ? "SELECT " + WheelSettings.Label(binding).ToUpperInvariant() + " ON YOUR SHIFTER" : "PRESS A BUTTON FOR " + WheelSettings.Label(binding).ToUpperInvariant();
        Text(card, title, 40, 45, 970, 27);
        prompt = Text(card, "Waiting for input…\nRelease held controls, then select the requested position.\nEscape / Circle / Back cancels.", 40, 115, 970, 22);
        prompt.rectTransform.sizeDelta = new Vector2(970, 140);
        replaceButton = SmallButton(card, "REPLACE", 40, 285, 260, AcceptCapture);
        replaceButton.gameObject.SetActive(false);
        var cancel = SmallButton(card, "CANCEL", 335, 285, 260, () => CancelCapture(true));
        StageboundMenuUI.Select(cancel);
    }

    private void AcceptCapture()
    {
        if (pendingButton < 0 || wheel == null || !wheel.WheelConnected || !wheel.HasLiveSample) { CancelCapture(true); return; }
        WheelSettings.Set(pendingBinding, pendingButton, true);
        CancelCapture(true);
        Refresh();
    }

    private void CancelCapture(bool restoreFocus)
    {
        if (wheel != null) wheel.EndBindingCapture();
        listening = false;
        pendingButton = -1;
        if (modal != null) { modal.SetActive(false); Destroy(modal); }
        modal = null;
        if (bodyGroup != null) { bodyGroup.interactable = true; bodyGroup.blocksRaycasts = true; }
        if (tabsGroup != null) { tabsGroup.interactable = true; tabsGroup.blocksRaycasts = true; }
        if (restoreFocus && returnSelection != null) StageboundMenuUI.Select(returnSelection);
    }

    public void Back()
    {
        if (listening) { CancelCapture(true); return; }
        StageboundAudioSettings.Save();
        gameObject.SetActive(false);
        back?.Invoke();
    }

    private void Update()
    {
        if (transform.parent is RectTransform parent)
            transform.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((parent.rect.width - 64) / 1140f, (parent.rect.height - 64) / 900f), .1f, 1f);
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        bool cancel = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (pad != null && pad.buttonEast.wasPressedThisFrame);
        if (listening)
        {
            if (cancel || !Application.isFocused || wheel == null || !wheel.WheelConnected || !wheel.HasLiveSample)
            { CancelCapture(true); return; }
            if (pendingButton < 0 && wheel.TryCaptureButton(out int button))
            {
                pendingButton = button;
                int conflict = WheelSettings.Conflict(pendingBinding, button);
                if (conflict < 0) AcceptCapture();
                else
                {
                    prompt.text = "Button " + button + " is already assigned to " + WheelSettings.Label((WheelSettings.Binding)conflict) + ".\nReplace clears the previous binding.";
                    replaceButton.gameObject.SetActive(true);
                    // Keep Cancel selected, so replacement is always deliberate.
                }
            }
            return;
        }
        if (cancel && Time.frameCount > openedFrame) { Back(); return; }
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) menuSource = "Keyboard";
        else if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.dpad.ReadValue().sqrMagnitude > .1f || pad.leftStick.ReadValue().sqrMagnitude > .1f)) menuSource = "Gamepad";
        else if (wheel != null && wheel.HasActivity) menuSource = "Logitech G29";
        Refresh();
    }

    private void OnDisable()
    {
        CancelCapture(false);
        StageboundAudioSettings.Save();
        if (ownsWheel && wheel != null)
        {
            wheel.enabled = false;
            Destroy(wheel.gameObject);
            wheel = null;
            ownsWheel = false;
        }
    }
}
