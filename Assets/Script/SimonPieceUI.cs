using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public class SimonPieceUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Image mainImage;
    [SerializeField] private Button pieceButton;
    [SerializeField] private TMP_Text labelText;

    [Header("Animacion de destello")]
    [SerializeField] private float flashDuration = 0.12f;

    private Color normalColor;
    private Color litColor;
    private Color labelDefaultColor = Color.white;

    private int index;
    private Action<int> onPressed;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (mainImage == null)
            mainImage = GetComponent<Image>();

        if (pieceButton == null)
            pieceButton = GetComponent<Button>();

        if (labelText != null)
            labelDefaultColor = labelText.color;
    }

    public void Setup(int newIndex, Color normal, Color lit, string label, Action<int> callback)
    {
        index = newIndex;
        normalColor = normal;
        litColor = lit;
        onPressed = callback;

        if (mainImage != null)
        {
            mainImage.color = normalColor;
            mainImage.alphaHitTestMinimumThreshold = 0.1f;
        }

        if (labelText != null)
        {
            labelText.text = label;
            labelText.color = Color.white;
        }

        if (pieceButton != null)
        {
            pieceButton.onClick.RemoveAllListeners();
            pieceButton.onClick.AddListener(HandlePress);
        }
    }

    private void HandlePress()
    {
        onPressed?.Invoke(index);
    }

    public void SetInteractable(bool value)
    {
        if (pieceButton != null)
            pieceButton.interactable = value;
    }

    public void SetOff()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (mainImage != null)
            mainImage.color = normalColor;

        if (labelText != null)
            labelText.color = Color.white;
    }

    public void ShowLit()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (mainImage != null)
            mainImage.color = litColor;
    }

    public void FlashOn()
    {
        if (!gameObject.activeInHierarchy)
        {
            ShowLit();
            return;
        }

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (mainImage != null)
            mainImage.color = litColor;

        yield return new WaitForSeconds(flashDuration);

        if (mainImage != null)
            mainImage.color = normalColor;

        if (labelText != null)
            labelText.color = Color.white;

        flashRoutine = null;
    }

    public void SetLabelRotation(float zRotation)
    {
        if (labelText != null)
        {
            labelText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            labelText.rectTransform.localScale = Vector3.one;
        }
    }

    public void SetLabelAnchoredX(float x)
    {
        if (labelText == null) return;

        Vector2 pos = labelText.rectTransform.anchoredPosition;
        pos.x = x;
        labelText.rectTransform.anchoredPosition = pos;
    }

    public void SetLabelAnchoredPosition(float x, float y)
    {
        if (labelText == null) return;
        labelText.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    public void SetLabelFontSize(float size)
    {
        if (labelText == null) return;
        labelText.fontSize = size;
    }

    public void SetLabelColor(Color color)
    {
        if (labelText != null)
            labelText.color = color;
    }

    public void SetSprite(Sprite sprite)
    {
        if (mainImage != null && sprite != null)
        {
            mainImage.sprite = sprite;
            mainImage.preserveAspect = true;
        }
    }

    public int GetIndex()
    {
        return index;
    }
}
