using UnityEngine;

public class PlayerLives : MonoBehaviour
{
    public static PlayerLives Instance { get; private set; }

    [Header("Cấu hình mạng")]
    [SerializeField] private int maxLives = 3;
    public int CurrentLives { get; private set; }

    [Header("UI (optional)")]
    // Bạn có thể kéo Text / Icon vào đây để hiển thị
    [SerializeField] private TMPro.TextMeshProUGUI livesText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Nếu muốn giữ mạng khi đổi scene:
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        CurrentLives = maxLives;
        UpdateUI();
    }

    /// <summary>
    /// Gọi khi player chết. Trả về true nếu SAU KHI trừ mạng vẫn còn mạng để respawn.
    /// </summary>
    public bool UseLife()
    {
        if (CurrentLives <= 0)
            return false;

        CurrentLives--;
        UpdateUI();

        // Nếu sau khi trừ mà vẫn còn > 0 → cho phép respawn
        return CurrentLives > 0;
    }
    public void ResetLives()
    {
        CurrentLives = maxLives;
    }

    private void UpdateUI()
    {
        // if (livesText)
        //     livesText.text = $"Lives: {CurrentLives}";
    }
}
