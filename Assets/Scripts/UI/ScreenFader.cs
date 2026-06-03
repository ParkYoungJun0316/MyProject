using System.Collections;
using UnityEngine;

/// <summary>
/// Canvas Group 알파로 화면 암전/복귀.
/// FadeOverlay(CanvasGroup)에 부착하고 MouthController와 연동.
/// maxAlpha: 암전 최대 불투명도. 1 = 완전 암전, 0.9 = 약간 투과
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;

    [Tooltip("FadeOut 시 도달하는 최대 알파. 1 = 완전 암전, 0.9 = 약간 투과.")]
    [SerializeField, Range(0f, 1f)] float maxAlpha = 0.9f;

    Coroutine _fadeCoroutine;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void OnDisable()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = Mathf.Clamp(alpha, 0f, maxAlpha);
    }

    /// <summary>화면을 어둡게 (alpha 0 → maxAlpha).</summary>
    public void FadeOut(float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(maxAlpha, duration));
    }

    /// <summary>화면을 밝게 (alpha maxAlpha → 0).</summary>
    public void FadeIn(float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(0f, duration));
    }

    IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            _fadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        _fadeCoroutine = null;
    }
}
