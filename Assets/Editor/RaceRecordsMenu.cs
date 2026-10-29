using UnityEditor;
using UnityEngine;

public static class RaceRecordsMenu
{
    [MenuItem("Stagebound/Reset Best Time")]
    public static void ResetBestTime()
    {
        // Resolve only the active scene's timer; never clear another scene's record.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var timer = root.GetComponentInChildren<RaceTimer>();
            if (timer == null) continue;
            string key = timer.BestTimeKey + " + " + timer.StageRecordKey;
            timer.ResetBestTime();
            Debug.Log("Reset Stagebound best time: " + key, timer);
            return;
        }
        Debug.LogWarning("No active RaceTimer in the current scene. Open SampleScene to reset its best time.");
    }
}
