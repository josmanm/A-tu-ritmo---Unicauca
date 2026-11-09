using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonPressOnDown : MonoBehaviour, IPointerDownHandler
{
    private Button button;
    private Action onPressed;

    public void Configure(Action callback)
    {
        button = GetComponent<Button>();
        onPressed = callback;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && button != null && button.IsActive() && button.IsInteractable())
            onPressed?.Invoke();
    }
}
