using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InstrumentClusterValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Folder = "Logs/HUD";
    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlaying || !File.Exists(Folder + "/render.request")) return;
        File.Delete(Folder + "/render.request"); Run();
    };
    [MenuItem("Stagebound/Validate and render instrument cluster")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode to render isolated HUD previews."); return; }
        Directory.CreateDirectory(Folder);
        try
        {
            Check(InstrumentCluster.LabelStep(240) == 40 && InstrumentCluster.LabelStep(300) == 50, "clean speed intervals");
            Check(InstrumentCluster.GaugeAngle(-20, 240) == 225 && InstrumentCluster.GaugeAngle(400, 240) == -45, "visual range clamp");
            foreach (string name in new[] { "AE86", "SkylineR32", "RX7" })
                foreach (int height in new[] { 1080, 1200 }) Render(name, height);
            File.WriteAllText(Folder + "/validation.txt", "PASS: all three configs, 1920x1080 and 1920x1200 renders; zero/idle/redline/overrange; N/R/1-6; no hierarchy rebuild on update; display config switching. Synthetic UI states, not driven gameplay. " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception e) { File.WriteAllText(Folder + "/validation.txt", "FAIL " + e); Debug.LogException(e); }
    }
    static object Field(object o, string name) => o.GetType().GetField(name, Private).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Render(string vehicle, int height)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject owner = null, root = null;
        RenderTexture texture = null; Texture2D png = null;
        var previous = RenderTexture.active;
        try
        {
            owner = new GameObject("HUD preview bindings"); SceneManager.MoveGameObjectToScene(owner, scene);
            var car = owner.AddComponent<CarController>(); var race = owner.AddComponent<RaceTimer>();
            race.driftScore = owner.AddComponent<DriftScoreSystem>();
            var hud = owner.AddComponent<RaceHUD>(); hud.car = car; Set(hud, "race", race);
            typeof(RaceHUD).GetMethod("EnsureHUD", Private).Invoke(hud, null);
            root = (GameObject)Field(hud, "hudRoot"); SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            var safe = (RectTransform)Field(hud, "safeArea"); safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            var cluster = (InstrumentCluster)Field(hud, "cluster");
            var config = AssetDatabase.LoadAssetAtPath<VehicleConfig>($"Assets/Vehicles/Configs/{vehicle}_VehicleConfig.asset");
            cluster.Configure(config.hudDisplay, config.engine.redlineRPM, vehicle == "SkylineR32" ? "SKYLINE R32" : vehicle);
            Check(cluster.MaxSpeed == config.hudDisplay.HudMaxSpeedKmh && cluster.Redline == config.engine.redlineRPM, "vehicle display values");
            int children = cluster.GetComponentsInChildren<RectTransform>().Length;
            foreach (float rpm in new[] { 0f, config.engine.idleRPM, config.engine.redlineRPM, 20000f })
                foreach (string gear in new[] { "N", "R", "1", "2", "3", "4", "5", "6" }) cluster.Present(400f, rpm, gear, config.engine.redlineRPM, .02f);
            Check(cluster.GetComponentsInChildren<RectTransform>().Length == children, "update rebuilt hierarchy");
            cluster.Configure(new VehicleConfig.HudDisplay { HudMaxSpeedKmh = 200, HudMaxRpm = 8000, RedlineStartRpm = 6800 }, 6800, "SWITCH");
            Check(cluster.MaxSpeed == 200 && cluster.Redline == 6800, "config switch");
            cluster.Configure(config.hudDisplay, config.engine.redlineRPM, vehicle == "SkylineR32" ? "SKYLINE R32" : vehicle);
            cluster.Present(86f, 6200f, "3", config.engine.redlineRPM, 1f);
            ((TMP_Text)Field(hud, "clock")).text = "01:24.382";
            ((TMP_Text)Field(hud, "best")).text = "BEST  02:18.440";
            ((TMP_Text)Field(hud, "delta")).text = "SPLIT  -0.842 s";
            ((TMP_Text)Field(hud, "status")).text = "STAGE IN PROGRESS";
            ((TMP_Text)Field(hud, "checkpoints")).text = "CHECKPOINT  3 / 9";
            var drift = (DriftHUD)Field(hud, "driftHUD");
            var scoring = race.driftScore;
            Set(scoring, "<Telemetry>k__BackingField", owner.GetComponent<DriftTelemetry>());
            Set(scoring.Telemetry, "<VehicleSlipAngle>k__BackingField", 24f);
            race.raceRunning = true;
            Set(scoring, "<ComboActive>k__BackingField", true);
            Set(scoring, "<ActiveComboScore>k__BackingField", 2840f);
            var updateDrift = typeof(DriftHUD).GetMethod("LateUpdate", Private);
            updateDrift.Invoke(drift, null);
            Check(((TMP_Text)Field(drift, "drift")).text.Contains("LIVE"), "drift start display");
            Set(scoring, "<ComboActive>k__BackingField", false);
            Set(scoring, "<LastBankedScore>k__BackingField", 2840f);
            Set(scoring, "<LastBankedAt>k__BackingField", Time.unscaledTime);
            updateDrift.Invoke(drift, null);
            Check(((TMP_Text)Field(drift, "drift")).text.Contains("BANKED"), "drift bank display");
            Set(scoring, "<LastBankedAt>k__BackingField", Time.unscaledTime - 3f);
            updateDrift.Invoke(drift, null);
            Check(((TMP_Text)Field(drift, "drift")).text.Contains("READY"), "drift bank expiry display");
            ((CanvasGroup)Field(drift, "emphasis")).alpha = 1f;
            ((TMP_Text)Field(drift, "drift")).text = "<size=13><color=#5BFFBF>DRIFT / LIVE</color></size>\n<size=40>2,840</size>  <size=24><color=#5BFFBF>×1.7</color></size>\n<size=15><color=#9DAEC1>24° SLIP ANGLE</color></size>";
            ((TMP_Text)Field(drift, "style")).text = "<size=11><color=#9DAEC1>STYLE CREDIT / TIME REDUCTION</color></size>\n-1.420 <size=16>s</size>";
            var camObject = new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(camObject, scene);
            var camera = camObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(33, 43, 51, 255);
            camera.scene = scene;
            texture = new RenderTexture(1920, height, 24); camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            foreach (var arc in root.GetComponentsInChildren<ClusterArc>())
            {
                var mesh = arc.canvasRenderer.GetMesh();
                Check(mesh != null && mesh.vertexCount > 0, "Empty procedural arc mesh: " + arc.name);
            }
            camera.Render(); RenderTexture.active = texture;
            png = new Texture2D(1920, height, TextureFormat.RGB24, false); png.ReadPixels(new Rect(0, 0, 1920, height), 0, 0); png.Apply();
            File.WriteAllBytes($"{Folder}/{vehicle}-{height}.png", png.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (png != null) UnityEngine.Object.DestroyImmediate(png);
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
