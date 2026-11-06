using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TireEffectsValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists("Logs/Tires/validate.request") || EditorApplication.isPlaying) return;
        File.Delete("Logs/Tires/validate.request");
        Run();
    };

    [MenuItem("Stagebound/Validate tire effects")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory("Logs/Tires");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            Check(WheelEffects.SlipIntensity(.1f, .2f, .85f) == 0, "grip remains clean");
            Check(WheelEffects.SlipIntensity(-1f, .2f, .85f) == 1, "signed lockup/burnout slip");
            Check(WheelEffects.LoadFactor(0, 3000) == 0, "unloaded contact suppressed");
            Check(WheelEffects.LoadFactor(750, 3000) < WheelEffects.LoadFactor(3000, 3000), "load response");
            var road = new GameObject("Sloped validation road", typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(road, scene);
            road.transform.rotation = Quaternion.Euler(12, 0, 8);
            var surface = road.GetComponent<BoxCollider>();
            surface.size = new Vector3(10, .2f, 10);
            var owner = new GameObject("Tire mesh validation");
            SceneManager.MoveGameObjectToScene(owner, scene);
            var marks = owner.AddComponent<SkidmarkSystem>();
            marks.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Effects/VehicleFeedback/Skidmarks.mat");
            marks.maxSegments = 64;
            typeof(SkidmarkSystem).GetMethod("Awake", Private).Invoke(marks, null);
            Physics.SyncTransforms();
            Vector3 normal = road.transform.up;
            Vector3 from = road.transform.TransformPoint(new Vector3(0, .1f, 0));
            Vector3 to = road.transform.TransformPoint(new Vector3(0, .1f, 1));
            Check(marks.AddSegment(from, to, normal, normal, .3f, 1f, .2f, surface), "continuous ribbon");
            var vertices = (Vector3[])typeof(SkidmarkSystem).GetField("vertices", Private).GetValue(marks);
            for (int i = 0; i < 16; i++)
                Check(Mathf.Abs(road.transform.InverseTransformPoint(vertices[i]).y - .103f) < .0015f, "road-conforming vertices");
            Check(!marks.AddSegment(from, from + Vector3.forward * 20, normal, normal, 1, 1, .2f, surface), "teleport break");
            for (int i = 0; i < 100; i++) marks.AddSegment(from, to, normal, normal, 1, 1, .2f, surface);
            Check(ReferenceEquals(vertices, typeof(SkidmarkSystem).GetField("vertices", Private).GetValue(marks)), "buffer reused");
            Check((int)typeof(SkidmarkSystem).GetField("nextSegment", Private).GetValue(marks) < 64, "bounded ring");
            Check(marks.AddSegment(from - road.transform.forward * .1f, from + road.transform.forward * .1f, normal, normal, 1, 1, .2f, surface), "stationary burnout patch");
            File.WriteAllText("Logs/Tires/validation.txt", "PASS: slip/load response, slope projection, continuous ribbons, stationary contact patch, teleport rejection, bounded reusable mesh. " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception e)
        {
            File.WriteAllText("Logs/Tires/validation.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void Check(bool success, string message)
    {
        if (!success) throw new Exception(message);
    }
}
