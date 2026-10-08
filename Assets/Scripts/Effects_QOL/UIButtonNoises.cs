using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonNoises : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public string hoverSound;
    public string clickSound;

    public void OnPointerEnter(PointerEventData eventData)
    {
        AudioManager.Instance.PlaySFX(hoverSound);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        AudioManager.Instance.PlaySFX(clickSound);
    }
}