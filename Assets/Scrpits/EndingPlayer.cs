using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class EndingPlayer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private VideoPlayer videoPlayer;      // kéo VideoPlayer
    [SerializeField] private RawImage videoImage;          // kéo RawImage hiển thị video
    [SerializeField] private CanvasGroup thankGroup;       // CanvasGroup chứa “Thank you for playing”

    [Header("Flow")]
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private float thankFadeIn = 1.0f;     // thời gian fade chữ
    [SerializeField] private float thankHold = 2.0f;       // giữ chữ trước khi về menu
    [SerializeField] private bool destroyPersistentRoot = true;
    [SerializeField] private string persistentRootName = "PersistentRoot";

    [Header("Skip")]
    [SerializeField] private bool allowSkip = true;        // cho phép bấm để bỏ qua
    [SerializeField] private KeyCode skipKey = KeyCode.Escape; // phím skip

    private bool _endingStarted;

    private void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    private void Awake()
    {
        // đảm bảo thời gian chạy bình thường
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (thankGroup)
        {
            thankGroup.alpha = 0f;
            thankGroup.interactable = false;
            thankGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        if (videoPlayer)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            // bắt đầu phát ngay khi ready
            if (!videoPlayer.isPrepared)
                videoPlayer.Prepare();
            videoPlayer.prepareCompleted += _ => videoPlayer.Play();
        }
    }

    private void OnDisable()
    {
        if (videoPlayer)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void Update()
    {
        if (allowSkip && !_endingStarted && Input.GetKeyDown(skipKey))
        {
            // Bỏ qua video
            OnVideoFinished(videoPlayer);
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (_endingStarted) return;
        _endingStarted = true;

        // dừng output video để tránh khung hình cuối đè lên text
        if (videoImage) videoImage.enabled = false;

        StartCoroutine(ShowThanksThenGoMenu());
    }

    private IEnumerator ShowThanksThenGoMenu()
    {
        // Fade in “Thank you for playing”
        if (thankGroup)
        {
            thankGroup.gameObject.SetActive(true);
            float t = 0f;
            while (t < thankFadeIn)
            {
                t += Time.unscaledDeltaTime;
                thankGroup.alpha = Mathf.Lerp(0f, 1f, t / thankFadeIn);
                yield return null;
            }
            thankGroup.alpha = 1f;
        }

        // Giữ một lát
        yield return new WaitForSecondsRealtime(thankHold);

        // (tuỳ chọn) dọn singleton để về menu sạch sẽ
        if (destroyPersistentRoot)
        {
            var root = GameObject.Find(persistentRootName);
            if (root) Destroy(root);
        }

        // Về Menu
        SceneManager.LoadScene(menuSceneName);
    }
}
