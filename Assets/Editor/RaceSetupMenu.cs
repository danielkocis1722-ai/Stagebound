#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RaceSetupMenu
{
    [MenuItem("Stagebound/Race/Create Start-Finish at Scene View")]
    static void StartGate() => CreateGate(true);
    [MenuItem("Stagebound/Race/Create Checkpoint at Scene View")]
    static void Checkpoint() => CreateGate(false);
    static void CreateGate(bool start)
    {
        int next = 1;
        foreach (var gate in Object.FindObjectsByType<RaceTrigger>())
        {
            if (start && gate.triggerType == RaceTrigger.TriggerType.StartFinish)
            { Selection.activeGameObject = gate.gameObject; return; }
            if (gate.triggerType != RaceTrigger.TriggerType.StartFinish)
                next = Mathf.Max(next, gate.checkpointIndex + 1);
        }
        var obj = new GameObject(start ? "Race Start-Finish" : "Race Checkpoint " + next);
        Undo.RegisterCreatedObjectUndo(obj, "Create race gate");
        var view = SceneView.lastActiveSceneView;
        if (view != null) obj.transform.position = view.pivot;
        var collider = obj.AddComponent<BoxCollider>();
        collider.isTrigger = true; collider.size = new Vector3(16, 8, 1);
        var trigger = obj.AddComponent<RaceTrigger>();
        trigger.triggerType = start ? RaceTrigger.TriggerType.StartFinish : RaceTrigger.TriggerType.Checkpoint1;
        trigger.checkpointIndex = next;
        trigger.raceTimer = Object.FindAnyObjectByType<RaceTimer>();
        Selection.activeGameObject = obj;
        EditorSceneManager.MarkSceneDirty(obj.scene);
    }
}
#endif
