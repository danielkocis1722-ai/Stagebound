using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(menuName = "Stagebound/Stage Definition")]
public class StageDefinition : ScriptableObject
{
    [Tooltip("Stable record identity. Use a new ID for a different route.")]
    public string stageId = "new-stage";
    public string stageName = "New Stage";
    [Header("Map selection")]
    [Tooltip("Scene asset path, also enabled in Build Settings.")]
    public string scenePath;
    [TextArea(2,4)] public string description;
    public bool selectable = true;
    public string unavailableLabel = "COMING SOON / DEVELOPMENT";
    [Min(1)] public int checkpointCount = 1;
    public int BuildIndex => string.IsNullOrWhiteSpace(scenePath)?-1:SceneUtility.GetBuildIndexByScenePath(scenePath);
    public bool IsPlayable => selectable && BuildIndex>=0;
    [Min(1)] public int recordVersion = 1;
    [Tooltip("Import this scene's old raw-time record only when no style record exists. Enable for upgraded legacy routes.")]
    public bool importLegacySceneBest;
    [Min(1f)] public float pointsPerSecondCredit = 2000f;
    [Min(0f)] public float maxStyleCredit = 8f;

    public float CreditFor(float score) => Mathf.Clamp(Mathf.Max(0f, score) /
        Mathf.Max(1f, pointsPerSecondCredit), 0f, Mathf.Max(0f, maxStyleCredit));
}
