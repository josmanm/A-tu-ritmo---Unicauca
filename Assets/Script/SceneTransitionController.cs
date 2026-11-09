using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionController : MonoBehaviour
{
    private static SceneTransitionController instance;

    [Header("Circular Iris")]
    [SerializeField] private Color irisColor = new Color(0.08f, 0.07f, 0.12f, 1f);
    [SerializeField] private float closeDuration = 0.22f;
    [SerializeField] private float openDuration = 0.2f;
    [SerializeField] [Range(0f, 2f)] private float openedRadius = 1.25f;
    [SerializeField] [Range(0f, 0.2f)] private float closedRadius = 0.01f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private CircularIrisGraphic irisGraphic;
    private Coroutine transitionRoutine;

    public static bool HasInstance => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void LoadScene(string sceneName)
    {
        EnsureInstance();
        instance.BeginLoadScene(sceneName);
    }

    public static void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject controllerObject = new GameObject("SceneTransitionController");
        instance = controllerObject.AddComponent<SceneTransitionController>();
        DontDestroyOnLoad(controllerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void BeginLoadScene(string sceneName)
    {
        EnsureOverlay();

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (irisGraphic == null)
        {
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        irisGraphic.raycastTarget = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        irisGraphic.color = irisColor;
        float elapsed = 0f;
        float outDuration = Mathf.Max(0.01f, closeDuration);
        irisGraphic.InnerRadiusNormalized = openedRadius;

        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / outDuration);
            irisGraphic.InnerRadiusNormalized = Mathf.Lerp(openedRadius, closedRadius, k);
            yield return null;
        }

        irisGraphic.InnerRadiusNormalized = closedRadius;
        yield return null;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        while (!loadOperation.isDone)
            yield return null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureOverlay();

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(FadeInRoutine());
        StartCoroutine(ReleaseOverlayFailSafe(openDuration + 0.5f));
    }

    private IEnumerator FadeInRoutine()
    {
        if (irisGraphic == null)
            yield break;

        // Once the target scene is loaded, keep the fade visual but never block UI input.
        irisGraphic.raycastTarget = false;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        canvasGroup.alpha = 1f;
        irisGraphic.color = irisColor;
        irisGraphic.InnerRadiusNormalized = closedRadius;

        float elapsed = 0f;
        float inDuration = Mathf.Max(0.01f, openDuration);

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / inDuration);
            irisGraphic.InnerRadiusNormalized = Mathf.Lerp(closedRadius, openedRadius, k);
            yield return null;
        }

        irisGraphic.InnerRadiusNormalized = openedRadius;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        irisGraphic.raycastTarget = false;
        transitionRoutine = null;
    }

    private IEnumerator ReleaseOverlayFailSafe(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delay));

        if (transitionRoutine != null)
            yield break;

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        if (irisGraphic != null)
            irisGraphic.raycastTarget = false;
    }

    private void EnsureOverlay()
    {
        if (canvas != null && canvasGroup != null && irisGraphic != null)
        {
            irisGraphic.color = irisColor;
            return;
        }

        canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("TransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        Transform overlayTransform = canvas.transform.Find("Overlay");
        if (overlayTransform == null)
        {
            GameObject overlayObject = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup), typeof(CircularIrisGraphic));
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlayTransform = overlayObject.transform;
        }

        canvasGroup = overlayTransform.GetComponent<CanvasGroup>();
        irisGraphic = overlayTransform.GetComponent<CircularIrisGraphic>();
        if (overlayTransform.GetComponent<CanvasRenderer>() == null)
            overlayTransform.gameObject.AddComponent<CanvasRenderer>();
        irisGraphic.color = irisColor;
        irisGraphic.raycastTarget = false;
        irisGraphic.InnerRadiusNormalized = openedRadius;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
