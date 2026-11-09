using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonPulse : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float animationDuration = 0.08f;

    private RectTransform rectTransform;
    private Button button;
    private Vector3 baseScale = Vector3.one;
    private Coroutine scaleRoutine;
    private bool isHovered;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        button = GetComponent<Button>();
        if (rectTransform != null)
            baseScale = rectTransform.localScale;
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (rectTransform != null)
            rectTransform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        isHovered = true;
        AnimateTo(baseScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        AnimateTo(baseScale);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        AnimateTo(baseScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        AnimateTo(baseScale * (isHovered ? hoverScale : 1f));
    }

    private bool IsInteractable()
    {
        return button == null || button.IsInteractable();
    }

    private void AnimateTo(Vector3 targetScale)
    {
        if (rectTransform == null)
            return;

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(AnimateScale(targetScale));
    }

    private IEnumerator AnimateScale(Vector3 targetScale)
    {
        Vector3 startScale = rectTransform.localScale;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, k);
            yield return null;
        }

        rectTransform.localScale = targetScale;
        scaleRoutine = null;
    }
}
