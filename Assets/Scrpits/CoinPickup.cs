using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [Header("ID duy nhất cho coin này")]
    [SerializeField] private string pickupId;   // để trống sẽ tự sinh GUID
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private int value = 1;

    private string FullId => $"{gameObject.scene.name}:{pickupId}";

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(pickupId))
        {
            pickupId = System.Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    private void OnEnable()
    {
        if (SaveRuntime.IsCollected(FullId))
            gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var gm = FindFirstObjectByType<GameManager>();
        gm?.AddScore(value);

        SaveRuntime.MarkCollected(FullId);

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
