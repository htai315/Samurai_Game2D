using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [Header("ID duy nhất cho coin này")]
    [SerializeField] private string pickupId;   // Editor assigns a unique scene-instance ID on save.
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private int value = 1;

    private string FullId => $"{gameObject.scene.name}:{pickupId}";
    private bool consumed;

    private void OnEnable()
    {
        if (SaveRuntime.IsCollected(FullId))
            gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || !other.CompareTag("Player")) return;

        var gm = FindFirstObjectByType<GameManager>();
        if (!gm) return;
        consumed = true;
        gm.AddScore(value);

        SaveRuntime.MarkCollected(FullId);

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
