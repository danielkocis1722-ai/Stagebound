using TMPro;
using UnityEngine;

// A separate presentation panel. Existing timing/dashboard geometry is untouched.
public class DriftHUD : MonoBehaviour
{
    RaceTimer race;
    DriftScoreSystem score;
    TMP_Text drift, style, debug;
    CanvasGroup emphasis;
    public DraggableHUDPanel Panel { get; private set; }

    public void Initialize(RectTransform parent, RaceTimer timer, DriftScoreSystem scoring)
    {
        race = timer; score = scoring;
        var panel = StageboundMenuUI.Panel(parent, "Drift style", new Vector2(1, .5f),
            new Vector2(-20, -160), new Vector2(270, 215), new Color32(10, 18, 29, 102));
        panel.localScale = Vector3.one * .65f;
        drift = StageboundMenuUI.Text(panel, "DRIFT", 24, new Vector2(18, -12), new Vector2(240, 145), Color.white);
        emphasis = drift.gameObject.AddComponent<CanvasGroup>();
        style = StageboundMenuUI.Text(panel, "STYLE  -0.000 s", 23, new Vector2(18, -172),
            new Vector2(240, 35), StageboundMenuUI.Mint);
        foreach (var text in new[] { drift, style })
        { text.outlineWidth = .12f; text.outlineColor = new Color32(5, 10, 16, 230); }
        Canvas.ForceUpdateCanvases();
        Panel = panel.gameObject.AddComponent<DraggableHUDPanel>();
        Panel.Initialize("Stagebound.HUD.Drift.v1");
        debug = StageboundMenuUI.Text(parent, "Drift debug", 16, new Vector2(20, -220),
            new Vector2(620, 350), Color.white);
        debug.outlineWidth = .2f; debug.outlineColor = Color.black;
        debug.gameObject.SetActive(false);
    }
    void LateUpdate()
    {
        if (score == null || race == null || drift == null) return;
        var t = score.Telemetry;
        bool bank = Time.unscaledTime - score.LastBankedAt < 2f && score.LastBankedScore > 0f;
        bool active = race.raceRunning && score.ComboActive;
        float target = active || bank ? 1f : .2f;
        emphasis.alpha = Mathf.MoveTowards(emphasis.alpha, target, Time.unscaledDeltaTime * 3f);
        drift.text = bank && !active ? $"<color=#5BFFBF>DRIFT BANKED</color>\n<size=36>+{score.LastBankedScore:N0}</size>\n<size=20>KEEP FLOWING</size>"
            : $"<color=#9DAEC1>DRIFT</color>\n<size=32>{(active ? t.CurrentDriftAngle : 0f):0}°  <size=20>{t.CurrentDriftSpeed:0} KM/H</size></size>\n<size=27>x{score.CurrentComboMultiplier:0.0}</size>\n<size=32>+{score.ActiveComboScore:N0}</size>";
        style.text = $"STYLE  -{score.StyleCredit:0.000} s";
        debug.gameObject.SetActive(score.showDebug);
        if (score.showDebug)
            debug.text = $"DRIFT DEBUG | {t.IsDrifting} | DIR {t.DriftDirection}\n" +
                $"ANGLE {t.VehicleSlipAngle:0.0}° | SPEED {t.CurrentDriftSpeed:0.0} | FORWARD {t.ForwardSpeedKmh:0.0} km/h\n" +
                $"YAW {t.CurrentYawRate:0.0}°/s | REAR GROUNDED {t.RearGrounded}\n" +
                $"BL FWD {t.BLForwardSlip:0.000} SIDE {t.BLSidewaysSlip:0.000}\n" +
                $"BR FWD {t.BRForwardSlip:0.000} SIDE {t.BRSidewaysSlip:0.000}\n" +
                $"ANGLE x{score.CurrentAngleMultiplier:0.00} SPEED x{score.CurrentSpeedMultiplier:0.00} COMBO x{score.CurrentComboMultiplier:0.00}\n" +
                $"ACTIVE {score.ActiveComboScore:0} BANKED {score.BankedDriftScore:0} CREDIT {score.StyleCredit:0.000}s\n" +
                $"AREA EXHAUSTED {score.AreaBudgetExhausted}";
    }
}
