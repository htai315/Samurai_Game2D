using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [Header("Mana")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float startMana = 0f;
    [SerializeField] private ThanhMana thanhMana; // 🌟 thêm thanh mana

    public float Current { get; private set; }
    public float Max => maxMana;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        if (!Finite(maxMana) || maxMana <= 0f) maxMana = 100f;
        if (!Finite(startMana)) startMana = 0f;
        Current = Mathf.Clamp(startMana, 0f, maxMana);
        if (thanhMana)
            thanhMana.capNhatMana(Current, Max);
    }

    public void AddMana(float amount)
    {
        EnsureInitialized();
        if (!Finite(amount) || amount <= 0f) return;
        Current += Mathf.Min(amount, maxMana - Current);

        // Cập nhật UI
        if (thanhMana)
            thanhMana.capNhatMana(Current, Max);

        Debug.Log($"[PlayerMana] +{amount} mana → {Current}/{maxMana}");
    }

    public bool TrySpend(float amount)
    {
        EnsureInitialized();
        if (!Finite(Current) || !Finite(maxMana) || maxMana <= 0f ||
            !PlayerActionAnimation.ValidCost(amount) || Current < amount) return false;
        Current -= amount;

        // Cập nhật UI
        if (thanhMana)
            thanhMana.capNhatMana(Current, Max);

        Debug.Log($"[PlayerMana] -{amount} mana → {Current}/{maxMana}");
        return true;
    }
    // PlayerMana.cs (bổ sung 2 API bên dưới trong class)
    public void ApplyNewMaxMana(float newMax, bool keepRatio)
    {
        EnsureInitialized();
        if (!Finite(newMax) || newMax <= 0f) return;
        float oldMax = maxMana;
        float oldCur = Current;

        maxMana = Mathf.Max(0.01f, newMax);

        float targetCur = keepRatio && oldMax > 0.0001f
            ? Mathf.Clamp01(oldCur / oldMax) * maxMana
            : Mathf.Min(oldCur, maxMana);

        Current = targetCur; // setter private nhưng trong class được phép set
        if (thanhMana) thanhMana.capNhatMana(Current, Max);
    }

    public void SetCurrentMana(float value)
    {
        EnsureInitialized();
        if (!Finite(value)) return;
        Current = Mathf.Clamp(value, 0, maxMana);
        if (thanhMana) thanhMana.capNhatMana(Current, Max);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
