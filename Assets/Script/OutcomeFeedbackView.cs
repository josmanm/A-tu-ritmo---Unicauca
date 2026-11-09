using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class OutcomeFeedbackView : MonoBehaviour
{
    [SerializeField] private Sprite successIcon;
    [SerializeField] private Sprite errorIcon;
    [SerializeField] private Sprite[] successFrames;
    [SerializeField] private Sprite[] errorFrames;
    [SerializeField] private Vector2 offset = new Vector2(0f, 90f);
    [SerializeField] private float burstSize = 200f;
    [SerializeField] private float iconSize = 70f;
    [SerializeField] private bool showSuccessBurst = true;
    [SerializeField] private bool showErrorBurst = true;
    [SerializeField] private float framesPerSecond = 15f;
    [SerializeField] [Min(0.55f)] private float minimumVisibleSeconds = 0.55f;

    private RectTransform root;
    private RectTransform canvasRect;
    private Canvas canvas;
    private Image burst;
    private Image icon;
    private Coroutine animation;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        canvasRect = canvas.transform as RectTransform;
        GameObject container = new GameObject("OutcomeFeedbackVisual", typeof(RectTransform), typeof(CanvasGroup));
        root = container.GetComponent<RectTransform>();
        root.SetParent(canvasRect, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(burstSize, burstSize);
        CanvasGroup group = container.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        burst = CreateImage(root, "CrispJuiceBurst", burstSize);
        icon = CreateImage(root, "CartoonOutcomeIcon", iconSize);
        root.gameObject.SetActive(false);
    }

    private static Image CreateImage(Transform parent, string name, float size)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        Image image = item.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public void ShowSuccess(RectTransform target = null)
    {
        Show(successIcon, showSuccessBurst ? successFrames : null, target);
    }

    public void ShowFailure(RectTransform target = null)
    {
        Show(errorIcon, showErrorBurst ? errorFrames : null, target);
    }

    public void Hide()
    {
        if (animation != null)
        {
            StopCoroutine(animation);
            animation = null;
        }
        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void Show(Sprite symbol, Sprite[] frames, RectTransform target)
    {
        if (root == null || (symbol == null && (frames == null || frames.Length == 0)))
            return;

        if (animation != null)
            StopCoroutine(animation);

        Vector2 position = Vector2.zero;
        if (target != null)
        {
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, camera, out position);
        }

        position += offset;
        Vector2 half = canvasRect.rect.size * 0.5f;
        float margin = burstSize * 0.5f;
        root.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -half.x + margin, half.x - margin),
            Mathf.Clamp(position.y, -half.y + margin, half.y - margin));
        root.SetAsLastSibling();
        root.gameObject.SetActive(true);
        icon.sprite = symbol;
        icon.gameObject.SetActive(symbol != null);
        burst.gameObject.SetActive(frames != null && frames.Length > 0);
        animation = StartCoroutine(Animate(frames));
    }

    private IEnumerator Animate(Sprite[] frames)
    {
        int count = frames != null ? frames.Length : 0;
        float duration = Mathf.Max(minimumVisibleSeconds, count / Mathf.Max(1f, framesPerSecond));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (count > 0)
                burst.sprite = frames[Mathf.Min(count - 1, Mathf.FloorToInt(elapsed * framesPerSecond))];
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        root.gameObject.SetActive(false);
        animation = null;
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root.gameObject);
    }
}
