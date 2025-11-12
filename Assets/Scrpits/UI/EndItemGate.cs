using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndItemGate : MonoBehaviour
{
    [Header("Ending")]
    [SerializeField] private string endingSceneName = "EndingScene";
    [SerializeField] private bool destroyPersistentRoot = true;
    [SerializeField] private string persistentRootName = "PersistentRoot";

    [Header("Transition (optional)")]
    [SerializeField] private Animator fadeAnimator;   // kéo FadeCanvas Animator nếu có
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float smallDelay = 0.15f;

    private bool triggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        StartCoroutine(GoEndingRoutine(other.gameObject));
    }

    private IEnumerator GoEndingRoutine(GameObject player)
    {
        // tắt điều khiển player để không di chuyển nữa
        var controller = player.GetComponent<PlayerController1>();
        if (controller) controller.enabled = false;

        // hiệu ứng fade (nếu có)
        if (fadeAnimator)
        {
            fadeAnimator.SetTrigger("FadeOut");
            yield return new WaitForSecondsRealtime(fadeDuration);
        }
        else
        {
            yield return new WaitForSecondsRealtime(smallDelay);
        }

        // Đảm bảo unpause âm thanh/thời gian
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // Bỏ toàn bộ thứ “đi theo” giữa scene (Player/HUD/Singletons) nếu bạn để trong PersistentRoot
        if (destroyPersistentRoot)
        {
            var root = GameObject.Find(persistentRootName);
            if (root) Destroy(root);
        }

        // Vào EndingScene chỉ để phát video (player KHÔNG đi theo)
        SceneManager.LoadScene(endingSceneName, LoadSceneMode.Single);
    }
}
