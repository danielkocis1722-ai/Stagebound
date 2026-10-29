using System;
using System.Collections.Generic;
using UnityEngine;

// Read the existing overall format and maintain an independent record per car.
// No timer/checkpoint/scoring decisions are made here.
public static class StageRecords
{
    [Serializable] public class Record
    {
        public int version=2;
        public bool hasBest;
        public float bestAdjustedTime, bestRawTime, bestDriftScore;
        public float[] rawSplitsOfBestAdjusted=Array.Empty<float>();
    }
    [Serializable] class Legacy { public float time; public float[] splits; }
    [Serializable] class CarIndex { public List<string> ids=new List<string>(); }
    public static string OverallKey(StageDefinition stage,int count) =>
        "Stagebound.Stage."+stage.stageId+"."+count+".v"+stage.recordVersion+".style2";
    public static string VehicleKey(StageDefinition stage,int count,string vehicleId) =>
        OverallKey(stage,count)+".car."+Uri.EscapeDataString(vehicleId);
    static string MigrationKey(StageDefinition stage,int count)=>OverallKey(stage,count)+".cars.v1.migrated";
    static string IndexKey(StageDefinition stage,int count)=>OverallKey(stage,count)+".cars.v1.index";
    static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    static Record Read(string key,int count)
    {
        try {
            var record=JsonUtility.FromJson<Record>(PlayerPrefs.GetString(key,"{}"));
            if(record!=null&&record.version==2&&record.hasBest&&Finite(record.bestAdjustedTime)&&record.bestAdjustedTime>=0&&
                Finite(record.bestRawTime)&&record.bestRawTime>0&&record.rawSplitsOfBestAdjusted!=null&&record.rawSplitsOfBestAdjusted.Length==count) {
                record.bestDriftScore=Finite(record.bestDriftScore)?Mathf.Max(0,record.bestDriftScore):0;
                return record;
            }
        }catch(ArgumentException) { }
        return new Record();
    }
    public static Record Overall(StageDefinition stage,int count,string scenePath=null)
    {
        if(stage==null) return new Record();
        var record=Read(OverallKey(stage,count),count);
        if(record.hasBest || !stage.importLegacySceneBest) return record;
        try {
            var old=JsonUtility.FromJson<Legacy>(PlayerPrefs.GetString(RaceTimer.BuildBestTimeKey(scenePath??stage.scenePath,count),"{}"));
            if(old!=null&&Finite(old.time)&&old.time>0&&old.splits!=null&&old.splits.Length==count)
                return new Record {hasBest=true,bestAdjustedTime=old.time,bestRawTime=old.time,rawSplitsOfBestAdjusted=old.splits};
        }catch(ArgumentException) { }
        return record;
    }
    public static Record ForVehicle(StageDefinition stage,int count,string vehicleId)
    {
        if(stage==null||string.IsNullOrEmpty(vehicleId)) return new Record();
        var record=Read(VehicleKey(stage,count,vehicleId),count);
        // Before this feature AE86 was the only playable car. Show its existing
        // record until the one-time snapshot is frozen at the next race startup.
        if(!record.hasBest&&vehicleId=="ae86"&&!PlayerPrefs.HasKey(MigrationKey(stage,count))) return Overall(stage,count);
        return record;
    }
    static CarIndex Index(StageDefinition stage,int count)
    {
        try {
            var index=JsonUtility.FromJson<CarIndex>(PlayerPrefs.GetString(IndexKey(stage,count),"{}"));
            if(index!=null&&index.ids!=null) return index;
        }catch(ArgumentException) { }
        return new CarIndex();
    }
    static void Write(StageDefinition stage,int count,string vehicleId,Record record)
    {
        PlayerPrefs.SetString(VehicleKey(stage,count,vehicleId),JsonUtility.ToJson(record));
        var index=Index(stage,count); if(!index.ids.Contains(vehicleId)) index.ids.Add(vehicleId);
        PlayerPrefs.SetString(IndexKey(stage,count),JsonUtility.ToJson(index));
    }
    public static void PrepareVehicleRecords(StageDefinition stage,int count,string scenePath)
    {
        if(stage==null||PlayerPrefs.HasKey(MigrationKey(stage,count))) return;
        var legacy=Overall(stage,count,scenePath);
        if(legacy.hasBest&&!Read(VehicleKey(stage,count,"ae86"),count).hasBest) Write(stage,count,"ae86",legacy);
        PlayerPrefs.SetInt(MigrationKey(stage,count),1); PlayerPrefs.Save();
    }
    public static void RecordVehicleRun(StageDefinition stage,int count,string vehicleId,float raw,float score,float credit,float[] splits)
    {
        if(stage==null||string.IsNullOrEmpty(vehicleId)||!Finite(raw)||raw<=0||!Finite(score)||!Finite(credit)||splits==null||splits.Length!=count) return;
        PrepareVehicleRecords(stage,count,stage.scenePath);
        var record=Read(VehicleKey(stage,count,vehicleId),count);float adjusted=Mathf.Max(0,raw-credit);
        if(!record.hasBest||adjusted<record.bestAdjustedTime) {
            record.bestAdjustedTime=adjusted; record.rawSplitsOfBestAdjusted=(float[])splits.Clone();
        }
        record.bestRawTime=record.hasBest?Mathf.Min(record.bestRawTime,raw):raw;
        record.bestDriftScore=Mathf.Max(record.bestDriftScore,Mathf.Max(0,score)); record.hasBest=true;
        Write(stage,count,vehicleId,record); PlayerPrefs.Save();
    }
    public static void ResetVehicles(StageDefinition stage,int count)
    {
        if(stage==null) return;
        var index=Index(stage,count);if(!index.ids.Contains("ae86"))index.ids.Add("ae86");
        foreach(var id in index.ids) if(!string.IsNullOrEmpty(id)) PlayerPrefs.DeleteKey(VehicleKey(stage,count,id));
        PlayerPrefs.DeleteKey(MigrationKey(stage,count));PlayerPrefs.DeleteKey(IndexKey(stage,count));
    }
}
