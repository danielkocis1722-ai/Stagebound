using System;
using UnityEngine;

[CreateAssetMenu(menuName="Stagebound/Stage Catalog")]
public class StageCatalog : ScriptableObject
{
    public const string SelectedStageKey="Stagebound.SelectedStageId";
    public StageDefinition[] stages=Array.Empty<StageDefinition>();
    public StageDefinition Find(string id)
    {
        if(stages!=null) foreach(var stage in stages) if(stage!=null&&stage.stageId==id) return stage;
        return null;
    }
    public StageDefinition Initial
    {
        get {
            var saved=Find(PlayerPrefs.GetString(SelectedStageKey,string.Empty));
            if(saved!=null&&saved.IsPlayable) return saved;
            if(stages!=null) foreach(var stage in stages) if(stage!=null&&stage.IsPlayable) return stage;
            return null;
        }
    }
}
