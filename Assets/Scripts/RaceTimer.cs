using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    public StageDefinition stageDefinition;
    public DriftScoreSystem driftScore;
    public TMP_Text timerText;
    public float elapsedTime;
    public bool raceRunning, raceFinished, checkpoint1Passed;
    public int PassedCheckpoints { get; private set; }
    public int TotalCheckpoints { get; private set; }
    public float BestTime { get; private set; }
    public float BestRawTime { get; private set; }
    public float BestDriftScore { get; private set; }
    public float? Delta { get; private set; }
    public bool NewBest { get; private set; }
    public bool TrackReady { get; private set; }
    public sealed class RunResult
    {
        public readonly float Time, RawTime, DriftScore, StyleCredit, Best, CompletedAt;
        public float FinalAdjustedTime => Time;
        public readonly float? Gap;
        public readonly bool IsNewBest;
        public RunResult(float rawTime, float score, float credit, float best, float? gap, bool isNewBest, float completedAt)
        {
            RawTime = rawTime; DriftScore = score; StyleCredit = credit;
            Time = Mathf.Max(0f, rawTime - credit); Best = best;
            Gap = gap; IsNewBest = isNewBest; CompletedAt = completedAt;
        }
    }
    public RunResult LastResult { get; private set; }
    public float? LatestSplit => splits.Count > 0 ? splits[splits.Count - 1] : (float?)null;
    public string BestTimeKey => BuildBestTimeKey(gameObject.scene.path, CountCheckpoints());
    public string StageRecordKey => stageDefinition != null
        ? "Stagebound.Stage." + stageDefinition.stageId + "." + CountCheckpoints() + ".v" + stageDefinition.recordVersion + ".style2"
        : BestTimeKey + ".style2";
    public float StyleCreditFor(float score) => stageDefinition != null ? stageDefinition.CreditFor(score)
        : Mathf.Clamp(score / 2000f, 0f, 8f);
    readonly List<float> splits = new List<float>();
    float[] bestSplits = Array.Empty<float>();
    string key;
    bool hasBest;

    [Serializable] class Record { public float time; public float[] splits; }
    [Serializable] class StyleRecord
    {
        public int version = 2;
        public bool hasBest;
        public float bestAdjustedTime, bestRawTime, bestDriftScore;
        public float[] rawSplitsOfBestAdjusted;
    }

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
        key = StageRecordKey;
        StageRecords.PrepareVehicleRecords(stageDefinition,TotalCheckpoints,gameObject.scene.path);
        LoadRecords();
        ResetTimer();
    }
    void LoadRecords()
    {
        BestTime = BestRawTime = BestDriftScore = 0f; hasBest = false;
        bestSplits = Array.Empty<float>();
        try
        {
            var saved = JsonUtility.FromJson<StyleRecord>(PlayerPrefs.GetString(key, "{}"));
            if (saved != null && saved.version == 2 && saved.hasBest && Finite(saved.bestAdjustedTime) &&
                Finite(saved.bestRawTime) && saved.bestRawTime > 0f && saved.bestAdjustedTime >= 0f &&
                saved.rawSplitsOfBestAdjusted != null && saved.rawSplitsOfBestAdjusted.Length == TotalCheckpoints)
            {
                BestTime = saved.bestAdjustedTime; BestRawTime = saved.bestRawTime;
                BestDriftScore = Finite(saved.bestDriftScore) ? Mathf.Max(0f, saved.bestDriftScore) : 0f;
                bestSplits = saved.rawSplitsOfBestAdjusted; hasBest = true; return;
            }
        }
        catch (ArgumentException) { }
        // Legacy raw records remain intact. Treat them as zero-credit runs until beaten.
        if (stageDefinition != null && !stageDefinition.importLegacySceneBest) return;
        try
        {
            var old = JsonUtility.FromJson<Record>(PlayerPrefs.GetString(BestTimeKey, "{}"));
            if (old != null && Finite(old.time) && old.time > 0f && old.splits != null && old.splits.Length == TotalCheckpoints)
            { BestTime = BestRawTime = old.time; bestSplits = old.splits; hasBest = true; }
        }
        catch (ArgumentException) { }
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public bool HasBest => hasBest;
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
        float score = driftScore != null ? driftScore.FinishRun() : 0f;
        float credit = StyleCreditFor(score);
        float adjusted = Mathf.Max(0f, elapsedTime - credit);
        Delta = hasBest ? adjusted - BestTime : (float?)null;
        NewBest = !hasBest || adjusted < BestTime;
        BestRawTime = BestRawTime <= 0f ? elapsedTime : Mathf.Min(BestRawTime, elapsedTime);
        BestDriftScore = Mathf.Max(BestDriftScore, score);
        if (NewBest)
        {
            BestTime = adjusted; hasBest = true;
            bestSplits = splits.ToArray();
        }
        StageRecords.RecordVehicleRun(stageDefinition,TotalCheckpoints,VehicleId(),elapsedTime,score,credit,splits.ToArray());
        PlayerPrefs.SetString(key, JsonUtility.ToJson(new StyleRecord { hasBest = hasBest,
            bestAdjustedTime = BestTime, bestRawTime = BestRawTime, bestDriftScore = BestDriftScore,
            rawSplitsOfBestAdjusted = bestSplits }));
        PlayerPrefs.Save();
        LastResult = new RunResult(elapsedTime, score, credit, BestTime, Delta, NewBest, Time.unscaledTime);
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
        if (driftScore != null) driftScore.ResetRun();
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
        PlayerPrefs.DeleteKey(StageRecordKey);
        StageRecords.ResetVehicles(stageDefinition,CountCheckpoints());
        PlayerPrefs.Save();
        BestTime = BestRawTime = BestDriftScore = 0; hasBest = false; bestSplits = Array.Empty<float>();
        Delta = null; NewBest = false;
    }
    public string GetFormattedTime() => FormatTime(elapsedTime);
    string VehicleId()
    {
        foreach(var rig in FindObjectsByType<VehicleRigLoader>())
            if(rig.gameObject.scene==gameObject.scene && rig.CurrentDefinition!=null) return rig.CurrentDefinition.vehicleId;
        return string.Empty;
    }
    public static string FormatTime(float time)
    {
        int ms = Mathf.FloorToInt(Mathf.Max(0, time) * 1000);
        return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}";
    }
}
