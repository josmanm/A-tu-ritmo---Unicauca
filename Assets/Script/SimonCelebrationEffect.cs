using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimonCelebrationEffect : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ParticleSystem particlePerfect;
    [SerializeField] private ParticleSystem particleGood;
    [SerializeField] private Image screenFlash;
    [SerializeField] private float flashDuration = 0.15f;

    [Header("Colores de feedback")]
    [SerializeField] private Color successFlashColor = new Color(0.3f, 1f, 0.4f, 0.3f);
    [SerializeField] private Color errorFlashColor = new Color(1f, 0.3f, 0.3f, 0.3f);
    [SerializeField] private Color levelUpColor = new Color(1f, 0.9f, 0.3f, 0.3f);

    private Coroutine flashRoutine;

    public void PlayPerfectEffect(Vector3 position)
    {
        PlayParticles(particlePerfect, position);
        FlashScreen(successFlashColor);
    }

    public void PlayGoodEffect(Vector3 position)
    {
        PlayParticles(particleGood, position);
    }

    public void PlayErrorEffect(Vector3 position)
    {
        FlashScreen(errorFlashColor);
    }

    public void PlayLevelUpEffect()
    {
        FlashScreen(levelUpColor);
    }

    private void PlayParticles(ParticleSystem particles, Vector3 position)
    {
        if (particles == null) return;

        ParticleSystem ps = Instantiate(particles, position, Quaternion.identity);
        ps.Play();
        Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
    }

    private void FlashScreen(Color color)
    {
        if (screenFlash == null) return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(color));
    }

    private System.Collections.IEnumerator FlashRoutine(Color color)
    {
        screenFlash.gameObject.SetActive(true);
        screenFlash.color = color;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(color.a, 0f, elapsed / flashDuration);
            screenFlash.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        screenFlash.gameObject.SetActive(false);
    }
}
