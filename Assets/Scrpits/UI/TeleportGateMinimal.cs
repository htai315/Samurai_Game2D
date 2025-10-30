using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class TeleportGateMinimal : MonoBehaviour
{
    [Header("Destination Scene")]
    [SerializeField] string targetScene = "Level2";

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }
}
