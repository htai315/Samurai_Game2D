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
    [SerializeField] private string menuSceneName = "MenuScene";
    [SerializeField] private float thankFadeIn = 1.0f;     // thời gian fade chữ
    [SerializeField] private float thankHold = 2.0f;       // giữ chữ trước khi về menu
    [SerializeField] private bool destroyPersistentRoot = true;

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
        _endingStarted = false;
        if (videoImage) videoImage.enabled = true;
        if (videoPlayer)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
            if (videoPlayer.isPrepared) OnVideoPrepared(videoPlayer);
            else videoPlayer.Prepare();
        }
        else OnVideoFinished(null);
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (isActiveAndEnabled && !_endingStarted) vp.Play();
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogWarning("[EndingPlayer] " + message);
        OnVideoFinished(vp);
    }

    private void OnDisable()
    {
        if (videoPlayer)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.Stop();
        }
        StopAllCoroutines();
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
        if (_endingStarted || !isActiveAndEnabled) return;
        _endingStarted = true;
        if (vp) vp.Stop();

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

        if (!SceneTransition.TryLoad(menuSceneName, SaveRuntime.Clear, destroyPersistentRoot))
            _endingStarted = false; // Allow retry/skip after a configuration error.
    }
}
