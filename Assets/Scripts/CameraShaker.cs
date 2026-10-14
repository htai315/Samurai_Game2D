using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng rung chấn Camera (Screen Shake) cho cả Camera thường và Unity Cinemachine.
/// Thực thi ở LateUpdate với DefaultExecutionOrder cao để chạy sau Cinemachine.
/// </summary>
[DefaultExecutionOrder(1000)]
public class CameraShaker : MonoBehaviour
{
    private static CameraShaker instance;
    private float shakeTimer;
    private float totalDuration;
    private float currentMagnitude;
    private Camera targetCamera;
    private Vector3 lastOffset, lastAppliedPosition;
    private bool offsetApplied;

    private void Update() => RemoveOffset();

    private void RemoveOffset()
    {
        // A follow camera may already have replaced the position this frame.
        if (offsetApplied && targetCamera &&
            (targetCamera.transform.position - lastAppliedPosition).sqrMagnitude < 0.000001f)
            targetCamera.transform.position -= lastOffset;
        offsetApplied = false;
    }

    private void OnDisable()
    {
        RemoveOffset();
        shakeTimer = currentMagnitude = 0f;
    }

    public static CameraShaker Instance => instance;

    private static void EnsureInstance()
    {
        if (instance != null) return;
        var go = new GameObject("CameraShaker_Manager");
        instance = go.AddComponent<CameraShaker>();
        DontDestroyOnLoad(go);
    }

    /// <summary>
    /// Kích hoạt rung màn hình.
    /// </summary>
    /// <param name="duration">Thời lượng rung (giây).</param>
    /// <param name="magnitude">Cường độ rung (độ lệch vị trí).</param>
    public static void Shake(float duration = 0.1f, float magnitude = 0.15f)
    {
        if (SceneTransition.IsBusy || Time.timeScale <= 0f) return;
        EnsureInstance();
        instance.StartShake(duration, magnitude);
    }

    public void StartShake(float duration, float magnitude)
    {
        totalDuration = Mathf.Max(0.02f, duration);
        shakeTimer = totalDuration;
        currentMagnitude = Mathf.Max(currentMagnitude, magnitude);
    }

    private void LateUpdate()
    {
        RemoveOffset();
        if (Time.timeScale <= 0f || SceneTransition.IsBusy) return;
        if (shakeTimer <= 0f) return;

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        shakeTimer -= Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(shakeTimer / totalDuration);
        float currentMag = currentMagnitude * progress;

        Vector3 offset = new Vector3(
            Random.Range(-1f, 1f) * currentMag,
            Random.Range(-1f, 1f) * currentMag,
            0f
        );

        targetCamera.transform.position += offset;
        lastOffset = offset;
        lastAppliedPosition = targetCamera.transform.position;
        offsetApplied = true;

        if (shakeTimer <= 0f)
        {
            currentMagnitude = 0f;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
