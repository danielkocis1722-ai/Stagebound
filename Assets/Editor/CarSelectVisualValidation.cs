using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class CarSelectVisualValidation
{
    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlaying || !File.Exists("Logs/CarSelect/render.request")) return;
        File.Delete("Logs/CarSelect/render.request");
        Run();
    };
    [MenuItem("Stagebound/Render car select previews")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory("Logs/CarSelect");
        var scene = EditorSceneManager.NewPreviewScene();
        var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>("Assets/Vehicles/VehicleCatalog.asset");
        var root = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var camera = new GameObject("UI render", typeof(Camera)).GetComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
        camera.scene = scene;
        camera.cullingMask = 1;
        camera.backgroundColor = new Color32(6, 11, 18, 255);
        camera.clearFlags = CameraClearFlags.SolidColor;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        VehicleDefinition chosen = null;
        var panel = CarSelectPanel.Create(root.transform, catalog, () => { }, v => chosen = v);
        panel.Show();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        for (int i = 0; i < catalog.vehicles.Length; i++)
        {
            typeof(CarSelectPanel).GetField("index", flags).SetValue(panel, i);
            typeof(CarSelectPanel).GetMethod("Refresh", flags).Invoke(panel, null);
            panel.Confirm();
            if (panel.CanSelect && chosen != panel.DisplayedVehicle) throw new Exception("Selection callback mismatch");
            var preview = root.GetComponentInChildren<VehiclePreview>();
            var world = (GameObject)typeof(VehiclePreview).GetField("world", flags).GetValue(preview);
            if (panel.CanSelect && world == null) throw new Exception("Missing 3D model");
            if (world != null)
            {
                SceneManager.MoveGameObjectToScene(world, scene);
                var modelCamera = world.GetComponentInChildren<Camera>();
                modelCamera.scene = scene;
                modelCamera.Render();
            }
            foreach (int height in new[] { 1080, 1200 })
            {
                var rt = new RenderTexture(1920, height, 24);
                camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                typeof(CarSelectPanel).GetMethod("Update", flags).Invoke(panel, null);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = rt;
                var png = new Texture2D(1920, height, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, 1920, height), 0, 0); png.Apply();
                File.WriteAllBytes($"Logs/CarSelect/{panel.DisplayedVehicle.vehicleId}-{height}.png", png.EncodeToPNG());
                RenderTexture.active = null; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(png); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        EditorSceneManager.ClosePreviewScene(scene);
        File.WriteAllText("Logs/CarSelect/validation.txt", "PASS: catalog selection callbacks, 3D previews, 16:9 and 16:10 renders. " + DateTime.UtcNow.ToString("O"));
    }
}
