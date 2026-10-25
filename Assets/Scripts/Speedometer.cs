using UnityEngine;
using TMPro;

public class Speedometer : MonoBehaviour
{
    public CarController car;
    public TMP_Text speedText;


    void Update()
    {
        if (car == null || speedText == null)
            return;


        int speed =
            Mathf.RoundToInt(car.SpeedKmh);

        int rpm =
            Mathf.RoundToInt(car.EngineRPM);


        speedText.text =
            $"{speed} km/h\n" +
            $"{car.CurrentGearText}   •   {rpm} RPM";
    }
}