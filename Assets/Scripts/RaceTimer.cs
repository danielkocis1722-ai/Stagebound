using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    public TMP_Text timerText;
    public float elapsedTime;
    public bool raceRunning, raceFinished, checkpoint1Passed;
    public int PassedCheckpoints { get; private set; }
    public int TotalCheckpoints { get; private set; }
    public float BestTime { get; private set; }
    public float? Delta { get; private set; }
    public bool NewBest { get; private set; }
    public bool TrackReady { get; private set; }
    public sealed class RunResult
    {
        public readonly float Time, Best, CompletedAt;
        public readonly float? Gap;
        public readonly bool IsNewBest;
        public RunResult(float time, float best, float? gap, bool isNewBest, float completedAt)
        { Time = time; Best = best; Gap = gap; IsNewBest = isNewBest; CompletedAt = completedAt; }
    }
    public RunResult LastResult { get; private set; }
    public float? LatestSplit => splits.Count > 0 ? splits[splits.Count - 1] : (float?)null;
    public string BestTimeKey => BuildBestTimeKey(gameObject.scene.path, CountCheckpoints());
    readonly List<float> splits = new List<float>();
    float[] bestSplits = Array.Empty<float>();
    string key;

    [Serializable] class Record { public float time; public float[] splits; }

    void Start()
    {
        var checkpoints = new SortedSet<int>();
        bool start = false;
        foreach (var trigger in FindObjectsByType<RaceTrigger>())
        {
            if (trigger.gameObject.scene != gameObject.scene) continue;
            if (trigger.raceTimer != null && trigger.raceTimer != this) continue;
            trigger.raceTimer = this;
            if (trigger.triggerType == RaceTrigger.TriggerType.StartFinish) start = true;
            else checkpoints.Add(trigger.checkpointIndex);
        }
        TotalCheckpoints = checkpoints.Count;
        TrackReady = start && TotalCheckpoints > 0;
        int expected = 1;
        foreach (int index in checkpoints) if (index != expected++) TrackReady = false;
        // Increment this suffix when the route or checkpoint positions change.
        key = BuildBestTimeKey(gameObject.scene.path, TotalCheckpoints);
        try
        {
            var record = JsonUtility.FromJson<Record>(PlayerPrefs.GetString(key, "{}"));
            if (record != null && record.time > 0 && record.splits != null && record.splits.Length == TotalCheckpoints)
            { BestTime = record.time; bestSplits = record.splits; }
        }
        catch (ArgumentException) { BestTime = 0; }
        ResetTimer();
    }
    void Update()
    {
        if (raceRunning) elapsedTime += Time.deltaTime;
        if (timerText != null) timerText.text = GetFormattedTime();
    }
    public void StartRace()
    {
        if (!TrackReady || raceRunning) return;
        ResetTimer();
        raceRunning = true;
    }
    public void PassCheckpoint1() => PassCheckpoint(1);
    public void PassCheckpoint(int index)
    {
        if (!raceRunning || index != PassedCheckpoints + 1 || index > TotalCheckpoints) return;
        splits.Add(elapsedTime);
        PassedCheckpoints++;
        checkpoint1Passed = PassedCheckpoints > 0;
        Delta = bestSplits.Length == TotalCheckpoints ? elapsedTime - bestSplits[index - 1] : (float?)null;
    }
    public void FinishRace()
    {
        if (!raceRunning || PassedCheckpoints != TotalCheckpoints || elapsedTime <= 0f) return;
        raceRunning = false;
        raceFinished = true;
        Delta = BestTime > 0 ? elapsedTime - BestTime : (float?)null;
        NewBest = BestTime <= 0 || elapsedTime < BestTime;
        if (NewBest)
        {
            BestTime = elapsedTime;
            bestSplits = splits.ToArray();
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new Record { time = BestTime, splits = bestSplits }));
            PlayerPrefs.Save();
        }
        LastResult = new RunResult(elapsedTime, BestTime, Delta, NewBest, Time.unscaledTime);
        // The same line crossing begins the next timed run; result data is independent.
        ResetRunState();
        raceRunning = true;
    }
    public bool CrossStartFinish()
    {
        if (!TrackReady) return false;
        if (!raceRunning) { StartRace(); return raceRunning; }
        if (PassedCheckpoints != TotalCheckpoints || elapsedTime <= 0f) return false;
        FinishRace();
        return true;
    }
    public void ResetTimer()
    {
        ResetRunState();
        LastResult = null;
    }
    void ResetRunState()
    {
        elapsedTime = 0; PassedCheckpoints = 0;
        raceRunning = raceFinished = checkpoint1Passed = NewBest = false;
        Delta = null; splits.Clear();
    }
    public static string BuildBestTimeKey(string scenePath, int checkpointCount) =>
        "Stagebound.Best." + scenePath + "." + checkpointCount + ".v1";

    int CountCheckpoints()
    {
        var indices = new HashSet<int>();
        foreach (var trigger in FindObjectsByType<RaceTrigger>())
            if (trigger.gameObject.scene == gameObject.scene &&
                (trigger.raceTimer == null || trigger.raceTimer == this) &&
                trigger.triggerType != RaceTrigger.TriggerType.StartFinish)
                indices.Add(trigger.checkpointIndex);
        return indices.Count;
    }

    public void ResetBestTime()
    {
        // Delete just this route's record. HUD layout, opacity and other settings remain intact.
        PlayerPrefs.DeleteKey(BestTimeKey);
        PlayerPrefs.Save();
        BestTime = 0; bestSplits = Array.Empty<float>();
        Delta = null; NewBest = false;
    }
    public string GetFormattedTime() => FormatTime(elapsedTime);
    public static string FormatTime(float time)
    {
        int ms = Mathf.FloorToInt(Mathf.Max(0, time) * 1000);
        return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}";
    }
}
