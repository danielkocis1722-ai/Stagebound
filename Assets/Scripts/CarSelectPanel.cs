using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CarSelectPanel : MonoBehaviour
{
    VehicleCatalog catalog;
    Action onBack;
    Action<VehicleDefinition> onSelect;
    int index;
    TMP_Text nameLabel, detail, description, status, count, previewHint;
    readonly RectTransform[] bars=new RectTransform[4];
    Button select, back, browser;
    public Button Browser => browser;
    VehiclePreview preview;
    InputSystemUIInputModule inputModule;
    bool previousBackgroundDeselect, ownsNavigationFocus;
    public VehicleDefinition DisplayedVehicle => catalog!=null && catalog.vehicles!=null && index>=0 && index<catalog.vehicles.Length ? catalog.vehicles[index] : null;
    public bool CanSelect => DisplayedVehicle!=null && DisplayedVehicle.IsPlayable;

    public static CarSelectPanel Create(Transform parent, VehicleCatalog vehicles, Action leave, Action<VehicleDefinition> choose)
    {
        var root=StageboundMenuUI.Panel(parent,"Car Select",new Vector2(.5f,.5f),Vector2.zero,
            new Vector2(1360,860),new Color32(10,18,29,245));
        var panel=root.gameObject.AddComponent<CarSelectPanel>(); panel.catalog=vehicles; panel.onBack=leave; panel.onSelect=choose;
        panel.Build(); root.gameObject.SetActive(false); return panel;
    }
    void Build()
    {
        StageboundMenuUI.Text(transform,"CAR SELECT",38,new Vector2(40,-26),new Vector2(1100,70),Color.white);
        count=StageboundMenuUI.Text(transform,"",18,new Vector2(1200,-40),new Vector2(120,35),StageboundMenuUI.Muted);
        var previous=StageboundMenuUI.Button(transform,"<",new Vector2(40,-115),new Vector2(70,54),()=>Browse(-1));
        var next=StageboundMenuUI.Button(transform,">",new Vector2(1240,-115),new Vector2(70,54),()=>Browse(1));
        var browserRect=StageboundMenuUI.Panel(transform,"Car Browser",new Vector2(0,1),new Vector2(125,-115),new Vector2(1100,60),Color.white);
        browser=browserRect.gameObject.AddComponent<Button>();browser.targetGraphic=browserRect.GetComponent<Image>();
        browser.targetGraphic.raycastTarget=true;var colors=browser.colors;
        colors.normalColor=new Color32(10,18,29,0);colors.highlightedColor=colors.selectedColor=new Color32(30,58,65,150);browser.colors=colors;
        browser.onClick.AddListener(()=>StageboundMenuUI.Select(browser));
        nameLabel=StageboundMenuUI.Text(browserRect,"",38,new Vector2(10,2),new Vector2(1080,66),Color.white);
        detail=StageboundMenuUI.Text(transform,"",20,new Vector2(40,-225),new Vector2(590,40),StageboundMenuUI.Mint);
        description=StageboundMenuUI.Text(transform,"",24,new Vector2(40,-280),new Vector2(590,110),StageboundMenuUI.Muted);
        description.textWrappingMode=TextWrappingModes.Normal;
        var labels=new[]{"POWER","HANDLING","DRIFT","STABILITY"};
        for(int i=0;i<4;i++) {
            float y=420+i*54;
            StageboundMenuUI.Text(transform,labels[i],17,new Vector2(40,-y),new Vector2(150,30),StageboundMenuUI.Muted);
            var track=StageboundMenuUI.Panel(transform,labels[i]+" Track",new Vector2(0,1),new Vector2(210,-y-8),new Vector2(370,8),new Color32(42,58,73,255));
            bars[i]=StageboundMenuUI.Panel(track,labels[i]+" Fill",new Vector2(0,1),Vector2.zero,new Vector2(370,8),StageboundMenuUI.Mint);
        }
        var frame=StageboundMenuUI.Panel(transform,"Vehicle Preview",new Vector2(0,1),new Vector2(650,-220),new Vector2(660,420),new Color32(9,17,26,220));
        var imageObject=new GameObject("Preview Image",typeof(RectTransform),typeof(RawImage));
        var image=imageObject.GetComponent<RawImage>(); image.rectTransform.SetParent(frame,false);
        image.rectTransform.anchorMin=Vector2.zero; image.rectTransform.anchorMax=Vector2.one;
        image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero; image.raycastTarget=false;
        preview=imageObject.AddComponent<VehiclePreview>(); preview.image=image;
        previewHint=StageboundMenuUI.Text(frame,"",18,new Vector2(24,-180),new Vector2(610,60),StageboundMenuUI.Muted);
        status=StageboundMenuUI.Text(transform,"",18,new Vector2(650,-658),new Vector2(660,42),StageboundMenuUI.Mint);
        select=StageboundMenuUI.Button(transform,"SELECT",new Vector2(40,-716),new Vector2(300,60),Confirm);
        back=StageboundMenuUI.Button(transform,"BACK",new Vector2(365,-716),new Vector2(240,60),Back);
        StageboundMenuUI.Text(transform,"LEFT / RIGHT: BUTTONS    L1 / R1: CAR    UP: CAR BROWSER    ENTER / CROSS: CONFIRM    ESC / CIRCLE: BACK",
            16,new Vector2(40,-807),new Vector2(1260,30),StageboundMenuUI.Muted);
        foreach(var button in new[]{previous,next,browser,select,back}) {
            button.gameObject.AddComponent<CarSelectNavigation>().panel=this;
            button.navigation=new Navigation { mode=Navigation.Mode.Explicit };
        }
        UpdateNavigation();
    }
    public void Show()
    {
        gameObject.SetActive(true);
        // Keep keyboard/controller focus when a pointer clicks the blank card.
        inputModule=EventSystem.current!=null?EventSystem.current.GetComponent<InputSystemUIInputModule>():null;
        if(inputModule!=null && !ownsNavigationFocus) {
            previousBackgroundDeselect=inputModule.deselectOnBackgroundClick;
            inputModule.deselectOnBackgroundClick=false; ownsNavigationFocus=true;
        }
        index=0;
        string reason=null;
        if(catalog==null || !catalog.IsValid(out reason)) {
            nameLabel.text="VEHICLES UNAVAILABLE"; status.text=catalog==null?"Assign a vehicle catalog.":reason;
            select.interactable=false; StageboundMenuUI.Select(back); return;
        }
        var saved=VehicleSelection.Resolve(catalog); index=Array.IndexOf(catalog.vehicles,saved); if(index<0) index=0;
        Refresh(); StageboundMenuUI.Select(select.interactable?select:back);
    }
    public void ChangeVehicle(int direction)
    {
        if(!isActiveAndEnabled || catalog==null || catalog.vehicles==null || catalog.vehicles.Length==0) return;
        index=(index+direction%catalog.vehicles.Length+catalog.vehicles.Length)%catalog.vehicles.Length;
        var focused=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        Refresh();
        if(focused==select.gameObject&&!select.interactable) StageboundMenuUI.Select(back);
        else if(focused==null) StageboundMenuUI.Select(browser);
    }
    void Refresh()
    {
        var vehicle=DisplayedVehicle;
        if(vehicle==null) { select.interactable=false; return; }
        nameLabel.text=(string.IsNullOrWhiteSpace(vehicle.displayName)?vehicle.vehicleId:vehicle.displayName).ToUpperInvariant();
        detail.text=vehicle.drivetrainLabel;
        description.text=vehicle.description;
        count.text=$"{index+1:00} / {catalog.vehicles.Length:00}";
        var values=new[]{vehicle.power,vehicle.handling,vehicle.drift,vehicle.stability};
        for(int i=0;i<4;i++) bars[i].sizeDelta=new Vector2(370*Mathf.Clamp01(values[i]),8);
        select.interactable=vehicle.IsPlayable;
        status.text=vehicle.IsPlayable?(VehicleSelection.Resolve(catalog)==vehicle?"CURRENT CAR":"AVAILABLE"):vehicle.unavailableLabel;
        previewHint.text=preview.Show(vehicle)?"":vehicle.visualPrefab==null?"MODEL NOT YET ASSIGNED":"VEHICLE PREVIEW";
        UpdateNavigation();
    }
    void Browse(int direction) { ChangeVehicle(direction);StageboundMenuUI.Select(browser); }
    void UpdateNavigation()
    {
        browser.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnDown=select.interactable?select:back,selectOnUp=back};
        select.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=back,selectOnRight=back,selectOnUp=browser,selectOnDown=back};
        back.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=select.interactable?select:browser,
            selectOnRight=select.interactable?select:browser,selectOnUp=browser,selectOnDown=browser};
    }
    public void Confirm() { if(CanSelect) onSelect?.Invoke(DisplayedVehicle); }
    public void Back() { gameObject.SetActive(false); onBack?.Invoke(); }
    public void ShowError(string message) { status.text=message; }
    void OnDisable()
    {
        if(ownsNavigationFocus && inputModule!=null) inputModule.deselectOnBackgroundClick=previousBackgroundDeselect;
        ownsNavigationFocus=false;
    }
    void Update()
    {
        var pad=Gamepad.current;
        if(pad!=null && pad.leftShoulder.wasPressedThisFrame) ChangeVehicle(-1);
        if(pad!=null && pad.rightShoulder.wasPressedThisFrame) ChangeVehicle(1);
    }
}

