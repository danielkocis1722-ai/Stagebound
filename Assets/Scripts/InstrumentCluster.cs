using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation-only skin. Receives authoritative values; never writes gameplay state.
public sealed class InstrumentCluster : MonoBehaviour
{
    public static readonly Color Mint = new Color32(91, 255, 191, 255);
    static readonly Color Muted = new Color32(151, 171, 186, 255);
    static readonly Color Red = new Color32(255, 104, 84, 255);
    RectTransform dialRoot, rpmNeedle, speedNeedle;
    TMP_Text rpmText, speedText, gearText, vehicleText;
    Image[] lights;
    float maxSpeed, maxRpm, redline, shownSpeed, shownRpm;
    float gearPulse;
    string lastGear;
    bool warned;
    public float MaxSpeed => maxSpeed;
    public float MaxRpm => maxRpm;
    public float Redline => redline;
    public static float GaugeAngle(float value, float maximum) => 225f - 270f * Mathf.Clamp01(float.IsFinite(value) ? value / Mathf.Max(1f, maximum) : 0f);
    public static float LabelStep(float maximum)
    {
        float rough = Mathf.Max(1f, maximum / 6f);
        float power = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rough)));
        foreach (float step in new[] { 1f, 2f, 2.5f, 4f, 5f, 10f }) if (step * power >= rough) return step * power;
        return power * 10f;
    }
    public void Configure(VehicleConfig.HudDisplay config, float engineRedline, string vehicleName)
    {
        bool valid = config != null && float.IsFinite(config.HudMaxSpeedKmh) && config.HudMaxSpeedKmh >= 60f &&
            float.IsFinite(config.HudMaxRpm) && config.HudMaxRpm >= 1000f && float.IsFinite(config.RedlineStartRpm) && config.RedlineStartRpm > 0 && config.RedlineStartRpm <= config.HudMaxRpm;
        float speed = valid ? Mathf.Min(600f, config.HudMaxSpeedKmh) : 240f;
        float rpm = valid ? Mathf.Min(20000f, config.HudMaxRpm) : 9000f;
        float red = valid ? Mathf.Min(rpm, config.RedlineStartRpm) : Mathf.Clamp(float.IsFinite(engineRedline) ? engineRedline : 7500f, 500f, rpm);
        if (!valid && !warned) { Debug.LogWarning("Instrument cluster: missing/invalid display configuration; using safe display defaults.", this); warned = true; }
        if (dialRoot != null && speed == maxSpeed && rpm == maxRpm && red == redline)
        { vehicleText.text = vehicleName; return; }
        maxSpeed = speed; maxRpm = rpm; redline = red;
        if (dialRoot != null)
        {
            dialRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(dialRoot.gameObject); else DestroyImmediate(dialRoot.gameObject);
        }
        dialRoot = Rect(transform, "Cluster skin", Vector2.zero, new Vector2(680, 290));
        var tach = Rect(dialRoot, "Tachometer", new Vector2(-180, 0), new Vector2(260, 260));
        var speedo = Rect(dialRoot, "Speedometer", new Vector2(180, 0), new Vector2(260, 260));
        rpmNeedle = BuildDial(tach, maxRpm, maxRpm <= 10000f ? 1000f : 2000f, true);
        speedNeedle = BuildDial(speedo, maxSpeed, LabelStep(maxSpeed), false);
        rpmText = Label(tach, "0", 29, new Vector2(0, -40), new Vector2(130, 40), Color.white);
        Label(tach, "RPM × 1000", 12, new Vector2(0, 34), new Vector2(140, 24), Muted);
        speedText = Label(speedo, "000", 45, new Vector2(0, -40), new Vector2(140, 55), Color.white);
        Label(speedo, "KM/H", 12, new Vector2(0, 34), new Vector2(100, 24), Muted);
        gearText = Label(dialRoot, "N", 72, new Vector2(0, -4), new Vector2(92, 90), Mint);
        Label(dialRoot, "GEAR", 11, new Vector2(0, -56), new Vector2(90, 20), Muted);
        vehicleText = Label(dialRoot, vehicleName, 13, new Vector2(0, -117), new Vector2(340, 22), Muted);
        Label(dialRoot, "STAGEBOUND  /  INSTRUMENTS", 10, new Vector2(0, -137), new Vector2(340, 20), Mint);
        lights = new Image[7];
        for (int i = 0; i < lights.Length; i++) lights[i] = Bar(dialRoot, "Shift light " + i, new Vector2((i - 3) * 13, 82), new Vector2(9, 4), new Color(1, 1, 1, .12f));
        Present(shownSpeed, shownRpm, lastGear ?? "N", engineRedline, 1f);
    }
    RectTransform BuildDial(RectTransform parent, float maximum, float major, bool tach)
    {
        Arc(parent, "Dial face", 122, 122, 0, 360, new Color32(7, 14, 22, 160));
        Arc(parent, "Outer bezel", 122, 1, 0, 360, new Color32(103, 136, 150, 65));
        Arc(parent, "Scale arc", 110, 2, 225, -270, new Color32(132, 157, 170, 115));
        Arc(parent, "Inner hairline", 77, 1, 225, -270, new Color32(100, 132, 146, 45));
        if (tach) Arc(parent, "Redline", 114, 5, GaugeAngle(redline, maximum), -270f * (1f - redline / maximum), Red);
        float minor = major / 5f;
        for (int i = 0; i <= Mathf.FloorToInt(maximum / minor); i++)
        {
            float value = i * minor, angle = GaugeAngle(value, maximum);
            bool large = i % 5 == 0 || Mathf.Abs(value - maximum) < .01f;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var tick = Bar(parent, "Tick " + i, direction * (large ? 102 : 105), new Vector2(large ? 12 : 5, large ? 2 : 1), tach && value >= redline ? Red : large ? Color.white : Muted);
            tick.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            if (large) Label(parent, (tach ? value / 1000f : value).ToString("0.#"), 14, direction * (tach ? 88f : 81f), new Vector2(42, 22), tach && value >= redline ? Red : Muted);
        }
        var needle = Rect(parent, "Needle pivot", Vector2.zero, Vector2.zero);
        Bar(needle, "Needle", new Vector2(46, 0), new Vector2(88, 2.5f), tach ? Red : Mint);
        Arc(parent, "Needle hub", 6, 6, 0, 360, Color.white);
        return needle;
    }
    public void Present(float speed, float rpm, string gear, float limiter, float dt)
    {
        if (dialRoot == null) return;
        speed = float.IsFinite(speed) ? Mathf.Max(0, speed) : 0;
        rpm = float.IsFinite(rpm) ? Mathf.Max(0, rpm) : 0;
        shownSpeed = Mathf.Lerp(shownSpeed, speed, 1f - Mathf.Exp(-10f * dt));
        shownRpm = Mathf.Lerp(shownRpm, rpm, 1f - Mathf.Exp(-18f * dt));
        speedNeedle.localEulerAngles = new Vector3(0, 0, GaugeAngle(shownSpeed, maxSpeed));
        rpmNeedle.localEulerAngles = new Vector3(0, 0, GaugeAngle(shownRpm, maxRpm));
        speedText.SetText("{0:000}", Mathf.Min(999f, Mathf.Round(speed)));
        rpmText.SetText("{0:0}", Mathf.Round(rpm));
        if (lastGear != gear) { lastGear = gear; gearText.text = gear; gearPulse = 1f; }
        gearPulse = Mathf.MoveTowards(gearPulse, 0, dt * 7f);
        gearText.rectTransform.localScale = Vector3.one * (1f + .06f * gearPulse);
        float threshold = Mathf.Max(500f, Mathf.Min(redline, float.IsFinite(limiter) && limiter > 0 ? limiter : redline));
        float progress = Mathf.InverseLerp(threshold * .8f, threshold * .99f, rpm);
        for (int i = 0; i < lights.Length; i++)
        {
            bool on = progress > (i + .5f) / lights.Length;
            Color c = i < 4 ? Mint : Red;
            c.a = on ? (rpm >= threshold * .99f ? .8f + .2f * Mathf.Sin(Time.unscaledTime * 12f) : 1f) : .12f;
            lights[i].color = c;
        }
    }
    public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; return r;
    }
    public static TMP_Text Label(Transform parent, string value, float size, Vector2 position, Vector2 bounds, Color color)
    {
        var t = Rect(parent, value, position, bounds).gameObject.AddComponent<TextMeshProUGUI>(); t.text = value; t.fontSize = size;
        t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        t.outlineWidth = .12f; t.outlineColor = new Color32(4, 9, 15, 230); return t;
    }
    public static Image Bar(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    { var image = Rect(parent, name, pos, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
    static void Arc(Transform parent, string name, float radius, float width, float start, float sweep, Color color)
    { var a = Rect(parent, name, Vector2.zero, new Vector2(radius * 2, radius * 2)).gameObject.AddComponent<ClusterArc>(); a.radius = radius; a.thickness = width; a.startDegrees = start; a.sweepDegrees = sweep; a.color = color; a.raycastTarget = false; a.SetVerticesDirty(); }
}
