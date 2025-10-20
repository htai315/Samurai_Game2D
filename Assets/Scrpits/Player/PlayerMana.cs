using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [Header("Mana")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float startMana = 0f;
    [SerializeField] private ThanhMana thanhMana; // 🌟 thêm thanh mana

    public float Current { get; private set; }
    public float Max => maxMana;

    private void Awake()
    {
        Current = Mathf.Clamp(startMana, 0f, maxMana);
        if (thanhMana)
            thanhMana.capNhatMana(Current, Max);
    }

    public void AddMana(float amount)
    {
        if (amount <= 0f) return;
        Current = Mathf.Min(Current + amount, maxMana);

        // Cập nhật UI
        if (thanhMana)
            thanhMana.capNhatMana(Current, Max);

        Debug.Log($"[PlayerMana] +{amount} mana → {Current}/{maxMana}");
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f || Current < amount) return false;
        Current -= amount;

        // Cập nhật UI
        if (thanhMana)
            thanhMana.capNhatMana(Current, Max);

        Debug.Log($"[PlayerMana] -{amount} mana → {Current}/{maxMana}");
        return true;
    }
}
