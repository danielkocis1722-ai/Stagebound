using UnityEngine;
using TMPro;

public class Speedometer : MonoBehaviour
{
    public CarController car;
    public TMP_Text speedText;
    void Start()
    {
        if (speedText != null) speedText.gameObject.SetActive(false);
        var telemetry = GetComponent<CarTelemetry>();
        if (telemetry != null && telemetry.telemetryText != null)
            telemetry.telemetryText.gameObject.SetActive(false);
        var hud = GetComponent<RaceHUD>();
        if (hud == null) hud = gameObject.AddComponent<RaceHUD>();
        hud.car = car;
    }
}
