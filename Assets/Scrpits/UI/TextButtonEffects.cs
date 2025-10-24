using UnityEngine;
using UnityEngine.EventSystems;

public class TextButtonEffects : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Vector3 baseScale;

    void Awake() => baseScale = transform.localScale;

    public void OnPointerEnter(PointerEventData e) => TweenScale(1.06f, 0.08f);
    public void OnPointerExit(PointerEventData e) => TweenScale(1.00f, 0.08f);
    public void OnPointerDown(PointerEventData e) => TweenScale(0.98f, 0.05f);
    public void OnPointerUp(PointerEventData e) => TweenScale(1.06f, 0.06f);

    void TweenScale(float s, float dur)
    {
        StopAllCoroutines();
        StartCoroutine(ScaleTo(baseScale * s, dur));
    }

    System.Collections.IEnumerator ScaleTo(Vector3 target, float time)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, time);
            transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
    }
}
