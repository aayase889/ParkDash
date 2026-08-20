using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Small unscaled-time press response for artwork-backed UI buttons.
/// </summary>
public sealed class SimpleButtonPressAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float pressedScale = 0.92f;
    [SerializeField] private float transitionDuration = 0.07f;

    private Coroutine scaleCoroutine;
    private Transform visualTarget;

    public void SetVisualTarget(Transform target)
    {
        visualTarget = target;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateTo(Mathf.Clamp(pressedScale, 0.75f, 1f));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateTo(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(1f);
    }

    private void OnDisable()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        Target.localScale = Vector3.one;
    }

    private void AnimateTo(float targetScale)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(targetScale));
    }

    private IEnumerator ScaleTo(float targetScale)
    {
        Transform target = Target;
        Vector3 startScale = target.localScale;
        Vector3 endScale = Vector3.one * targetScale;
        float duration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        target.localScale = endScale;
        scaleCoroutine = null;
    }

    private Transform Target => visualTarget != null ? visualTarget : transform;
}
