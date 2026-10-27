using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AudioSettingsPanel : MonoBehaviour
{
    Action back;
    Slider master;
    public static AudioSettingsPanel Create(Transform parent, Action onBack)
    {
        var root = StageboundMenuUI.Panel(parent, "Audio Settings", new Vector2(.5f,.5f), Vector2.zero,
            new Vector2(640,500), new Color32(10,18,29,245));
        var panel = root.gameObject.AddComponent<AudioSettingsPanel>(); panel.back = onBack;
        StageboundMenuUI.Text(root,"SETTINGS",30,new Vector2(36,-26),new Vector2(560,50),Color.white);
        panel.master = panel.Row("MASTER VOLUME",110,StageboundAudioSettings.Master,StageboundAudioSettings.SetMaster,true);
        panel.Row("MUSIC  /  COMING LATER",210,StageboundAudioSettings.Music,StageboundAudioSettings.SetMusic,false);
        panel.Row("SFX VOLUME",310,StageboundAudioSettings.Sfx,StageboundAudioSettings.SetSfx,true);
        StageboundMenuUI.Button(root,"BACK",new Vector2(36,-420),new Vector2(220,50),panel.Back);
        root.gameObject.SetActive(false); return panel;
    }
    Slider Row(string title,float y,float value,Action<float> setter,bool enabled)
    {
        StageboundMenuUI.Text(transform,title,18,new Vector2(36,-y),new Vector2(480,32),StageboundMenuUI.Muted);
        var number = StageboundMenuUI.Text(transform,$"{value:P0}",18,new Vector2(520,-y),new Vector2(90,32),StageboundMenuUI.Mint);
        var bar = StageboundMenuUI.Panel(transform,title+" Slider",new Vector2(0,1),new Vector2(36,-y-42),new Vector2(560,22),Color.clear);
        bar.GetComponent<Image>().raycastTarget = enabled;
        var slider = bar.gameObject.AddComponent<Slider>(); slider.minValue=0; slider.maxValue=1; slider.interactable=enabled;
        var track = StageboundMenuUI.Panel(bar,"Track",new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,5),new Color32(42,58,73,255));
        var fill = StageboundMenuUI.Panel(bar,"Fill",new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,5),StageboundMenuUI.Mint);
        fill.anchorMin=Vector2.zero; fill.anchorMax=Vector2.one; fill.offsetMin=new Vector2(0,8); fill.offsetMax=new Vector2(0,-8);
        var handle = StageboundMenuUI.Panel(bar,"Handle",new Vector2(.5f,.5f),Vector2.zero,new Vector2(12,22),Color.white);
        track.GetComponent<Image>().raycastTarget=true; handle.GetComponent<Image>().raycastTarget=true;
        slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>();
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v=>{setter(v);number.text=$"{v:P0}";});
        return slider;
    }
    public void Show() { gameObject.SetActive(true); StageboundMenuUI.Select(master); }
    public void Back() { StageboundAudioSettings.Save(); gameObject.SetActive(false); back?.Invoke(); }
    void Update()
    {
        if ((Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current!=null && Gamepad.current.buttonEast.wasPressedThisFrame)) Back();
    }
    void OnDisable() { StageboundAudioSettings.Save(); }
}
