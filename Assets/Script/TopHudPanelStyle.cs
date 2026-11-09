using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TopHudPanelStyle : MonoBehaviour
{
    private const float DesignWidth = 1280f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = new Color(1f, 0.95f, 0.78f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.86f, 0.82f, 0.72f, 1f);
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.55f);
    [SerializeField] private float fadeDuration = 0.08f;
    private RectTransform parentRect;
    private float lastParentWidth = -1f;

    private void LateUpdate()
    {
        if (parentRect == null)
            parentRect = transform.parent as RectTransform;
        if (parentRect == null || Mathf.Approximately(lastParentWidth, parentRect.rect.width))
            return;

        lastParentWidth = parentRect.rect.width;
        float scale = Mathf.Min(1f, lastParentWidth / DesignWidth);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            StyleButton(buttons[i]);
    }

    private void StyleButton(Button button)
    {
        if (button == null)
            return;

        CartoonButtonSkin cartoonSkin = button.GetComponent<CartoonButtonSkin>();
        if (cartoonSkin != null)
        {
            cartoonSkin.Apply();
            if (button.GetComponent<UIButtonPulse>() == null)
                button.gameObject.AddComponent<UIButtonPulse>();
            return;
        }

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = selectedColor;
        colors.disabledColor = disabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = fadeDuration;
        button.colors = colors;

        if (button.targetGraphic != null)
            button.targetGraphic.color = button.interactable ? normalColor : disabledColor;

        if (button.GetComponent<UIButtonPulse>() == null)
            button.gameObject.AddComponent<UIButtonPulse>();
    }
}
