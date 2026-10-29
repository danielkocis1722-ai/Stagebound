using UnityEngine;

public static class VehicleSelection
{
    public const string PlayerPrefsKey = "Stagebound.SelectedVehicleId";
    public static VehicleDefinition Resolve(VehicleCatalog catalog)
    {
        if(catalog==null || !catalog.IsValid(out _)) return null;
        var saved=catalog.Find(PlayerPrefs.GetString(PlayerPrefsKey,string.Empty));
        return saved!=null && saved.IsPlayable ? saved : catalog.Fallback;
    }
    public static bool Save(VehicleCatalog catalog, VehicleDefinition vehicle)
    {
        if(catalog==null || vehicle==null || !vehicle.IsPlayable || catalog.Find(vehicle.vehicleId)!=vehicle) return false;
        PlayerPrefs.SetString(PlayerPrefsKey,vehicle.vehicleId); PlayerPrefs.Save(); return true;
    }
}

