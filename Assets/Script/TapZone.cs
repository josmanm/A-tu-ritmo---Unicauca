using UnityEngine;
using UnityEngine.EventSystems;

public class TapZone : MonoBehaviour, IPointerDownHandler
{
    public TempoTapGameManager game;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!game || eventData.button != PointerEventData.InputButton.Left)
            return;

        // Además del botón JUGAR, tocar el área de juego permite iniciar en pantallas táctiles.
        if (!game.IsSessionActive)
            game.StartGameFromButton();
        else
            game.RegisterTap();
    }
}
