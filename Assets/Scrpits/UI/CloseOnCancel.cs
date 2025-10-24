using UnityEngine;
using UnityEngine.EventSystems;

public class CloseOnCancel : MonoBehaviour, ICancelHandler
{
    public GameObject panel;

    public void OnCancel(BaseEventData e)
    {
        panel.SetActive(false);
        EventSystem.current.SetSelectedGameObject(GameObject.Find("BtnInstructions"));
    }
}
