using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PausePanelView : MonoBehaviour
{
    [SerializeField] private string contentRootName = "PauseBox";
    [SerializeField] private float overlayFadeDuration = 0.18f;

    private UIPanelTransition panelTransition;
    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    public void InitializeHidden()
    {
        ResolveReferences();

        if (panelTransition != null)
        {
            panelTransition.CaptureShownState();
            panelTransition.SetHiddenInstant();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    public void Show()
    {
        ResolveReferences();
        gameObject.SetActive(true);

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(AnimateOverlayAlpha(1f, null));

        if (panelTransition != null)
        {
            panelTransition.PlayShow();
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void Hide(Action onComplete = null)
    {
        ResolveReferences();

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (panelTransition != null)
        {
            panelTransition.PlayHide(() =>
            {
                fadeRoutine = StartCoroutine(AnimateOverlayAlpha(0f, () =>
                {
                    gameObject.SetActive(false);
                    onComplete?.Invoke();
                }));
            });
            return;
        }

        fadeRoutine = StartCoroutine(AnimateOverlayAlpha(0f, () =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }));
    }

    private void ResolveReferences()
    {
        if (panelTransition == null)
        {
            Transform contentRoot = transform.Find(contentRootName);
            if (contentRoot != null)
            {
                panelTransition = contentRoot.GetComponent<UIPanelTransition>();
                if (panelTransition == null)
                    panelTransition = contentRoot.gameObject.AddComponent<UIPanelTransition>();
            }

            if (panelTransition == null)
                panelTransition = GetComponent<UIPanelTransition>();
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private IEnumerator AnimateOverlayAlpha(float targetAlpha, Action onComplete)
    {
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, overlayFadeDuration);

        canvasGroup.interactable = targetAlpha > 0.01f;
        canvasGroup.blocksRaycasts = targetAlpha > 0.01f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, k);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.interactable = targetAlpha > 0.01f;
        canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
        fadeRoutine = null;
        onComplete?.Invoke();
    }
}
