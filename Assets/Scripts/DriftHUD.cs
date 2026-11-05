using TMPro;
using UnityEngine;

// Presentation only: scoring and style-to-time calculations stay authoritative.
public class DriftHUD : MonoBehaviour
{
    private RaceTimer race;
    private DriftScoreSystem score;
    private TMP_Text drift, style, debug;
    private CanvasGroup emphasis;

    public DraggableHUDPanel Panel { get; private set; }

    public void Initialize(RectTransform parent, RaceTimer timer, DriftScoreSystem scoring)
    {
        race = timer;
        score = scoring;
        var panel = StageboundMenuUI.Panel(parent, "Drift style", new Vector2(0, 0), new Vector2(32, 42), new Vector2(310, 190), new Color32(10, 18, 29, 102));
        panel.localScale = Vector3.one;
        InstrumentCluster.Bar(panel, "Style separator", new Vector2(0, -35), new Vector2(270, 1), new Color32(91, 255, 191, 90));
        drift = StageboundMenuUI.Text(panel, "DRIFT", 24, new Vector2(20, -15), new Vector2(270, 115), Color.white);
        emphasis = drift.gameObject.AddComponent<CanvasGroup>();
        style = StageboundMenuUI.Text(panel, "STYLE CREDIT  -0.000 s", 22, new Vector2(20, -142), new Vector2(280, 38), StageboundMenuUI.Mint);
        foreach (var text in new[]
        {
            drift,
            style
        }

        )
        {
            text.outlineWidth = .12f;
            text.outlineColor = new Color32(5, 10, 16, 230);
        }

        Canvas.ForceUpdateCanvases();
        Panel = panel.gameObject.AddComponent<DraggableHUDPanel>();
        if (Application.isPlaying) Panel.Initialize("Stagebound.HUD.Drift.v2");
        debug = StageboundMenuUI.Text(parent, "Drift debug", 16, new Vector2(20, -220), new Vector2(620, 350), Color.white);
        debug.outlineWidth = .2f;
        debug.outlineColor = Color.black;
        debug.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (score == null || race == null || drift == null)
            return;
        var t = score.Telemetry;
        bool bank = Time.unscaledTime - score.LastBankedAt < 2f && score.LastBankedScore > 0f;
        bool active = race.raceRunning && score.ComboActive;
        float target = active || bank ? 1f : .12f;
        emphasis.alpha = Mathf.MoveTowards(emphasis.alpha, target, Time.unscaledDeltaTime * 3f);
        drift.text = bank && !active
            ? $"<size=13><color=#5BFFBF>DRIFT BANKED</color></size>\n<size=40>+{score.LastBankedScore:N0}</size>\n<size=15><color=#9DAEC1>KEEP THE FLOW</color></size>"
            : $"<size=13><color=#5BFFBF>DRIFT / {(active ? "LIVE" : "READY")}</color></size>\n<size=40>{score.ActiveComboScore:N0}</size>  <size=24><color=#5BFFBF>×{score.CurrentComboMultiplier:0.0}</color></size>\n<size=15><color=#9DAEC1>{(active ? t.CurrentDriftAngle : 0f):0}° SLIP ANGLE</color></size>";
        style.text = $"<size=11><color=#9DAEC1>STYLE CREDIT / TIME REDUCTION</color></size>\n-{score.StyleCredit:0.000} <size=16>s</size>";
        debug.gameObject.SetActive(score.showDebug);
        if (score.showDebug)
            debug.text = $"DRIFT DEBUG | {t.IsDrifting} | DIR {t.DriftDirection}\n" + $"ANGLE {t.VehicleSlipAngle:0.0}° | SPEED {t.CurrentDriftSpeed:0.0} | FORWARD {t.ForwardSpeedKmh:0.0} km/h\n" + $"YAW {t.CurrentYawRate:0.0}°/s | REAR GROUNDED {t.RearGrounded}\n" + $"BL FWD {t.BLForwardSlip:0.000} SIDE {t.BLSidewaysSlip:0.000}\n" + $"BR FWD {t.BRForwardSlip:0.000} SIDE {t.BRSidewaysSlip:0.000}\n" + $"ANGLE x{score.CurrentAngleMultiplier:0.00} SPEED x{score.CurrentSpeedMultiplier:0.00} COMBO x{score.CurrentComboMultiplier:0.00}\n" + $"ACTIVE {score.ActiveComboScore:0} BANKED {score.BankedDriftScore:0} CREDIT {score.StyleCredit:0.000}s\n" + $"AREA EXHAUSTED {score.AreaBudgetExhausted}";
    }
}
