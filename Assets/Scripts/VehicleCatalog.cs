using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Stagebound/Vehicles/Vehicle Catalog")]
public class VehicleCatalog : ScriptableObject
{
    public VehicleDefinition defaultVehicle;
    public VehicleDefinition[] vehicles = Array.Empty<VehicleDefinition>();
    public VehicleDefinition Find(string id)
    {
        if(vehicles!=null) foreach(var vehicle in vehicles)
            if(vehicle!=null && string.Equals(vehicle.vehicleId,id,StringComparison.Ordinal)) return vehicle;
        return null;
    }
    public VehicleDefinition Fallback
    {
        get {
            if(defaultVehicle!=null && Find(defaultVehicle.vehicleId)==defaultVehicle && defaultVehicle.IsPlayable) return defaultVehicle;
            if(vehicles!=null) foreach(var vehicle in vehicles) if(vehicle!=null && vehicle.IsPlayable) return vehicle;
            return null;
        }
    }
    public bool IsValid(out string reason)
    {
        reason=null;
        if(vehicles==null || vehicles.Length==0) { reason="No vehicles configured."; return false; }
        var ids=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var vehicle in vehicles)
            if(vehicle==null || string.IsNullOrWhiteSpace(vehicle.vehicleId) || !ids.Add(vehicle.vehicleId))
            { reason="Each catalog entry needs a unique, non-empty vehicle ID."; return false; }
        if(Fallback==null) { reason="No playable vehicle is configured."; return false; }
        return true;
    }
}

