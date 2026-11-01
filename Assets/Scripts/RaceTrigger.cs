using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RaceTrigger : MonoBehaviour
{
    public enum TriggerType
    {
        StartFinish,
        Checkpoint1,
        Start,
        Finish
    }

    public TriggerType triggerType;
    [Min(1)]
    public int checkpointIndex = 1;
    public RaceTimer raceTimer;
    [Tooltip("Optional car-root pose for respawning after this checkpoint.")]
    public Transform respawnPoint;
    private readonly HashSet<Collider> occupants = new HashSet<Collider>();
    private CarController activeCar;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnDisable()
    {
        occupants.Clear();
        activeCar = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var car = other.GetComponentInParent<CarController>();
        if (car == null || (activeCar != null && activeCar != car))
            return;
        var respawn = car.GetComponent<RespawnManager>();
        if (respawn != null && respawn.SuppressRaceTriggers)
            return;
        bool first = occupants.Count == 0;
        occupants.Add(other);
        activeCar = car;
        if (!first)
            return;
        if (raceTimer == null)
            raceTimer = FindAnyObjectByType<RaceTimer>();
        if (raceTimer == null)
            return;
        if (triggerType == TriggerType.StartFinish)
        {
            if (raceTimer.CrossStartFinish() && respawn != null)
                respawn.BeginStage(raceTimer);
        }
        else if (triggerType == TriggerType.Start)
        {
            bool running = raceTimer.raceRunning;
            raceTimer.StartRace();
            if (!running && raceTimer.raceRunning && respawn != null)
                respawn.BeginStage(raceTimer);
        }
        else if (triggerType == TriggerType.Finish)
            raceTimer.FinishRace();
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
        if (activeCar != car)
            return;
        occupants.Clear();
        activeCar = null;
    }

    private void OnTriggerExit(Collider other)
    {
        occupants.Remove(other);
        if (occupants.Count == 0)
            activeCar = null;
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
            return;
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
        UnityEditor.Handles.Label(transform.TransformPoint(box.center + Vector3.up * (box.size.y * .5f)), triggerType == TriggerType.Checkpoint1 ? "CHECKPOINT " + checkpointIndex : triggerType.ToString());
#endif
    }
}
