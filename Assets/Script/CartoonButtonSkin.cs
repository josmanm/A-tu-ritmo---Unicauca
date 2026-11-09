using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button), typeof(Image))]
public class CartoonButtonSkin : MonoBehaviour
{
    [SerializeField] private Sprite normal;
    [SerializeField] private Sprite hover;
    [SerializeField] private Sprite pressed;
    [SerializeField] private Sprite disabled;
    [SerializeField] private Sprite focused;

    private void OnEnable()
    {
        Apply();
    }

    public void Configure(Sprite normalSprite, Sprite hoverSprite, Sprite pressedSprite,
        Sprite disabledSprite, Sprite focusedSprite = null)
    {
        normal = normalSprite;
        hover = hoverSprite;
        pressed = pressedSprite;
        disabled = disabledSprite;
        focused = focusedSprite;
        Apply();
    }

    public void Apply()
    {
        Button button = GetComponent<Button>();
        Image image = GetComponent<Image>();
        if (normal == null || button == null || image == null)
            return;

        image.sprite = normal;
        // Kit PNGs without a 9-slice border must render as Simple; Sliced
        // would stretch their highlights and contours unpredictably.
        image.type = normal.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState states = button.spriteState;
        states.highlightedSprite = hover != null ? hover : normal;
        states.pressedSprite = pressed != null ? pressed : normal;
        states.selectedSprite = focused != null ? focused : states.highlightedSprite;
        states.disabledSprite = disabled != null ? disabled : normal;
        button.spriteState = states;
    }
}
