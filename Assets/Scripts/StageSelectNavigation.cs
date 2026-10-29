using UnityEngine;
using UnityEngine.EventSystems;

public class StageSelectNavigation : MonoBehaviour, ISelectHandler, ICancelHandler
{
    public StageSelectPanel panel;
    public int stageIndex=-1;
    public void OnSelect(BaseEventData eventData) { if(panel!=null&&stageIndex>=0) panel.Highlight(stageIndex); }
    public void OnCancel(BaseEventData eventData) { if(panel!=null) panel.Back();eventData.Use(); }
}
