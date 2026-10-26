using UnityEngine;

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

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        ApplyAntiRoll(
            frontLeft,
            frontRight,
            frontAntiRoll
        );

        ApplyAntiRoll(
            rearLeft,
            rearRight,
            rearAntiRoll
        );
    }

    void ApplyAntiRoll(
        WheelCollider leftWheel,
        WheelCollider rightWheel,
        float antiRollForce
    )
    {
        if (leftWheel == null || rightWheel == null)
            return;

        float leftTravel = 1f;
        float rightTravel = 1f;

        bool leftGrounded =
            leftWheel.GetGroundHit(out WheelHit leftHit);

        bool rightGrounded =
            rightWheel.GetGroundHit(out WheelHit rightHit);


        if (leftGrounded)
        {
            leftTravel =
                (-leftWheel.transform.InverseTransformPoint(
                    leftHit.point
                ).y - leftWheel.radius)
                / leftWheel.suspensionDistance;
        }

        if (rightGrounded)
        {
            rightTravel =
                (-rightWheel.transform.InverseTransformPoint(
                    rightHit.point
                ).y - rightWheel.radius)
                / rightWheel.suspensionDistance;
        }


        float antiRoll =
            (leftTravel - rightTravel)
            * antiRollForce;


        if (leftGrounded)
        {
            rb.AddForceAtPosition(
                leftWheel.transform.up * -antiRoll,
                leftWheel.transform.position
            );
        }

        if (rightGrounded)
        {
            rb.AddForceAtPosition(
                rightWheel.transform.up * antiRoll,
                rightWheel.transform.position
            );
        }
    }
}
