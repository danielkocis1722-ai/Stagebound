using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// An editor-driven Play Mode integration check. No test framework or runtime
// test component is added to the game. Invoked explicitly from the command line.
public static class ValidateVehicleFlow
{
    private static bool tracksOnly;
    public static void RunTracks() { tracksOnly = true; Run(); }
    private static IEnumerator steps;
    private static readonly StringBuilder log = new StringBuilder();
    private static readonly List<Action> restore = new List<Action>();
    private static bool oldOptionsEnabled;
    private static EnterPlayModeOptions oldOptions;
    private static Gamepad pad;
    private static Keyboard keyboard;
    private static readonly List<InputDevice> disabled = new List<InputDevice>();
    private static int lastFrame = -1;
    private static double deadline;
    private const string Output = "Library/TrackIntegration";

    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new Exception(label);
        log.AppendLine("PASS " + label);
        Flush();
    }

    private static void Flush() => File.WriteAllText(Output + "/playmode.txt", log.ToString());

    private static T Find<T>()
        where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();

    private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void BackupString(string key)
    {
        bool had = PlayerPrefs.HasKey(key);
        string value = PlayerPrefs.GetString(key);
        restore.Add(() =>
        {
            if (had)
                PlayerPrefs.SetString(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        });
    }

    private static void BackupInt(string key)
    {
        bool had = PlayerPrefs.HasKey(key);
        int value = PlayerPrefs.GetInt(key);
        restore.Add(() =>
        {
            if (had)
                PlayerPrefs.SetInt(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        });
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/SampleScene.unity", "Assets/Scenes/TrackCompleCircuit.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            Check(scene.GetRootGameObjects().All(root => root.GetComponentsInChildren<Component>(true).All(c => c != null)), "No missing scripts: " + path);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Vehicles" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab.GetComponentsInChildren<Component>(true).All(c => c != null), "No missing scripts: " + path);
        }
        var rx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vehicles/Visuals/RX7_Visual.prefab");
        Func<string> meshSignature = () => string.Join("\n", rx.GetComponentsInChildren<MeshFilter>(true).Select(f =>
        {
            if (f.sharedMesh == null)
                throw new Exception("Missing RX7 mesh: " + f.name);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f.sharedMesh, out string guid, out long fileId);
            return guid + ":" + fileId + ":" + f.sharedMesh.name + ":" + f.sharedMesh.vertexCount;
        }).OrderBy(s => s));
        string beforeReimport = meshSignature();
        AssetDatabase.ImportAsset("Assets/Models/Cars/RX7_FBX.fbx", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Check(meshSignature() == beforeReimport, "RX7 mesh references survive a forced FBX reimport");
        BackupString(VehicleSelection.PlayerPrefsKey);
        BackupString(StageCatalog.SelectedStageKey);
        var stageCatalog = AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/Stages/StageCatalog.asset");
        // Remove only this utility's reserved records from any previously
        // interrupted run. Never delete the user's real stage records.
        foreach (var stage in stageCatalog.stages.Where(s => s != null && s.IsPlayable))
        {
            foreach (var id in new[]
            {
                "ae86",
                "skyline-r32",
                "rx7-fd"
            }

            )
            {
                string reserved = "Stagebound.Stage.integration-validation-" + stage.stageId + "-" + id + "." + stage.checkpointCount + ".v" + stage.recordVersion + ".style2";
                foreach (var suffix in new[]
                {
                    "",
                    ".cars.v1.index",
                    ".cars.v1.migrated",
                    ".car.ae86",
                    ".car." + id
                }

                )
                    PlayerPrefs.DeleteKey(reserved + suffix);
            }

            string prefix = StageRecords.OverallKey(stage, stage.checkpointCount);
            BackupInt(prefix + ".cars.v1.migrated");
            BackupString(prefix + ".cars.v1.index");
            BackupString(StageRecords.VehicleKey(stage, stage.checkpointCount, "ae86"));
        }

        oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        steps = Tests();
        deadline = EditorApplication.timeSinceStartup + 900;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Finish(new Exception("Play Mode validation timed out."));
            return;
        }

        if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.frameCount == lastFrame)
            return;
        lastFrame = Time.frameCount;
        try
        {
            if (!steps.MoveNext())
                Finish(null);
        }
        catch (Exception e)
        {
            Finish(e);
        }
    }

    private static void Finish(Exception failure)
    {
        EditorApplication.update -= Tick;
        if (failure != null)
        {
            log.AppendLine("FAIL " + failure);
            Debug.LogException(failure);
        }

        Flush();
        if (pad != null)
            InputSystem.RemoveDevice(pad);
        if (keyboard != null)
            InputSystem.RemoveDevice(keyboard);
        foreach (var device in disabled)
            if (device.added)
                InputSystem.EnableDevice(device);
        foreach (var action in restore)
            try
            {
                action();
            }
            catch (Exception e)
            {
                failure = e;
                log.AppendLine("FAIL cleanup " + e);
            }

        PlayerPrefs.Save();
        Flush();
        Time.captureDeltaTime = 0;
        EditorSettings.enterPlayModeOptions = oldOptions;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
        EditorApplication.Exit(failure == null ? 0 : 1);
    }

    private static void GamepadState(GamepadState state) => InputSystem.QueueStateEvent(pad, state);

    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

    private static void Capture(string file)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;
        var preview = Find<VehiclePreview>();
        if (preview != null && preview.image.texture is RenderTexture previewTexture)
            SaveTexture(previewTexture, Output + "/" + file + "-model.png");
        var destination = new RenderTexture(1920, 1080, 24);
        destination.Create();
        var canvas = Find<Canvas>();
        if (canvas != null && Find<MainMenu>() != null)
        {
            var go = new GameObject("Validation capture camera", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, -5000, 0);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31;
            camera.targetTexture = destination;
            var mode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            var layers = canvas.GetComponentsInChildren<Transform>(true).Select(t => (t, t.gameObject.layer)).ToArray();
            foreach (var pair in layers)
                pair.t.gameObject.layer = 31;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = destination });
            SaveTexture(destination, Output + "/" + file + ".png");
            canvas.renderMode = mode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = distance;
            foreach (var pair in layers)
                pair.t.gameObject.layer = pair.layer;
            UnityEngine.Object.DestroyImmediate(go);
            Canvas.ForceUpdateCanvases();
        }
        else if (Camera.main != null)
        {
            RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = destination });
            SaveTexture(destination, Output + "/" + file + ".png");
        }

        destination.Release();
        UnityEngine.Object.DestroyImmediate(destination);
    }

    private static void SaveTexture(RenderTexture source, string path)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = source;
        var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        RenderTexture.active = previous;
    }

    private static IEnumerator CrossTrigger(CarController car, RaceTrigger trigger)
    {
        var body = car.GetComponent<Rigidbody>();
        var box = trigger.GetComponent<BoxCollider>();
        var center = box.transform.TransformPoint(box.center);
        var direction = trigger.transform.forward;
        for (int i = 0; i <= 44; i++)
        {
            body.position = center - Vector3.up * 1.25f + direction * (-11 + i * .5f);
            body.rotation = trigger.transform.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return null;
        }

        for (int i = 0; i < 4; i++)
            yield return null;
    }

    private static IEnumerator CheckPhysicalTriggers(CarController car, RaceTimer timer, RespawnManager respawn, string stageId, string vehicleId)
    {
        bool enabled = car.enabled;
        var body = car.GetComponent<Rigidbody>();
        bool gravity = body.useGravity;
        GamepadState(default);
        Keys();
        car.enabled = false;
        body.useGravity = false;
        respawn.RestartStage();
        for (int i = 0; i < 8; i++)
            yield return null;
        var triggers = UnityEngine.Object.FindObjectsByType<RaceTrigger>();
        bool pointToPoint = timer.stageDefinition.stageType == StageDefinition.StageType.PointToPoint;
        var line = triggers.Single(t => t.triggerType == (pointToPoint ? RaceTrigger.TriggerType.Start : RaceTrigger.TriggerType.StartFinish));
        var finish = pointToPoint ? triggers.Single(t => t.triggerType == RaceTrigger.TriggerType.Finish) : line;
        var checkpoints = triggers.Where(t => t.triggerType == RaceTrigger.TriggerType.Checkpoint1).OrderBy(t => t.checkpointIndex).ToArray();
        var crossing = CrossTrigger(car, line);
        while (crossing.MoveNext())
            yield return crossing.Current;
        Check(timer.raceRunning && timer.PassedCheckpoints == 0, "Physical start line starts timer: " + stageId + " / " + vehicleId);
        crossing = CrossTrigger(car, finish);
        while (crossing.MoveNext())
            yield return crossing.Current;
        Check(timer.raceRunning && timer.LastResult == null, "Physical finish rejected without checkpoints");
        if (checkpoints.Length > 1)
        {
            crossing = CrossTrigger(car, checkpoints[checkpoints.Length - 1]);
            while (crossing.MoveNext())
                yield return crossing.Current;
            Check(timer.PassedCheckpoints == 0, "Out-of-order physical checkpoint rejected");
        }

        for (int i = 0; i < checkpoints.Length; i++)
        {
            crossing = CrossTrigger(car, checkpoints[i]);
            while (crossing.MoveNext())
                yield return crossing.Current;
            Check(timer.PassedCheckpoints == i + 1, "Physical checkpoint " + (i + 1) + " accepted in order");
            if (i == 0)
            {
                Keys(Key.R);
                yield return null;
                yield return null;
                Keys();
                yield return null;
                var expected = checkpoints[i].respawnPoint;
                if (expected != null)
                    Check(Vector3.Distance(body.position, expected.position) < .35f, "R respawns at latest checkpoint");
                Check(timer.raceRunning && timer.PassedCheckpoints == 1, "Checkpoint respawn retains race progress");
                for (int f = 0; f < 8; f++)
                    yield return null;
            }
        }

        crossing = CrossTrigger(car, finish);
        while (crossing.MoveNext())
            yield return crossing.Current;
        Check(timer.LastResult != null && timer.raceRunning != pointToPoint && timer.PassedCheckpoints == (pointToPoint ? checkpoints.Length : 0), "Physical finish follows stage type: " + stageId);
        Check(StageRecords.ForVehicle(timer.stageDefinition, timer.TotalCheckpoints, vehicleId).hasBest, "Finish saves isolated per-stage/per-car record");
        body.useGravity = gravity;
        car.enabled = enabled;
        respawn.RestartStage();
        yield return null;
    }

    private static bool RoadHit(Vector3 position, out RaycastHit hit)
    {
        hit = default;
        bool found = false;
        float closest = float.PositiveInfinity;
        foreach (var sample in Physics.RaycastAll(position + Vector3.up * 90, Vector3.down, 180, ~0, QueryTriggerInteraction.Ignore))
        {
            if (sample.collider.transform.parent == null || sample.collider.transform.parent.name != "Road")
                continue;
            float difference = Mathf.Abs(sample.point.y - position.y);
            if (difference < closest)
            {
                closest = difference;
                hit = sample;
                found = true;
            }
        }

        return found && closest < 3;
    }

    private static IEnumerator CheckTrackSurfaces(CarController car, RespawnManager respawn, VehicleDefinition definition)
    {
        var guide = GameObject.Find("Route Guide").transform;
        Check(guide.childCount > 200, "Complete track surface guide");
        var wheelConfigs = new[]
        {
            definition.config.frontLeft,
            definition.config.frontRight,
            definition.config.backLeft,
            definition.config.backRight
        };
        int probes = 0;
        for (int i = 0; i < guide.childCount; i++)
        {
            var point = guide.GetChild(i);
            foreach (var wheel in wheelConfigs)
            {
                var footprint = point.position + point.rotation * new Vector3(wheel.localPosition.x, 0, wheel.localPosition.z);
                if (!RoadHit(footprint, out var hit))
                    throw new Exception("Unsupported wheel footprint on " + point.name + " / " + definition.vehicleId + " at " + footprint);
                if (hit.normal.y < .55f)
                    throw new Exception("Unsafe driving-surface normal on " + point.name);
                probes++;
            }
        }

        Check(probes == guide.childCount * 4, "All " + probes + " wheel-footprint probes have real road support: " + definition.vehicleId);
        var physical = GameObject.Find("Track Collisions").GetComponentsInChildren<Collider>();
        Check(physical.All(c => c.attachedRigidbody == null && !c.isTrigger && c.transform.lossyScale == Vector3.one), "All track colliders are static, non-trigger and positively scaled");
        var rb = car.GetComponent<Rigidbody>();
        bool enabled = car.enabled;
        car.enabled = false;
        GamepadState(default);
        var representatives = guide.Cast<Transform>().GroupBy(t => t.name.Substring(5)).Select(g => g.ElementAt(g.Count() / 2)).ToArray();
        var wheels = new[]
        {
            car.frontLeft,
            car.frontRight,
            car.backLeft,
            car.backRight
        };
        foreach (var point in representatives)
        {
            rb.position = point.position + Vector3.up * 1.25f;
            rb.rotation = point.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            foreach (var wheel in wheels)
            {
                wheel.motorTorque = 0;
                wheel.brakeTorque = 0;
            }

            Physics.SyncTransforms();
            for (int f = 0; f < 38; f++)
                yield return null;
            int grounded = wheels.Count(w => w.isGrounded);
            Check(grounded >= 2 && rb.position.y > point.position.y - 2, "Suspension stays above road on " + point.name + " / " + definition.vehicleId + " grounded=" + grounded);
            if (point.name.EndsWith("Jump Connector", StringComparison.Ordinal))
                Capture("track-comple-" + definition.vehicleId + "-crossing");
        }

        respawn.RestartStage();
        for (int f = 0; f < 80; f++)
            yield return null;
        var fenceObject = GameObject.Find("fence.009 Box");
        if (fenceObject == null)
        {
            RaycastHit barrier = default;
            Transform station = null;
            Vector3 push = Vector3.zero;
            foreach (Transform point in guide)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 direction = point.right * side;
                    var hits = Physics.RaycastAll(point.position + Vector3.up * .8f, direction, 25, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var hit in hits.OrderBy(h => h.distance))
                        if (hit.collider.attachedRigidbody == null && Mathf.Abs(hit.normal.y) < .5f && hit.distance > 2)
                        { barrier = hit; station = point; push = direction; break; }
                    if (station != null) break;
                }
                if (station != null) break;
            }
            Check(station != null, "Visible roadside solid has collision: " + definition.vehicleId);
            rb.position = station.position + push * Mathf.Max(0, barrier.distance - 2.5f) + Vector3.up * 1.25f;
            rb.rotation = station.rotation;
            rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            for (int f = 0; f < 40; f++) yield return null;
            float beforeWall = Vector3.Dot(barrier.point - rb.position, push);
            // An initial sideways impact tests the actual chassis/mesh contact.
            // No ongoing velocity overwrite or production recovery force is used.
            rb.linearVelocity = push * 12;
            for (int f = 0; f < 75; f++) yield return null;
            float afterWall = Vector3.Dot(barrier.point - rb.position, push);
            Check(afterWall >= -.5f && afterWall < beforeWall - .2f, "Roadside solid stops physical chassis impact: " + definition.vehicleId);
            car.enabled = enabled;
            respawn.RestartStage();
            yield break;
        }
        var fence = fenceObject.GetComponent<BoxCollider>();
        rb.position = new Vector3(fence.bounds.center.x + 6, respawn.startPoint.position.y, fence.bounds.center.z);
        rb.rotation = Quaternion.identity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        for (int f = 0; f < 50; f++)
        {
            rb.linearVelocity = Vector3.left * 8;
            yield return null;
        }

        Check(rb.position.x >= fence.bounds.min.x && rb.position.x < fence.bounds.max.x + 3, "Visible trackside fence blocks controlled car push: " + definition.vehicleId);
        car.enabled = enabled;
        respawn.RestartStage();
        yield return null;
    }

    private static IEnumerator ReverseCases(CarController car, RespawnManager respawn, string vehicle)
    {
        // Isolated native-physics fixture: pose/velocity are seeded only at the
        // beginning of each case; recovery itself uses the normal driving inputs.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Reverse test floor";
        floor.transform.position = new Vector3(0, -1001, 0);
        floor.transform.localScale = new Vector3(600, 2, 600);
        var body = car.GetComponent<Rigidbody>();
        var wheels = new[] { car.frontLeft, car.frontRight, car.backLeft, car.backRight };
        for (int test = 0; test < 5; test++)
        {
            car.enabled = false;
            GamepadState(default);
            body.position = new Vector3(0, -998.75f, 0);
            body.rotation = test == 4 ? Quaternion.Euler(0, 150, 0) : Quaternion.identity;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            foreach (var wheel in wheels) { wheel.motorTorque = 0; wheel.brakeTorque = 0; }
            Physics.SyncTransforms();
            for (int i = 0; i < 90; i++) yield return null;
            car.currentGear = test == 0 || test == 4 ? 1 : 0;
            car.enabled = true;
            body.linearVelocity = test == 4 ? Vector3.forward * 12 : Vector3.back * (test == 0 ? 3 : test == 1 ? 8 : 12);
            if (test == 4) body.angularVelocity = Vector3.up;
            Quaternion initial = body.rotation;
            var input = new GamepadState { rightTrigger = test == 0 || test == 4 ? 1 : 0, leftStick = test >= 1 && test <= 3 ? new Vector2(.85f, 0) : Vector2.zero };
            if (test == 2) input = input.WithButton(GamepadButton.South);
            GamepadState(input);
            float peakDrive = 0, yaw = 0, minimumEngine = float.PositiveInfinity;
            if (test == 3)
            {
                // A test driver turns first, then pulses the handbrake briefly.
                // Sustained lock from rest-of-input in case C simply stops the car.
                for (int i = 0; i < 360 && Quaternion.Angle(initial, body.rotation) < 140; i++) yield return null;
                yaw = Quaternion.Angle(initial, body.rotation);
                GamepadState(input.WithButton(GamepadButton.South));
                for (int i = 0; i < 15; i++) yield return null;
                car.currentGear = 1;
                GamepadState(new GamepadState { rightTrigger = 1 });
            }
            for (int i = 0; i < (test == 0 || test >= 3 ? 360 : 180); i++)
            {
                yield return null;
                peakDrive = Mathf.Max(peakDrive, car.DeliveredAxleTorque);
                minimumEngine = Mathf.Min(minimumEngine, car.EngineRPM);
                yaw = Mathf.Max(yaw, Quaternion.Angle(initial, body.rotation));
                if (car.currentGear == 1) CheckMotorDirection(car);
            }
            string label = test == 0 ? "A backward-to-forward" : test == 1 ? "B reverse steering" : test == 2 ? "C reverse handbrake" : test == 3 ? "D reverse J-turn + powered recovery" : "E backward spin recovery";
            log.AppendLine($"MEASURE {vehicle} {label}: forward={car.SignedLongitudinalSpeedKmh:F2} km/h, yaw={yaw:F2} deg, peak forward axle={peakDrive:F1} Nm, min engine={minimumEngine:F1} RPM");
            Check(test == 0 || test >= 3 ? car.SignedLongitudinalSpeedKmh > 2 && peakDrive > 0 && minimumEngine > 0 && (test != 3 || yaw >= 120) : yaw > 3, vehicle + " " + label);
        }
        UnityEngine.Object.Destroy(floor);
        GamepadState(default);
        respawn.RestartStage();
        for (int i = 0; i < 90; i++) yield return null;
    }

    private static void CheckMotorDirection(CarController car)
    {
        if (car.backLeft.motorTorque < -.01f || car.backRight.motorTorque < -.01f)
            throw new Exception("First gear reversed motor torque while wheels were rotating backward.");
    }

    private static IEnumerator Tests()
    {
        for (int i = 0; i < 15; i++)
            yield return null;
        Time.captureDeltaTime = 1f / 60f;
        // Batch editors have no focused Game View. Route synthetic keyboard
        // input to this test process without changing the project's settings.
        var originalInputSettings = InputSystem.settings;
        var originalInputFlags = originalInputSettings.hideFlags;
        string originalInputJson = JsonUtility.ToJson(originalInputSettings);
        originalInputSettings.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        var testInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
        testInputSettings.hideFlags = HideFlags.HideAndDontSave;
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = testInputSettings;
        restore.Add(() =>
        {
            if (originalInputSettings == null)
            {
                originalInputSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(originalInputJson, originalInputSettings);
            }

            originalInputSettings.hideFlags = originalInputFlags;
            InputSystem.settings = originalInputSettings;
            UnityEngine.Object.DestroyImmediate(testInputSettings);
        });
        foreach (var d in InputSystem.devices.ToArray())
            if (d is Gamepad || d is Keyboard)
            {
                disabled.Add(d);
                InputSystem.DisableDevice(d);
            }

        keyboard = InputSystem.AddDevice<Keyboard>();
        pad = InputSystem.AddDevice<Gamepad>();
        Check(Find<MainMenu>() != null, "Launch opens Main Menu");
        var menu = Find<MainMenu>();
        var catalog = menu.vehicleCatalog;
        var availableStages = menu.stageCatalog.stages.Where(s => s != null && s.IsPlayable).ToArray();
        Check(availableStages.Length == 4, "All four real stages are available");
        Check(availableStages.Select(s => s.stageId).Distinct().Count() == availableStages.Length, "Unique stable stage IDs");
        Check(availableStages.Select(s => StageRecords.OverallKey(s, s.checkpointCount)).Distinct().Count() == availableStages.Length, "Separate overall stage record keys");
        Check(availableStages.Select(s => StageRecords.VehicleKey(s, s.checkpointCount, "ae86")).Distinct().Count() == availableStages.Length, "Separate per-car stage record keys");
        Check(catalog.vehicles.Length == 3 && catalog.vehicles.All(v => v.IsPlayable), "All three definitions have valid configs and visual references");
        Check(catalog.IsValid(out _), "Unique stable vehicle IDs");
        var configBefore = EditorJsonUtility.ToJson(catalog.defaultVehicle.config);
        for (int combination = 0; combination < catalog.vehicles.Length * availableStages.Length; combination++)
        {
            int vehicleIndex = combination % catalog.vehicles.Length;
            var expectedStage = availableStages[combination / catalog.vehicles.Length];
            log.AppendLine("CASE " + expectedStage.stageId + " / " + catalog.vehicles[vehicleIndex].vehicleId);
            menu = Find<MainMenu>();
            menu.Play();
            yield return null;
            var cars = Find<CarSelectPanel>();
            Check(cars != null && cars.isActiveAndEnabled, "PLAY opens Car Select");
            while (cars.DisplayedVehicle != catalog.vehicles[vehicleIndex])
                cars.ChangeVehicle(1);
            var definition = cars.DisplayedVehicle;
            // Shoulder input must browse while the Continue footer has focus.
            GamepadState(new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return null;
            yield return null;
            Check(cars.DisplayedVehicle != definition, "R1 browses vehicles");
            GamepadState(default);
            yield return null;
            GamepadState(new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return null;
            yield return null;
            Check(cars.DisplayedVehicle == definition, "L1 returns to vehicle");
            GamepadState(default);
            yield return null;
            // Exercise the same move event delivered by keyboard/D-pad navigation.
            var move = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Right
            };
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, move, ExecuteEvents.moveHandler);
            Check(cars.DisplayedVehicle != definition, "Right navigation browses on focused footer");
            cars.ChangeVehicle(-1);
            for (int i = 0; i < 5; i++)
                yield return null;
            Capture(definition.vehicleId + "-select");
            yield return null;
            var preview = Find<VehiclePreview>();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Check(preview != null && preview.image.enabled && preview.image.texture != null, "3D visual preview renders " + definition.vehicleId);
            if (vehicleIndex == 0)
            {
                ExecuteEvents.Execute(Field<Button>(cars, "select").gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            }
            else if (vehicleIndex == 1)
                GamepadState(new GamepadState().WithButton(GamepadButton.South));
            else
                Keys(Key.Enter);
            yield return null;
            yield return null;
            GamepadState(default);
            Keys();
            yield return null;
            var stages = Find<StageSelectPanel>();
            Check(stages != null && stages.CanStart, "Car confirmation opens Stage Select");
            Check(Find<CarController>() == null, "Car confirmation does not load race");
            stages.Highlight(Array.IndexOf(menu.stageCatalog.stages, expectedStage));
            var stage = stages.DisplayedStage;
            Check(stage == expectedStage, "Selected stage " + expectedStage.stageId);
            int unavailable = Array.FindIndex(menu.stageCatalog.stages, s => s != null && !s.IsPlayable);
            if (unavailable >= 0)
            {
                stages.Highlight(unavailable);
                Check(!stages.CanStart, "Coming-soon stage cannot start");
                stages.Confirm();
                yield return null;
                Check(Find<MainMenu>() != null, "Unavailable stage does not load a scene");
                stages.Highlight(Array.IndexOf(menu.stageCatalog.stages, stage));
            }

            Check(PlayerPrefs.GetString(VehicleSelection.PlayerPrefsKey) == definition.vehicleId, "Vehicle selection persists by ID");
            // Back through both screens, then confirm again.
            GamepadState(new GamepadState().WithButton(GamepadButton.East));
            yield return null;
            yield return null;
            GamepadState(default);
            yield return null;
            cars = Find<CarSelectPanel>();
            Check(cars != null && cars.DisplayedVehicle == definition, "Circle / Stage Back restores selected car");
            Keys(Key.Enter);
            yield return null;
            yield return null;
            Keys();
            yield return null;
            stages = Find<StageSelectPanel>();
            Check(stages != null, "Keyboard Enter confirms selected car");
            stages.Highlight(Array.IndexOf(menu.stageCatalog.stages, expectedStage));
            StageboundMenuUI.Select(Field<List<Button>>(stages, "rows")[Array.IndexOf(menu.stageCatalog.stages, expectedStage)]);
            for (int i = 0; i < 5; i++)
                yield return null;
            Capture(stage.stageId + "-stage-select");
            yield return null;
            Field<Button>(stages, "start").Select();
            Keys(Key.Enter);
            yield return null;
            yield return null;
            Keys();
            // Let the elevated spawn settle before comparing rendered wheel
            // poses with the physics solver's most recent contact poses.
            for (int i = 0; i < 80; i++)
                yield return null;
            var car = Find<CarController>();
            var loader = car != null ? car.GetComponent<VehicleRigLoader>() : null;
            Check(car != null && loader != null && loader.Initialized && loader.CurrentDefinition == definition, "Race initializes selected " + definition.vehicleId);
            Check(PlayerPrefs.GetString(StageCatalog.SelectedStageKey) == stage.stageId, "Stage selection persists by ID");
            Check(UnityEngine.Object.FindObjectsByType<CarController>().Length == 1, "Exactly one playable controller");
            Check(UnityEngine.Object.FindObjectsByType<VehicleVisualSetup>().Length == 1, "Exactly one active vehicle visual");
            var config = definition.config;
            var rb = car.GetComponent<Rigidbody>();
            log.AppendLine($"MEASURE {definition.vehicleId} inertia={rb.inertiaTensor:F3} tensorRotation={rb.inertiaTensorRotation.eulerAngles:F3} wheelbase={car.SteeringWheelbase:F3} frontTrack={car.SteeringFrontTrack:F3}");
            Check(Mathf.Approximately(rb.mass, config.chassis.mass) && Mathf.Approximately(car.maxEngineTorque, config.engine.maxEngineTorque), "Per-car mass and torque applied");
            var wheels = new[]
            {
                car.frontLeft,
                car.frontRight,
                car.backLeft,
                car.backRight
            };
            var visuals = new[]
            {
                car.frontLeftWheel,
                car.frontRightWheel,
                car.backLeftWheel,
                car.backRightWheel
            };
            var configs = new[]
            {
                config.frontLeft,
                config.frontRight,
                config.backLeft,
                config.backRight
            };
            for (int w = 0; w < 4; w++)
            {
                Check(Vector3.Distance(wheels[w].transform.localPosition, configs[w].localPosition) < .001f && Mathf.Abs(wheels[w].radius - configs[w].radius) < .001f, "Wheel geometry " + definition.vehicleId + " " + w);
                wheels[w].GetWorldPose(out var pos, out var rot);
                Check(Vector3.Distance(visuals[w].position, pos) < .03f, "Wheel visual follows suspension " + w + " error=" + Vector3.Distance(visuals[w].position, pos).ToString("F4"));
            }

            var respawn = car.GetComponent<RespawnManager>();
            Check(respawn.startPoint != null && Vector3.Distance(rb.position, respawn.startPoint.position) < 3, "Uses authoritative spawn");
            var camera = Find<FollowCamera>();
            Check(camera != null && camera.target == car.transform && camera.carRigidbody == rb, "Camera follows shared CarRoot");
            var timer = Find<RaceTimer>();
            Check(timer.TrackReady && timer.stageDefinition == stage && timer.TotalCheckpoints == stage.checkpointCount, stage.stageId + " start/checkpoints ready");
            Check(car.GetComponent<DriftTelemetry>() != null && car.GetComponent<DriftScoreSystem>().raceTimer == timer, "Drift telemetry and scoring bound to race");
            var audio = car.GetComponent<VehicleAudio>();
            Check(audio != null && audio.engineLayers.Any(l => l != null && l.clip != null) && audio.screechClip != null, "Engine and screech clips assigned");
            var effects = car.GetComponent<WheelEffects>();
            Check(effects != null && effects.wheels.Length == 4 && effects.wheels.All(w => w.collider != null && w.smoke != null), "Smoke and skidmark wheel bindings");
            // Let the selected geometry settle onto the real South Loop surface.
            GamepadState(default);
            for (int i = 0; i < 100; i++)
                yield return null;
            int grounded = wheels.Count(w => w.isGrounded);
            Check(grounded == 4, "All four wheels settle on " + stage.stageId + ": " + definition.vehicleId + " (" + grounded + ")");
            Capture(stage.stageId + "-" + definition.vehicleId + "-race");
            yield return null;
            Quaternion restWheel = visuals[2].rotation;
            Vector3 initial = rb.position;
            GamepadState(new GamepadState { rightTrigger = 1 });
            for (int i = 0; i < 150; i++)
                yield return null;
            Check(car.ThrottleInput > .8f && car.EngineRPM > car.idleRPM && Vector3.Distance(rb.position, initial) > .5f, "Engine accelerates " + definition.vehicleId);
            Check(Quaternion.Angle(restWheel, visuals[2].rotation) > 1, "Wheel meshes rotate");
            GamepadState(new GamepadState { rightTrigger = .5f, leftStick = new Vector2(.6f, 0) });
            for (int i = 0; i < 12; i++)
                yield return null;
            Check(car.frontLeft.steerAngle > 0 && car.frontRight.steerAngle > 0, "Front wheels steer");
            GamepadState(new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return null;
            yield return null;
            Check(car.currentGear == 2, "Gearbox upshift");
            GamepadState(default);
            yield return null;
            GamepadState(new GamepadState().WithButton(GamepadButton.South));
            for (int i = 0; i < 8; i++)
                yield return null;
            Check(car.HandbrakeInput > .9f && car.backLeft.brakeTorque > 0, "Handbrake reaches rear wheels");
            GamepadState(new GamepadState().WithButton(GamepadButton.West));
            for (int i = 0; i < 8; i++)
                yield return null;
            Check(car.ClutchInput > .9f, "Manual clutch input");
            GamepadState(new GamepadState { leftTrigger = 1 });
            for (int i = 0; i < 10; i++)
                yield return null;
            var lights = car.GetComponent<BrakeLights>();
            Check(lights.enabled == (loader.CurrentVisual.brakeLightMaterial != null), "Brake-light availability matches verified geometry");
            if (lights.enabled)
            {
                var lamp = lights.leftRear;
                var properties = new MaterialPropertyBlock();
                lamp.renderer.GetPropertyBlock(properties, lamp.materialIndex);
                Check(lamp.runtimeMaterial != null && lamp.runtimeMaterial.IsKeywordEnabled("_EMISSION") && properties.GetColor("_EmissionColor").maxColorComponent > .1f, "Brake pedal emits on dedicated rear-light slot");
                foreach (var renderer in loader.CurrentVisual.GetComponentsInChildren<Renderer>())
                    foreach (var material in renderer.sharedMaterials)
                        if (material != lamp.runtimeMaterial && material.HasProperty("_EmissionColor"))
                            Check(material.GetColor("_EmissionColor").maxColorComponent < .001f, "Non-brake material stays dark: " + material.name);
                Capture(definition.vehicleId + "-braking");
            }

            GamepadState(default);
            yield return null;
            // Isolate result records from the user's actual times.
            var testStage = UnityEngine.Object.Instantiate(stage);
            testStage.stageId = "integration-validation-" + stage.stageId + "-" + definition.vehicleId;
            string testPrefix = StageRecords.OverallKey(testStage, stage.checkpointCount);
            foreach (string suffix in new[]
            {
                "",
                ".cars.v1.index",
                ".car.ae86",
                ".car." + definition.vehicleId
            }

            )
            {
                string key = testPrefix + suffix;
                BackupString(key);
            }

            BackupInt(testPrefix + ".cars.v1.migrated");
            timer.stageDefinition = testStage;
            timer.SendMessage("Start");
            respawn.RestartStage();
            for (int i = 0; i < 100; i++)
                yield return null;
            timer.StartRace();
            var telemetry = car.GetComponent<DriftTelemetry>();
            var scoring = car.GetComponent<DriftScoreSystem>();
            var marks = car.GetComponent<SkidmarkSystem>();
            marks.Clear();
            bool sawDrift = false, sawSmoke = false, sawScreech = false;
            // Controlled velocity excites actual WheelCollider slip on the real
            // road; the telemetry, effects and scoring paths stay unmodified.
            for (int i = 0; i < 50; i++)
            {
                rb.linearVelocity = car.transform.forward * 14 + car.transform.right * 8;
                rb.angularVelocity = Vector3.zero;
                GamepadState(new GamepadState { rightTrigger = .5f });
                yield return null;
                sawDrift |= telemetry.IsDrifting;
                sawSmoke |= effects.wheels.Any(w => w.emissionRate > 0);
                sawScreech |= audio.screechSource != null && audio.screechSource.volume > .001f;
            }

            Check(sawDrift, "DriftTelemetry detects real rear slip: " + definition.vehicleId);
            Check(scoring.TotalDriftScore > 0 && scoring.StyleCredit > 0, "DriftScore earns Style Credit: " + definition.vehicleId);
            Check(sawSmoke && sawScreech && Field<int>(marks, "nextSegment") > 0, "Live slip drives smoke, screech gain and skidmarks: " + definition.vehicleId);
            GamepadState(default);
            timer.elapsedTime = 30;
            for (int cp = 1; cp <= timer.TotalCheckpoints; cp++)
                timer.PassCheckpoint(cp);
            Check(timer.CrossStartFinish() && timer.LastResult != null && timer.raceRunning == (stage.stageType == StageDefinition.StageType.Circuit), "Finish follows configured circuit / point-to-point flow");
            Check(Mathf.Abs(timer.StyleCreditFor(16000) - 8) < .001f, "Style Credit uses existing eight-second cap");
            var triggerChecks = CheckPhysicalTriggers(car, timer, respawn, stage.stageId, definition.vehicleId);
            while (triggerChecks.MoveNext())
                yield return triggerChecks.Current;
            if (stage.stageId != "south-loop")
            {
                var routeChecks = CheckTrackSurfaces(car, respawn, definition);
                while (routeChecks.MoveNext())
                    yield return routeChecks.Current;
            }

            var pause = car.GetComponent<PauseMenu>();
            if (!tracksOnly && stage.stageId == "south-loop")
            {
                var reverse = ReverseCases(car, respawn, definition.vehicleId);
                while (reverse.MoveNext()) yield return reverse.Current;
            }
            pause.Pause();
            Check(PauseMenu.IsPaused && Time.timeScale == 0, "Pause works");
            pause.RestartStage();
            yield return null;
            Check(!PauseMenu.IsPaused && Time.timeScale == 1 && Vector3.Distance(rb.position, respawn.startPoint.position) < .3f, "Restart uses SpawnPoint");
            pause.Pause();
            pause.QuitToMainMenu();
            for (int i = 0; i < 10; i++)
                yield return null;
            Check(Find<MainMenu>() != null && Time.timeScale == 1, "Pause returns to Main Menu");
            menu = Find<MainMenu>();
            menu.Play();
            yield return null;
            cars = Find<CarSelectPanel>();
            Check(cars.DisplayedVehicle == definition, "PLAY again remembers " + definition.vehicleId);
            cars.Confirm();
            yield return null;
            var rememberedStage = Find<StageSelectPanel>();
            Check(rememberedStage.DisplayedStage == stage, "PLAY again remembers " + stage.stageId);
            rememberedStage.Back();
            yield return null;
            Keys(Key.Escape);
            yield return null;
            yield return null;
            Keys();
            yield return null;
            Check(!cars.gameObject.activeSelf, "Escape returns to main buttons");
        }

        Check(EditorJsonUtility.ToJson(catalog.defaultVehicle.config) == configBefore, "AE86 config unchanged by all runtime selections");
        log.AppendLine("PASS: all playable vehicle/stage combinations, physical triggers, route support and menu/race integration. Synthetic inputs, not a physical controller road test.");
    }
}
