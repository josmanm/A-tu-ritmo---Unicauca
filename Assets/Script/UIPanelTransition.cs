using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class UIPanelTransition : MonoBehaviour
{
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private bool useScale = true;
    [SerializeField] private float hiddenScale = 0.96f;
    [SerializeField] private bool useSlide = true;
    [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -32f);

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 shownScale = Vector3.one;
    private Vector2 shownPosition;
    private Coroutine transitionRoutine;

    private void Awake()
    {
        ResolveReferences();
        CaptureShownState();
    }

    public void CaptureShownState()
    {
        ResolveReferences();
        if (rectTransform != null)
        {
            shownScale = rectTransform.localScale;
            shownPosition = rectTransform.anchoredPosition;
        }
    }

    public void SetHiddenInstant()
    {
        ResolveReferences();
        ApplyState(0f);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void SetShownInstant()
    {
        ResolveReferences();
        ApplyState(1f);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void PlayShow(Action onComplete = null)
    {
        ResolveReferences();
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        gameObject.SetActive(true);
        transitionRoutine = StartCoroutine(AnimateTransition(show: true, onComplete));
    }

    public void PlayHide(Action onComplete = null)
    {
        ResolveReferences();
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(AnimateTransition(show: false, onComplete));
    }

    private IEnumerator AnimateTransition(bool show, Action onComplete)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        float startAlpha = canvasGroup.alpha;
        float endAlpha = show ? 1f : 0f;
        float startT = show ? 0f : 1f;
        float endT = show ? 1f : 0f;

        if (show)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            if (canvasGroup.alpha <= 0.001f)
                ApplyState(0f);
        }

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / safeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, k);
            ApplyState(Mathf.Lerp(startT, endT, k));
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        ApplyState(endT);
        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;
        transitionRoutine = null;
        onComplete?.Invoke();
    }

    private void ApplyState(float shownT)
    {
        if (rectTransform == null)
            return;

        if (useScale)
        {
            Vector3 hidden = shownScale * hiddenScale;
            rectTransform.localScale = Vector3.Lerp(hidden, shownScale, shownT);
        }
        else
        {
            rectTransform.localScale = shownScale;
        }

        if (useSlide)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(shownPosition + hiddenOffset, shownPosition, shownT);
        }
        else
        {
            rectTransform.anchoredPosition = shownPosition;
        }
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (rectTransform == null)
            rectTransform = transform as RectTransform;
    }
}
