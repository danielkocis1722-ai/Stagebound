using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(DriftTelemetry))]
public class DriftScoreSystem : MonoBehaviour
{
    public RaceTimer raceTimer;
    [Min(0f)] public float baseScorePerSecond = 100f;
    public AnimationCurve angleMultiplier = new AnimationCurve(new Keyframe(0, 0), new Keyframe(10, .25f),
        new Keyframe(15, .6f), new Keyframe(25, 1.5f), new Keyframe(35, 2.3f),
        new Keyframe(45, 3f), new Keyframe(55, 1f), new Keyframe(60, 0), new Keyframe(180, 0));
    public AnimationCurve speedMultiplier = new AnimationCurve(new Keyframe(0, 0), new Keyframe(25, .15f),
        new Keyframe(50, 1f), new Keyframe(80, 1.8f), new Keyframe(100, 2.3f), new Keyframe(140, 2.5f));
    [Min(1f)] public float maxSpeedMultiplier = 2.5f;
    [Min(1f)] public float maxCombo = 3f;
    [Min(0f)] public float comboGrowthPerSecond = .12f;
    [Min(0f)] public float transitionGrace = .75f;
    [Min(0f)] public float minimumTransitionDriftDuration = .35f;
    [Min(0f)] public float transitionBonus = 50f;
    [Range(50f, 90f)] public float spinAngle = 65f;
    [Min(0f)] public float spinGrace = .2f;
    [Min(0f)] public float maximumYawRate = 150f;
    [Header("Repeated-area farming protection (per run)")]
    [Min(5f)] public float scoringCellSize = 20f;
    [Min(.1f)] public float scoringSecondsPerCell = 3f;
    [Header("Optional debug overlay")]
    public bool showDebug;

    public float ActiveComboScore { get; private set; }
    public float BankedDriftScore { get; private set; }
    public float TotalDriftScore => BankedDriftScore + ActiveComboScore;
    public float StyleCredit => raceTimer != null ? raceTimer.StyleCreditFor(TotalDriftScore) : 0f;
    public float CurrentAngleMultiplier { get; private set; }
    public float CurrentSpeedMultiplier { get; private set; }
    public float CurrentComboMultiplier { get; private set; } = 1f;
    public bool ComboActive { get; private set; }
    public float LastBankedScore { get; private set; }
    public float LastBankedAt { get; private set; } = float.NegativeInfinity;
    public bool AreaBudgetExhausted { get; private set; }
    public DriftTelemetry Telemetry { get; private set; }
    readonly Dictionary<Vector2Int, float> areaSeconds = new Dictionary<Vector2Int, float>();
    float validDuration, invalidDuration, spinDuration, directionDuration;
    int previousDirection;
    bool pendingTransition;

    void Awake() { Telemetry = GetComponent<DriftTelemetry>(); }
    void FixedUpdate() => Advance(Time.fixedDeltaTime);
    public void Advance(float dt)
    {
        if (dt <= 0 || Time.timeScale <= 0 || PauseMenu.IsPaused) return;
        CurrentAngleMultiplier = CurrentSpeedMultiplier = 0f;
        if (raceTimer == null || !raceTimer.raceRunning) { EndCombo(); return; }
        var t = Telemetry;
        bool spin = t.CurrentDriftAngle >= spinAngle || Mathf.Abs(t.CurrentYawRate) >= maximumYawRate;
        spinDuration = spin ? spinDuration + dt : 0f;
        bool moving = t.CurrentDriftSpeed >= t.minimumSpeedKmh && t.ForwardSpeedKmh >= t.minimumForwardSpeedKmh;
        if (!moving || spinDuration >= spinGrace && spin)
        { EndCombo(); return; }
        bool valid = t.IsDrifting && !spin;
        if (!valid)
        {
            invalidDuration += dt;
            if (invalidDuration > transitionGrace) EndCombo();
            return;
        }
        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(transform.position.x / Mathf.Max(5f, scoringCellSize)),
            Mathf.FloorToInt(transform.position.z / Mathf.Max(5f, scoringCellSize)));
        areaSeconds.TryGetValue(cell, out float spent);
        float scoringDt = Mathf.Min(dt, Mathf.Max(0f, scoringSecondsPerCell - spent));
        AreaBudgetExhausted = scoringDt <= 0f;
        if (AreaBudgetExhausted) { EndCombo(); return; }
        areaSeconds[cell] = spent + scoringDt;
        CurrentAngleMultiplier = Mathf.Clamp(angleMultiplier.Evaluate(t.CurrentDriftAngle), 0f, 10f);
        CurrentSpeedMultiplier = Mathf.Clamp(speedMultiplier.Evaluate(t.CurrentDriftSpeed), 0f, maxSpeedMultiplier);
        if (CurrentAngleMultiplier <= 0f || CurrentSpeedMultiplier <= 0f)
        {
            invalidDuration += dt;
            if (invalidDuration > transitionGrace) EndCombo();
            return;
        }
        if (!ComboActive) { ComboActive = true; previousDirection = t.DriftDirection; }
        if (previousDirection != t.DriftDirection)
        {
            pendingTransition = directionDuration >= minimumTransitionDriftDuration && invalidDuration <= transitionGrace;
            previousDirection = t.DriftDirection; directionDuration = 0f;
        }
        invalidDuration = 0f; validDuration += scoringDt; directionDuration += scoringDt;
        if (pendingTransition && directionDuration >= minimumTransitionDriftDuration)
        { ActiveComboScore += transitionBonus; pendingTransition = false; }
        CurrentComboMultiplier = Mathf.Clamp(1f + validDuration * comboGrowthPerSecond, 1f, maxCombo);
        ActiveComboScore += baseScorePerSecond * CurrentAngleMultiplier * CurrentSpeedMultiplier *
            CurrentComboMultiplier * scoringDt;
    }
    public void EndCombo()
    {
        if (ActiveComboScore > 0f)
        {
            LastBankedScore = ActiveComboScore; LastBankedAt = Time.unscaledTime;
            BankedDriftScore += ActiveComboScore;
        }
        ActiveComboScore = validDuration = invalidDuration = spinDuration = directionDuration = 0f;
        previousDirection = 0; pendingTransition = false; ComboActive = false; CurrentComboMultiplier = 1f;
    }
    public float FinishRun() { EndCombo(); return BankedDriftScore; }
    public void ResetRun()
    {
        EndCombo(); BankedDriftScore = LastBankedScore = 0f; LastBankedAt = float.NegativeInfinity;
        CurrentAngleMultiplier = CurrentSpeedMultiplier = 0f; AreaBudgetExhausted = false;
        areaSeconds.Clear(); if (Telemetry != null) Telemetry.ResetDrift();
    }
}
