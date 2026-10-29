using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{

    // References
    [Tooltip("Optional future mixer with MasterVolume/MusicVolume/SFXVolume exposed in dB.")]
    public AudioMixer audioMixer;
    public VehicleCatalog vehicleCatalog;
    public StageCatalog stageCatalog;

    // Runtime UI state
    private GameObject menu;
    private AudioSettingsPanel settings;
    private Button play, carButton, settingsButton;
    private CarSelectPanel cars;
    private StageSelectPanel maps;

    // Menu creation
    private void Start()
    {
        PauseMenu.ResetPauseState();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (audioMixer != null)
            StageboundAudioSettings.UseMixer(audioMixer);
        if (Camera.main == null)
        {
            var cameraObject = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(6, 11, 18, 255);
            camera.cullingMask = 0;
        }

        var canvas = StageboundMenuUI.Canvas(transform, "Stagebound Main Menu");
        var shade = StageboundMenuUI.Panel(canvas.transform, "Background", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color32(6, 11, 18, 255));
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        menu = StageboundMenuUI.Panel(shade, "Menu", new Vector2(.15f, .75f), Vector2.zero, new Vector2(900, 620), new Color(0, 0, 0, 0)).gameObject;
        StageboundMenuUI.Text(menu.transform, "STAGEBOUND", 86, new Vector2(0, 0), new Vector2(900, 115), Color.white);
        StageboundMenuUI.Text(menu.transform, "TIME ATTACK", 20, new Vector2(4, -108), new Vector2(600, 40), StageboundMenuUI.Mint);
        StageboundMenuUI.Panel(menu.transform, "Accent", new Vector2(0, 1), new Vector2(4, -170), new Vector2(54, 3), StageboundMenuUI.Mint);
        play = StageboundMenuUI.Button(menu.transform, "PLAY", new Vector2(0, -215), new Vector2(400, 64), Play);
        carButton = StageboundMenuUI.Button(menu.transform, "CAR SELECT", new Vector2(0, -295), new Vector2(400, 64), ShowCarSelect);
        settingsButton = StageboundMenuUI.Button(menu.transform, "SETTINGS", new Vector2(0, -375), new Vector2(400, 64), ShowSettings);
        StageboundMenuUI.Button(menu.transform, "QUIT", new Vector2(0, -455), new Vector2(400, 64), Quit);
        StageboundMenuUI.Text(menu.transform, "A STAGE. YOUR LINE. YOUR TIME.", 16, new Vector2(4, -565), new Vector2(800, 30), StageboundMenuUI.Muted);
        settings = AudioSettingsPanel.Create(shade, ShowMenu);
        cars = CarSelectPanel.Create(shade, vehicleCatalog, ShowCarMenu, ChooseCar);
        maps = StageSelectPanel.Create(shade, stageCatalog, ShowPlayMenu, StartStage);
        StageboundMenuUI.Select(play);
    }

    // Selection and navigation
    public void Play()
    {
        ShowCarSelect();
    }

    public void ShowCarSelect()
    {
        menu.SetActive(false);
        maps.gameObject.SetActive(false);
        cars.Show();
    }

    private void ShowCarMenu()
    {
        menu.SetActive(true);
        StageboundMenuUI.Select(carButton);
    }

    private void ChooseCar(VehicleDefinition vehicle)
    {
        if (!VehicleSelection.Save(vehicleCatalog, vehicle))
            return;
        cars.gameObject.SetActive(false);
        maps.Show(vehicle);
    }

    private void ShowPlayMenu()
    {
        ShowCarSelect();
    }

    private void StartStage(StageDefinition stage)
    {
        if (stage == null || !stage.IsPlayable)
        {
            maps.ShowError("MAP UNAVAILABLE");
            return;
        }

        if (VehicleSelection.Resolve(vehicleCatalog) == null)
        {
            maps.ShowError("CHOOSE A PLAYABLE CAR FIRST");
            return;
        }

        PlayerPrefs.SetString(StageCatalog.SelectedStageKey, stage.stageId);
        PlayerPrefs.Save();
        StageboundAudioSettings.Save();
        PauseMenu.ResetPauseState();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(stage.BuildIndex);
    }

    public void ShowSettings()
    {
        menu.SetActive(false);
        settings.Show();
    }

    private void ShowMenu()
    {
        menu.SetActive(true);
        StageboundMenuUI.Select(settingsButton);
    }

    public void Quit()
    {
        StageboundAudioSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
