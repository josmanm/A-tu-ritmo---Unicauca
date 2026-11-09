using UnityEngine;
using UnityEngine.UI;

// Preserve the design's 800-unit height on wide landscape phones. Matching
// width/height halfway makes the canvas too short and clips bottom controls.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public class LandscapePhoneCanvas : MonoBehaviour
{
    private CanvasScaler scaler;
    private int lastWidth = -1;
    private int lastHeight = -1;

    public static bool IsWidePhone => Screen.height > 0 && Screen.width > Screen.height &&
        (float)Screen.width / Screen.height >= 1.65f;

    public static void Configure(Canvas canvas)
    {
        if (canvas == null)
            return;
        LandscapePhoneCanvas layout = canvas.GetComponent<LandscapePhoneCanvas>();
        if (layout == null)
            layout = canvas.gameObject.AddComponent<LandscapePhoneCanvas>();
        layout.Apply();
    }

    private void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void LateUpdate()
    {
        if (lastWidth != Screen.width || lastHeight != Screen.height)
            Apply();
    }

    private void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        if (scaler != null)
            scaler.matchWidthOrHeight = IsWidePhone ? 1f : 0.5f;
    }
}
