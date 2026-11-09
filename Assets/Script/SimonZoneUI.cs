using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class SimonZoneUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Referencias")]
    [SerializeField] private Image mainImage;
    [SerializeField] private Button zoneButton;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Outline zoneOutline;

    [Header("Animacion label")]
    [SerializeField] private float activeLabelScale = 1.2f;
    [SerializeField] private Color activeLabelColor = new Color(1f, 0.97f, 0.78f, 1f);

    [Header("Juice visual")]
    [SerializeField] private float pulseDuration = 0.12f;
    [SerializeField] private Color outlineColor = new Color(1f, 0.95f, 0.55f, 0.75f);
    [SerializeField] private Vector2 outlineDistance = new Vector2(10f, -10f);

    private int index;
    private Action<int, double> onPressed;
    private bool pointerHandled;
    private Vector3 baseLabelScale = Vector3.one;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        AutoResolveReferences();
    }

    public void Setup(int newIndex, string label, Action<int, double> callback)
    {
        AutoResolveReferences();

        index = newIndex;
        onPressed = callback;

        if (mainImage != null)
        {
            mainImage.color = Color.clear;
            mainImage.raycastTarget = true;
        }

        if (zoneOutline != null)
        {
            zoneOutline.enabled = false;
            zoneOutline.effectColor = outlineColor;
            zoneOutline.effectDistance = outlineDistance;
            zoneOutline.useGraphicAlpha = true;
        }

        if (labelText != null)
        {
            labelText.text = label;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            baseLabelScale = labelText.rectTransform.localScale;
        }

        if (zoneButton != null)
        {
            zoneButton.transition = Selectable.Transition.None;
            zoneButton.onClick.RemoveAllListeners();
            zoneButton.onClick.AddListener(HandlePress);
        }

        HideUnusedChildImages();
    }

    public void SetInteractable(bool value)
    {
        if (zoneButton != null)
            zoneButton.interactable = value;
    }

    public void SetOff()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        if (mainImage != null)
            mainImage.color = Color.clear;

        if (zoneOutline != null)
            zoneOutline.enabled = false;

        if (labelText != null)
        {
            labelText.color = Color.white;
            labelText.rectTransform.localScale = baseLabelScale;
        }
    }

    public void SetHighlighted(Color? accentColor = null)
    {
        Color highlightColor = accentColor ?? activeLabelColor;

        if (zoneOutline != null)
        {
            Color outlineHighlight = highlightColor;
            outlineHighlight.a = outlineColor.a;
            zoneOutline.effectColor = outlineHighlight;
            zoneOutline.effectDistance = outlineDistance;
            zoneOutline.enabled = true;
        }

        if (labelText != null)
        {
            labelText.color = highlightColor;
            labelText.rectTransform.localScale = baseLabelScale * activeLabelScale;
        }

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseZone());
    }

    private void HandlePress()
    {
        if (!pointerHandled)
            onPressed?.Invoke(index, AudioSettings.dspTime);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (zoneButton == null || !zoneButton.IsActive() || !zoneButton.IsInteractable())
            return;

        pointerHandled = true;
        onPressed?.Invoke(index, AudioSettings.dspTime);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointerHandled)
            StartCoroutine(ClearPointerAfterFrame());
    }

    private IEnumerator ClearPointerAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        pointerHandled = false;
    }

    private void AutoResolveReferences()
    {
        if (mainImage == null)
            mainImage = GetComponent<Image>();

        if (zoneButton == null)
            zoneButton = GetComponent<Button>();

        if (zoneOutline == null)
            zoneOutline = GetComponent<Outline>();

        if (zoneOutline == null)
            zoneOutline = gameObject.AddComponent<Outline>();

        if (labelText == null)
        {
            Transform labelTransform = transform.Find("Label");
            if (labelTransform != null)
                labelText = labelTransform.GetComponent<TMP_Text>();
        }

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>(true);

        if (labelText != null)
            baseLabelScale = labelText.rectTransform.localScale;

        if (zoneOutline != null)
        {
            zoneOutline.enabled = false;
            zoneOutline.effectColor = outlineColor;
            zoneOutline.effectDistance = outlineDistance;
            zoneOutline.useGraphicAlpha = true;
        }
    }

    private IEnumerator PulseZone()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, pulseDuration);
        Color peakOutlineColor = zoneOutline != null ? zoneOutline.effectColor : outlineColor;
        Color startOutlineColor = peakOutlineColor;
        startOutlineColor.a = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);

            if (zoneOutline != null)
                zoneOutline.effectColor = Color.Lerp(startOutlineColor, peakOutlineColor, k);

            yield return null;
        }

        if (zoneOutline != null)
            zoneOutline.effectColor = peakOutlineColor;

        pulseRoutine = null;
    }

    private void HideUnusedChildImages()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null || images[i] == mainImage)
                continue;

            images[i].enabled = false;
            images[i].raycastTarget = false;
        }
    }
}
