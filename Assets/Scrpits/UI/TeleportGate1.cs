using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class TeleportGate1 : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string targetScene = "Level2";
    [SerializeField] private string targetSpawnId = "A"; // ID điểm spawn bên map đích

    [Header("Transition (optional)")]
    [SerializeField] private CanvasGroup fadeCanvas; // CanvasGroup full-screen đen (optional)
    [SerializeField] private float fadeDuration = 0.4f; // thời gian fade mờ
    [SerializeField] private float delayBeforeTeleport = 0.2f; // khoảng chờ nhỏ trước khi dịch chuyển

    private bool _loading = false;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // đảm bảo có thể đi xuyên qua
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_loading) return;
        if (!other.CompareTag("Player")) return;

        // Ghi nhớ ID spawn cho scene kế tiếp
        SpawnLocator.NextSpawnId = targetSpawnId;

        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        _loading = true;

        // Nếu có hiệu ứng fade, thực hiện fade-out
        if (fadeCanvas)
        {
            fadeCanvas.blocksRaycasts = true;
            yield return StartCoroutine(FadeCanvas(fadeCanvas, 0f, 1f, fadeDuration)); // fade ra đen
        }
        else
        {
            yield return new WaitForSeconds(delayBeforeTeleport);
        }

        // Load scene mới (async để có thể chờ và fade in mượt)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Khi scene mới load xong → tìm lại fadeCanvas mới trong scene đích để fade in
        if (fadeCanvas)
        {
            // Chờ 1 frame để đảm bảo scene mới được kích hoạt hoàn toàn
            yield return null;

            // Tìm CanvasGroup trong scene mới (nếu có)
            CanvasGroup newFadeCanvas = Object.FindAnyObjectByType<CanvasGroup>();
            if (newFadeCanvas)
            {
                newFadeCanvas.alpha = 1f; // đảm bảo bắt đầu đen hoàn toàn
                yield return StartCoroutine(FadeCanvas(newFadeCanvas, 1f, 0f, fadeDuration)); // fade sáng dần
                newFadeCanvas.blocksRaycasts = false;
            }
        }

        _loading = false;
    }

    private IEnumerator FadeCanvas(CanvasGroup canvas, float from, float to, float duration)
    {
        float t = 0f;
        canvas.alpha = from;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvas.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        canvas.alpha = to;
    }
}
