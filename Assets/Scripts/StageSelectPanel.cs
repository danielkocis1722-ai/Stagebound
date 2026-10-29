using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class StageSelectPanel : MonoBehaviour
{
    StageCatalog catalog;
    VehicleDefinition vehicle;
    Action onBack;
    Action<StageDefinition> onStart;
    int index;
    TMP_Text title,description,carLabel,records,status;
    readonly List<Button> rows=new List<Button>();
    Button start,back;
    ScrollRect scroll;
    InputSystemUIInputModule inputModule;
    bool ownsFocus,previousDeselect;
    public StageDefinition DisplayedStage => catalog!=null&&catalog.stages!=null&&index>=0&&index<catalog.stages.Length?catalog.stages[index]:null;
    public bool CanStart => DisplayedStage!=null&&DisplayedStage.IsPlayable&&vehicle!=null&&vehicle.IsPlayable;
    public static StageSelectPanel Create(Transform parent,StageCatalog maps,Action leave,Action<StageDefinition> play)
    {
        var root=StageboundMenuUI.Panel(parent,"Stage Select",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1360,860),new Color32(10,18,29,245));
        var panel=root.gameObject.AddComponent<StageSelectPanel>();panel.catalog=maps;panel.onBack=leave;panel.onStart=play;
        panel.Build();root.gameObject.SetActive(false);return panel;
    }
    void Build()
    {
        StageboundMenuUI.Text(transform,"SELECT MAP",38,new Vector2(40,-26),new Vector2(1200,65),Color.white);
        carLabel=StageboundMenuUI.Text(transform,"",20,new Vector2(40,-108),new Vector2(1250,40),StageboundMenuUI.Mint);
        var viewport=StageboundMenuUI.Panel(transform,"Maps",new Vector2(0,1),new Vector2(40,-190),new Vector2(530,490),new Color32(9,17,26,100));
        viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
        var content=new GameObject("Map List",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
        content.sizeDelta=new Vector2(0,Mathf.Max(490,(catalog?.stages?.Length??0)*104));scroll.content=content;
        if(catalog!=null&&catalog.stages!=null) for(int i=0;i<catalog.stages.Length;i++) {
            int rowIndex=i;var stage=catalog.stages[i];
            var button=StageboundMenuUI.Button(content,stage!=null?stage.stageName:"UNAVAILABLE",new Vector2(4,-i*104),new Vector2(510,88),()=>{
                Highlight(rowIndex);
                if(CanStart) Confirm();
                else StageboundMenuUI.Select(back);
            });
            rows.Add(button);var nav=button.gameObject.AddComponent<StageSelectNavigation>();nav.panel=this;nav.stageIndex=i;
            StageboundMenuUI.Text(button.transform,stage!=null&&stage.IsPlayable?"TIME ATTACK":stage?.unavailableLabel??"UNAVAILABLE",
                14,new Vector2(22,-51),new Vector2(475,27),StageboundMenuUI.Muted);
        }
        title=StageboundMenuUI.Text(transform,"",34,new Vector2(650,-195),new Vector2(660,60),Color.white);
        description=StageboundMenuUI.Text(transform,"",20,new Vector2(650,-270),new Vector2(660,90),StageboundMenuUI.Muted);
        description.textWrappingMode=TextWrappingModes.Normal;
        records=StageboundMenuUI.Text(transform,"",22,new Vector2(650,-380),new Vector2(660,270),Color.white);
        status=StageboundMenuUI.Text(transform,"",17,new Vector2(650,-664),new Vector2(660,50),StageboundMenuUI.Mint);
        start=StageboundMenuUI.Button(transform,"START",new Vector2(40,-736),new Vector2(320,60),Confirm);
        back=StageboundMenuUI.Button(transform,"BACK",new Vector2(390,-736),new Vector2(240,60),Back);
        foreach(var button in new[]{start,back})button.gameObject.AddComponent<StageSelectNavigation>().panel=this;
        StageboundMenuUI.Text(transform,"D-PAD / ARROWS: NAVIGATE    CROSS / ENTER: CONFIRM    CIRCLE / ESC: BACK",
            16,new Vector2(40,-813),new Vector2(1250,28),StageboundMenuUI.Muted);
    }
    public void Show(VehicleDefinition selectedVehicle)
    {
        vehicle=selectedVehicle;gameObject.SetActive(true);
        inputModule=EventSystem.current!=null?EventSystem.current.GetComponent<InputSystemUIInputModule>():null;
        if(inputModule!=null&&!ownsFocus) {previousDeselect=inputModule.deselectOnBackgroundClick;inputModule.deselectOnBackgroundClick=false;ownsFocus=true;}
        carLabel.text="CAR  /  "+(vehicle!=null?vehicle.displayName.ToUpperInvariant():"NO PLAYABLE CAR SELECTED");
        index=0;var initial=catalog!=null?catalog.Initial:null;
        if(initial!=null)index=Array.IndexOf(catalog.stages,initial);
        Highlight(index);StageboundMenuUI.Select(rows.Count>index&&index>=0?rows[index]:back);
    }
    public void Highlight(int stageIndex)
    {
        index=stageIndex;var stage=DisplayedStage;start.interactable=CanStart;
        if(stage==null) {title.text="NO MAPS CONFIGURED";description.text="";records.text="";status.text="Assign a stage catalog.";UpdateNavigation();return;}
        title.text=stage.stageName.ToUpperInvariant();description.text=stage.description;
        var overall=StageRecords.Overall(stage,stage.checkpointCount);
        var car=StageRecords.ForVehicle(stage,stage.checkpointCount,vehicle!=null?vehicle.vehicleId:null);
        records.text="SELECTED CAR\nBEST (STYLE)   "+Time(car.hasBest,car.bestAdjustedTime)+
            "\nFASTEST RAW    "+Time(car.hasBest,car.bestRawTime)+"\nBEST DRIFT       "+(car.hasBest?Mathf.RoundToInt(car.bestDriftScore).ToString("N0"):"--")+
            "\n\nALL CARS\nBEST (STYLE)   "+Time(overall.hasBest,overall.bestAdjustedTime)+
            "\nFASTEST RAW    "+Time(overall.hasBest,overall.bestRawTime);
        status.text=CanStart?"READY":!stage.selectable?stage.unavailableLabel:"MAP OR CAR UNAVAILABLE";
        UpdateNavigation();
        float height=scroll.viewport.rect.height;
        scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(index*104-height*.35f,0,Mathf.Max(0,scroll.content.sizeDelta.y-height)));
    }
    static string Time(bool hasBest,float value)=>hasBest?RaceTimer.FormatTime(value):"--:--.---";
    void UpdateNavigation()
    {
        for(int i=0;i<rows.Count;i++) rows[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnUp=i>0?rows[i-1]:back,selectOnDown=i+1<rows.Count?rows[i+1]:(start.interactable?start:back),
            selectOnRight=start.interactable?start:back};
        var row=index>=0&&index<rows.Count?rows[index]:null;
        start.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=row,selectOnLeft=row,selectOnRight=back,selectOnDown=back};
        back.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=row,selectOnLeft=start.interactable?start:row,
            selectOnRight=start.interactable?start:row,selectOnDown=row};
    }
    public void Confirm() {if(CanStart)onStart?.Invoke(DisplayedStage);}
    public void ShowError(string message)=>status.text=message;
    public void Back() {gameObject.SetActive(false);onBack?.Invoke();}
    void OnDisable(){if(ownsFocus&&inputModule!=null)inputModule.deselectOnBackgroundClick=previousDeselect;ownsFocus=false;}
}
