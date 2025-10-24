using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class TeleportGate : MonoBehaviour
{
    [Header("Destination")]
    public string targetScene = "MainGame";   // tên scene đích

    [Header("Conditions")]
    public bool requireTutorialComplete = true;
    public TutorialManager tutorialManager;
    public int requiredStage = 5;             // stage hoàn thành (khớp tutorial của bạn)

    [Header("UX (optional)")]
    public CanvasGroup fadeCanvas;            // CanvasGroup full-screen đen (optional)
    public float fadeDuration = 0.4f;

    private bool _loading;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_loading) return;
        if (!other.CompareTag("Player")) return;

        if (requireTutorialComplete && tutorialManager != null)
        {
            // Chỉ cho qua khi đã xong stage yêu cầu
            // (Bạn đang dùng stage 5 là DONE trong TutorialManager)
            var currentStageField = typeof(TutorialManager).GetField("stage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int curStage = currentStageField != null ? (int)currentStageField.GetValue(tutorialManager) : requiredStage;

            if (curStage < requiredStage) return; // chưa xong thì không dịch chuyển
        }

        StartCoroutine(LoadNext());
    }

    private IEnumerator LoadNext()
    {
        _loading = true;

        // (tuỳ chọn) Khoá input cho an toàn
        var player = GameObject.FindGameObjectWithTag("Player");
        var rb = player ? player.GetComponent<Rigidbody2D>() : null;
        if (rb) rb.linearVelocity = Vector2.zero;

        var filter = player ? player.GetComponent<TutorialInputFilter>() : null;
        if (filter) filter.Set(false, false, false, false);

        // (tuỳ chọn) hiệu ứng fade đen
        if (fadeCanvas)
        {
            fadeCanvas.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvas.alpha = Mathf.Infinity == fadeDuration ? 1f : Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            fadeCanvas.alpha = 1f;
        }
        else
        {
            // Chờ 0.2s cho cảm giác “bước vào cổng”
            yield return new WaitForSeconds(0.2f);
        }

        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }
}
