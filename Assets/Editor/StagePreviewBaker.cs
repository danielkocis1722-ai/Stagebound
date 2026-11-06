using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StagePreviewBaker
{
    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlaying || !File.Exists("Logs/Menus/bake.request")) return;
        File.Delete("Logs/Menus/bake.request"); Bake();
    };

    [MenuItem("Stagebound/Bake stage menu previews")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying) return;
        var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/Stages/StageCatalog.asset");
        Directory.CreateDirectory("Assets/Resources/StagePreviews");
        foreach (var stage in catalog.stages)
        {
            if (stage == null || string.IsNullOrEmpty(stage.scenePath)) continue;
            var scene = EditorSceneManager.OpenPreviewScene(stage.scenePath);
            RenderTexture rt = null;
            Texture2D png = null;
            var previous = RenderTexture.active;
            try
            {
                var roots = scene.GetRootGameObjects();
                var spawn = roots.SelectMany(r => r.GetComponentsInChildren<RespawnManager>(true)).FirstOrDefault();
                var start = spawn != null ? (spawn.startPoint != null ? spawn.startPoint : spawn.transform) : null;
                foreach (var root in roots)
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                    foreach (var car in root.GetComponentsInChildren<CarController>(true)) car.gameObject.SetActive(false);
                }
                var obj = new GameObject("Menu capture", typeof(Camera));
                SceneManager.MoveGameObjectToScene(obj, scene);
                var view = obj.GetComponent<Camera>();
                view.scene = scene;
                view.backgroundColor = new Color32(104, 134, 153, 255);
                view.clearFlags = CameraClearFlags.SolidColor;
                view.fieldOfView = 55;
                view.farClipPlane = 3500;
                view.nearClipPlane = .3f;
                Vector3 origin = start != null ? start.position : Vector3.zero;
                Vector3 forward = start != null ? Vector3.ProjectOnPlane(start.forward, Vector3.up).normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                obj.transform.position = origin - forward * 22 + right * 24 + Vector3.up * 24;
                obj.transform.LookAt(origin + forward * 40 + Vector3.up * 2);
                rt = new RenderTexture(1560, 540, 24);
                view.targetTexture = rt;
                view.Render();
                RenderTexture.active = rt;
                png = new Texture2D(1560, 540, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, 1560, 540), 0, 0); png.Apply();
                File.WriteAllBytes("Assets/Resources/StagePreviews/" + stage.stageId + ".png", png.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (png != null) Object.DestroyImmediate(png);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        AssetDatabase.Refresh();
        foreach (var stage in catalog.stages)
        {
            var importer = AssetImporter.GetAtPath("Assets/Resources/StagePreviews/" + stage.stageId + ".png") as TextureImporter;
            if (importer == null) continue;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Logs/Menus");
        File.WriteAllText("Logs/Menus/bake.txt", "Stage preview images generated from isolated scenes; no scenes saved.");
    }
}
