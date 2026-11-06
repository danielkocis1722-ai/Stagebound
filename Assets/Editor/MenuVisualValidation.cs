using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuVisualValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlaying || !File.Exists("Logs/Menus/render.request") || !File.Exists("Logs/Menus/bake.txt")) return;
        File.Delete("Logs/Menus/render.request"); Run();
    };
    [MenuItem("Stagebound/Render menu previews")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory("Logs/Menus");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Menu preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var camera = new GameObject("UI camera", typeof(Camera)).GetComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene); camera.scene = scene;
        camera.cullingMask = 1; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(6, 11, 18, 255);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/Stages/StageCatalog.asset");
            var vehicles = AssetDatabase.LoadAssetAtPath<VehicleCatalog>("Assets/Vehicles/VehicleCatalog.asset");
            StageDefinition selected = null;
            var panel = StageSelectPanel.Create(root.transform, catalog, () => { }, s => selected = s);
            panel.Show(vehicles.Fallback);
            for (int i = 0; i < catalog.stages.Length; i++)
            {
                panel.Highlight(i);
                if (panel.CanStart)
                {
                    panel.Confirm();
                    if (selected != catalog.stages[i]) throw new Exception("Stage callback mismatch");
                    if (Resources.Load<Texture2D>("StagePreviews/" + selected.stageId) == null) throw new Exception("Stage preview missing");
                }
                foreach (int h in new[] { 1080, 1200 }) Capture(root, camera, panel, catalog.stages[i].stageId, h);
            }
            panel.gameObject.SetActive(false);
            var settings = AudioSettingsPanel.Create(root.transform, () => { });
            settings.gameObject.SetActive(true);
            string[] names = { "AUDIO", "WHEEL", "CONTROLS" };
            for (int i = 0; i < 3; i++)
            {
                typeof(AudioSettingsPanel).GetMethod("BuildPage", Private).Invoke(settings, new object[] { i });
                var tabs = (Button[])typeof(AudioSettingsPanel).GetField("tabs", Private).GetValue(settings);
                if (!tabs[i].GetComponentInChildren<TMP_Text>().text.Contains(names[i])) throw new Exception("Settings order mismatch");
                foreach (int h in new[] { 1080, 1200 }) Capture(root, camera, settings, names[i], h);
            }
            File.WriteAllText("Logs/Menus/validation.txt", "PASS: playable stage previews, stage callbacks, Audio/Wheel/Controls order; all stage/settings pages rendered at 16:9 and 16:10. " + DateTime.UtcNow.ToString("O"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(camera.gameObject); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Capture(GameObject root, Camera camera, MonoBehaviour panel, string name, int height)
    {
        var rt = new RenderTexture(1920, height, 24); camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)panel.transform.parent;
        float width = panel is StageSelectPanel ? 1560 : 1140;
        panel.transform.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((rect.rect.width - 64) / width, (rect.rect.height - 64) / 900), .1f, 1);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var png = new Texture2D(1920, height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1920, height), 0, 0); png.Apply();
        File.WriteAllBytes($"Logs/Menus/{name}-{height}.png", png.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(png); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
}
