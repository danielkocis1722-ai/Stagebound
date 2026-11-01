using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Inspection and A/B controls only. Never writes forces, steering or vehicle pose.
public class RealScaleLab : MonoBehaviour
{
    public string variantLabel;
    public string otherScene;
    private CarController car;
    private DriftYawStability governor;

    private void Start()
    {
        car = GetComponent<CarController>();
        governor = GetComponent<DriftYawStability>();
    }

    private void Update()
    {
        var keys = Keyboard.current;
        if (keys == null) return;
        if (keys.f6Key.wasPressedThisFrame && governor != null) governor.enabled = !governor.enabled;
        if (keys.f7Key.wasPressedThisFrame && !PauseMenu.IsPaused) SceneManager.LoadScene(otherScene);
    }

    private void OnGUI()
    {
        if (car == null) return;
        GUILayout.BeginArea(new Rect(12, 12, 380, 150), GUI.skin.box);
        GUILayout.Label(variantLabel);
        GUILayout.Label($"Speed {car.SpeedKmh:F1} km/h | signed {car.SignedLongitudinalSpeedKmh:F1}");
        GUILayout.Label($"Steer {car.frontLeft.steerAngle:F1} / {car.frontRight.steerAngle:F1} degrees | yaw {car.YawRate:F1} deg/s");
        GUILayout.Label($"Slip angle {car.VehicleSlipAngle:F1} degrees | rear RPM {car.SignedRearWheelRPM:F0}");
        GUILayout.Label("F6: yaw governor " + (governor != null && governor.enabled ? "ON" : "OFF") + "   F7: compare other variant");
        GUILayout.Label("Existing driving, gear, clutch, pause and respawn controls apply.");
        GUILayout.EndArea();
    }
}
