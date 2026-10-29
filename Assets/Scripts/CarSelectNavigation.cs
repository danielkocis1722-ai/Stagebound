using UnityEngine;
using UnityEngine.EventSystems;

// Uses the same EventSystem move/cancel actions as the existing menu buttons.
public class CarSelectNavigation : MonoBehaviour, IMoveHandler, ICancelHandler
{
    public CarSelectPanel panel;
    public void OnMove(AxisEventData eventData)
    {
        if(panel==null) return;
        if(gameObject!=panel.Browser.gameObject) return;
        if(eventData.moveDir==MoveDirection.Left) { panel.ChangeVehicle(-1); eventData.Use(); }
        else if(eventData.moveDir==MoveDirection.Right) { panel.ChangeVehicle(1); eventData.Use(); }
    }
    public void OnCancel(BaseEventData eventData) { if(panel!=null) panel.Back(); eventData.Use(); }
}

