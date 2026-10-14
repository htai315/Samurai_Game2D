using Samurai.Core.Time;
using UnityEngine;

// The sole production timeScale writer. No additional Update loop or coroutine.
public static class GameTime
{
    private static TimeScaleOwnership ownership = new TimeScaleOwnership();
    public static bool TryAcquire(float scale, out TimeScaleLease lease)
    {
        if (!ownership.TryAcquire(Time.timeScale, scale, out lease)) return false;
        Time.timeScale = scale;
        return true;
    }
    public static bool Owns(TimeScaleLease lease) => ownership.Owns(lease, Time.timeScale);
    public static void Release(TimeScaleLease lease)
    {
        float observed = Time.timeScale;
        if (ownership.TryRelease(lease, observed, out float restored) && restored != observed)
            Time.timeScale = restored;
    }
    public static void ResetForSceneBoundary()
    {
        ownership.Invalidate();
        Time.timeScale = 1f;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        // Old tokens keep their old scope and cannot affect the new play session.
        ownership = new TimeScaleOwnership();
    }
}
