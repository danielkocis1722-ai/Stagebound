using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RaceTrigger : MonoBehaviour
{
    public enum TriggerType { StartFinish, Checkpoint1 }
    public TriggerType triggerType;
    [Min(1)] public int checkpointIndex = 1;
    public RaceTimer raceTimer;
    [Tooltip("Optional car-root pose for respawning after this checkpoint.")]
    public Transform respawnPoint;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();
    CarController activeCar;

    void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
    void OnDisable() { occupants.Clear(); activeCar = null; }
    void OnTriggerEnter(Collider other)
    {
        var car = other.GetComponentInParent<CarController>();
        if (car == null || (activeCar != null && activeCar != car)) return;
        var respawn = car.GetComponent<RespawnManager>();
        if (respawn != null && respawn.SuppressRaceTriggers) return;
        bool first = occupants.Count == 0;
        occupants.Add(other); activeCar = car;
        if (!first) return;
        if (raceTimer == null) raceTimer = FindAnyObjectByType<RaceTimer>();
        if (raceTimer == null) return;
        if (triggerType == TriggerType.StartFinish)
        {
            if (!raceTimer.raceRunning)
            {
                raceTimer.StartRace();
                if (raceTimer.raceRunning && respawn != null) respawn.BeginStage(raceTimer);
            }
            else raceTimer.FinishRace();
        }
        else
        {
            int passed = raceTimer.PassedCheckpoints;
            raceTimer.PassCheckpoint(checkpointIndex);
            if (raceTimer.PassedCheckpoints > passed && respawn != null)
                respawn.RecordCheckpoint(this);
        }
    }
    public void ClearCarOccupancy(CarController car)
    {
        if (activeCar != car) return;
        occupants.Clear(); activeCar = null;
    }
    void OnTriggerExit(Collider other)
    {
        occupants.Remove(other);
        if (occupants.Count == 0) activeCar = null;
    }
    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        var previousMatrix = Gizmos.matrix;
        var previousColor = Gizmos.color;
        Color color = triggerType == TriggerType.StartFinish ? Color.cyan : Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, .12f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = color;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.TransformPoint(box.center + Vector3.up * (box.size.y * .5f)),
            triggerType == TriggerType.StartFinish ? "START / FINISH" : "CHECKPOINT " + checkpointIndex);
#endif
    }
}
