using System;
using System.Collections.Generic;
using UnityEngine;

public class RaceTimer : MonoBehaviour
{
    public StageDefinition stageDefinition;
    public DriftScoreSystem driftScore;
    public float elapsedTime;
    public bool raceRunning;

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
            RawTime = rawTime;
            DriftScore = score;
            StyleCredit = credit;
            Time = Mathf.Max(0f, rawTime - credit);
            Best = best;
            Gap = gap;
            IsNewBest = isNewBest;
            CompletedAt = completedAt;
        }
    }

    public RunResult LastResult { get; private set; }
    public float? LatestSplit => splits.Count > 0 ? splits[splits.Count - 1] : (float?)null;
    public string BestTimeKey => BuildBestTimeKey(gameObject.scene.path, CountCheckpoints());
    public string StageRecordKey => stageDefinition != null ? StageRecords.OverallKey(stageDefinition, CountCheckpoints()) : BestTimeKey + ".style2";

    public float StyleCreditFor(float score) => stageDefinition != null ? stageDefinition.CreditFor(score) : Mathf.Clamp(score / 2000f, 0f, 8f);

    private readonly List<float> splits = new List<float>();
    private float[] bestSplits = Array.Empty<float>();
    private string key;
    private bool hasBest;

    private void Start()
    {
        var checkpoints = new SortedSet<int>();
        bool start = false, finish = false;
        foreach (var trigger in FindObjectsByType<RaceTrigger>())
        {
            if (trigger.gameObject.scene != gameObject.scene)
                continue;
            if (trigger.raceTimer != null && trigger.raceTimer != this)
                continue;
            trigger.raceTimer = this;
            if (trigger.triggerType == RaceTrigger.TriggerType.Checkpoint1)
                checkpoints.Add(trigger.checkpointIndex);
            else
            {
                start |= trigger.triggerType == RaceTrigger.TriggerType.StartFinish || trigger.triggerType == RaceTrigger.TriggerType.Start;
                finish |= trigger.triggerType == RaceTrigger.TriggerType.StartFinish || trigger.triggerType == RaceTrigger.TriggerType.Finish;
            }
        }

        TotalCheckpoints = checkpoints.Count;
        TrackReady = start && finish && TotalCheckpoints > 0;
        int expected = 1;
        foreach (int index in checkpoints)
            if (index != expected++)
                TrackReady = false;
        // Increment this suffix when the route or checkpoint positions change.
        key = StageRecordKey;
        StageRecords.PrepareVehicleRecords(stageDefinition, TotalCheckpoints, gameObject.scene.path);
        LoadRecords();
        ResetTimer();
    }

    private void LoadRecords()
    {
        var saved = StageRecords.Overall(stageDefinition, TotalCheckpoints, gameObject.scene.path);
        BestTime = saved.bestAdjustedTime;
        BestRawTime = saved.bestRawTime;
        BestDriftScore = saved.bestDriftScore;
        hasBest = saved.hasBest;
        bestSplits = saved.rawSplitsOfBestAdjusted;
    }

    public bool HasBest => hasBest;

    private void Update()
    {
        if (raceRunning)
            elapsedTime += Time.deltaTime;
    }

    public void StartRace()
    {
        if (!TrackReady || raceRunning)
            return;
        ResetTimer();
        raceRunning = true;
    }

    public void PassCheckpoint(int index)
    {
        if (!raceRunning || index != PassedCheckpoints + 1 || index > TotalCheckpoints)
            return;
        splits.Add(elapsedTime);
        PassedCheckpoints++;
        Delta = bestSplits.Length == TotalCheckpoints ? elapsedTime - bestSplits[index - 1] : (float?)null;
    }

    public void FinishRace()
    {
        if (!raceRunning || PassedCheckpoints != TotalCheckpoints || elapsedTime <= 0f)
            return;
        raceRunning = false;
        float score = driftScore != null ? driftScore.FinishRun() : 0f;
        float credit = StyleCreditFor(score);
        float adjusted = Mathf.Max(0f, elapsedTime - credit);
        Delta = hasBest ? adjusted - BestTime : (float?)null;
        NewBest = !hasBest || adjusted < BestTime;
        BestRawTime = BestRawTime <= 0f ? elapsedTime : Mathf.Min(BestRawTime, elapsedTime);
        BestDriftScore = Mathf.Max(BestDriftScore, score);
        if (NewBest)
        {
            BestTime = adjusted;
            hasBest = true;
            bestSplits = splits.ToArray();
        }

        StageRecords.RecordVehicleRun(stageDefinition, TotalCheckpoints, VehicleId(), elapsedTime, score, credit, splits.ToArray());
        PlayerPrefs.SetString(key, JsonUtility.ToJson(new StageRecords.Record { hasBest = hasBest, bestAdjustedTime = BestTime, bestRawTime = BestRawTime, bestDriftScore = BestDriftScore, rawSplitsOfBestAdjusted = bestSplits }));
        PlayerPrefs.Save();
        LastResult = new RunResult(elapsedTime, score, credit, BestTime, Delta, NewBest, Time.unscaledTime);
        if (stageDefinition != null && stageDefinition.stageType == StageDefinition.StageType.PointToPoint)
            return;
        // The same line crossing begins the next timed run; result data is independent.
        ResetRunState();
        raceRunning = true;
    }

    public bool CrossStartFinish()
    {
        if (!TrackReady)
            return false;
        if (!raceRunning)
        {
            StartRace();
            return raceRunning;
        }

        if (PassedCheckpoints != TotalCheckpoints || elapsedTime <= 0f)
            return false;
        FinishRace();
        return true;
    }

    public void ResetTimer()
    {
        ResetRunState();
        LastResult = null;
    }

    private void ResetRunState()
    {
        if (driftScore != null)
            driftScore.ResetRun();
        elapsedTime = 0;
        PassedCheckpoints = 0;
        raceRunning = NewBest = false;
        Delta = null;
        splits.Clear();
    }

    public static string BuildBestTimeKey(string scenePath, int checkpointCount) => "Stagebound.Best." + scenePath + "." + checkpointCount + ".v1";

    private int CountCheckpoints()
    {
        var indices = new HashSet<int>();
        foreach (var trigger in FindObjectsByType<RaceTrigger>())
            if (trigger.gameObject.scene == gameObject.scene && (trigger.raceTimer == null || trigger.raceTimer == this) && trigger.triggerType == RaceTrigger.TriggerType.Checkpoint1)
                indices.Add(trigger.checkpointIndex);
        return indices.Count;
    }

    public void ResetBestTime()
    {
        // Delete just this route's record. HUD layout, opacity and other settings remain intact.
        PlayerPrefs.DeleteKey(BestTimeKey);
        PlayerPrefs.DeleteKey(StageRecordKey);
        StageRecords.ResetVehicles(stageDefinition, CountCheckpoints());
        PlayerPrefs.Save();
        BestTime = BestRawTime = BestDriftScore = 0;
        hasBest = false;
        bestSplits = Array.Empty<float>();
        Delta = null;
        NewBest = false;
    }

    private string VehicleId()
    {
        foreach (var rig in FindObjectsByType<VehicleRigLoader>())
            if (rig.gameObject.scene == gameObject.scene && rig.CurrentDefinition != null)
                return rig.CurrentDefinition.vehicleId;
        return string.Empty;
    }

    public static string FormatTime(float time)
    {
        int ms = Mathf.FloorToInt(Mathf.Max(0, time) * 1000);
        return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}";
    }
}
