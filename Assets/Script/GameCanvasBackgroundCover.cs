using UnityEngine;
using UnityEngine.UI;

// Keeps scene illustrations edge-to-edge without stretching them on wide phones.
[DisallowMultipleComponent]
public class GameCanvasBackgroundCover : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform parent;
    private Image image;

    public static void Attach(Canvas canvas, string backgroundName)
    {
        if (canvas == null)
            return;

        foreach (Transform child in canvas.transform)
        {
            if (child.name.Trim() != backgroundName || !child.TryGetComponent(out Image background))
                continue;

            if (background.GetComponent<GameCanvasBackgroundCover>() == null)
                background.gameObject.AddComponent<GameCanvasBackgroundCover>();
            return;
        }
    }

    private void Awake()
    {
        rect = transform as RectTransform;
        parent = transform.parent as RectTransform;
        image = GetComponent<Image>();
    }

    private void LateUpdate()
    {
        if (rect == null || parent == null || image == null || image.sprite == null ||
            parent.rect.width <= 0f || parent.rect.height <= 0f)
            return;

        Vector2 spriteSize = image.sprite.rect.size;
        float scale = Mathf.Max(parent.rect.width / spriteSize.x, parent.rect.height / spriteSize.y);
        Vector2 coverSize = spriteSize * scale;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        if ((rect.sizeDelta - coverSize).sqrMagnitude > 0.01f)
            rect.sizeDelta = coverSize;
    }
}
