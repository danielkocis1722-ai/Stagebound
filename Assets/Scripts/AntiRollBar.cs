using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AntiRollBar : MonoBehaviour
{
    [Header("Front")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public float frontAntiRoll = 2500f;
    [Header("Rear")]
    public WheelCollider rearLeft;
    public WheelCollider rearRight;
    public float rearAntiRoll = 1800f;
    Rigidbody body;

    void Awake() => body = GetComponent<Rigidbody>();
    void FixedUpdate()
    {
        ApplyAntiRoll(frontLeft, frontRight, frontAntiRoll);
        ApplyAntiRoll(rearLeft, rearRight, rearAntiRoll);
    }
    void ApplyAntiRoll(WheelCollider left, WheelCollider right, float strength)
    {
        if (left == null || right == null ||
            !left.GetGroundHit(out WheelHit leftHit) || !right.GetGroundHit(out WheelHit rightHit)) return;
        // Use a balanced couple only. With one wheel airborne, the old bar
        // applied an unopposed upward force to the remaining grounded wheel.
        float force = (Travel(left, leftHit) - Travel(right, rightHit)) * Mathf.Max(0f, strength);
        body.AddForceAtPosition(-left.transform.up * force, left.transform.position);
        body.AddForceAtPosition(right.transform.up * force, right.transform.position);
    }
    static float Travel(WheelCollider wheel, WheelHit hit) => Mathf.Clamp01(
        (-wheel.transform.InverseTransformPoint(hit.point).y + wheel.center.y - wheel.radius) /
        Mathf.Max(.001f, wheel.suspensionDistance));
}
