using UnityEngine;
using Samurai.Core.Time;

/// <summary>
/// Quản lý hiệu ứng Hit Stop (micro-pause / micro-slowdown) khi đòn chém hoặc kỹ năng trúng mục tiêu,
/// tạo cảm giác "chém có trọng lượng" và tăng độ đã tay (Combat Juice).
/// </summary>
public class HitStop : MonoBehaviour
{
    private static HitStop instance;
    private bool isStopping;
    private TimeScaleLease timeLease;
    private float stopUntil;
    private const float SlowScale = 0.18f;
    private float savedFixedStep, ownedFixedStep;

    public static bool IsActive => instance != null && instance.isStopping;

    private static void EnsureInstance()
    {
        if (instance != null) return;
        var go = new GameObject("HitStop_Manager");
        instance = go.AddComponent<HitStop>();
        DontDestroyOnLoad(go);
    }

    /// <summary>
    /// Kích hoạt hiệu ứng Hit Stop trong một khoảng thời gian ngắn (tính theo thời gian thực).
    /// </summary>
    /// <param name="duration">Thời gian micro-slowdown (mặc định 0.05s).</param>
    public static void Trigger(float duration = 0.05f)
    {
        // Không kích hoạt nếu game đang Pause thật sự hoặc đang chuyển màn
        if (Time.timeScale <= 0f || SceneTransition.IsBusy || float.IsNaN(duration) || float.IsInfinity(duration)) return;

        EnsureInstance();
        instance.StartHitStop(Mathf.Clamp(duration, 0.01f, 0.2f));
    }

    private void StartHitStop(float duration)
    {
        if (!isStopping)
        {
            if (!GameTime.TryAcquire(SlowScale, out timeLease)) return;
            savedFixedStep = Time.fixedDeltaTime;
            // Preserve real-time physics cadence during slowdown, not a half-second tick.
            ownedFixedStep = savedFixedStep * SlowScale;
            Time.fixedDeltaTime = ownedFixedStep;
            isStopping = true;
        }
        stopUntil = Mathf.Max(stopUntil, Time.realtimeSinceStartup + duration);
    }

    private void Update()
    {
        if (!isStopping) return;
        if (!GameTime.Owns(timeLease) || SceneTransition.IsBusy)
            Cancel();
        else if (Time.realtimeSinceStartup >= stopUntil)
            Cancel();
    }

    // Call before another system takes ownership of timeScale.
    public static void Cancel()
    {
        if (!instance || !instance.isStopping) return;
        GameTime.Release(instance.timeLease);
        // Unity quantizes this native timer; a microsecond tolerance avoids a false
        // ownership loss after setting small fixed intervals.
        if (Mathf.Abs(Time.fixedDeltaTime - instance.ownedFixedStep) <= .000001f)
            Time.fixedDeltaTime = instance.savedFixedStep;
        instance.timeLease = default;
        instance.isStopping = false;
        instance.stopUntil = 0f;
    }

    private void OnDisable() { if (instance == this) Cancel(); }

    private void OnDestroy()
    {
        if (instance == this) { Cancel(); instance = null; }
    }
}
