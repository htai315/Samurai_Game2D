using UnityEngine;
using UnityEngine.UI;

public class DashCooldownUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image icon;          // icon L
    [SerializeField] private Image cooldownFill;  // lớp che mờ phía trên

    [Header("Màu sắc")]
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color cooldownColor = Color.gray;

    private PlayerDash dash;

    void Start()
    {
        // Auto lấy Image trên chính object nếu chưa gán
        if (!icon) icon = GetComponent<Image>();

        // Tìm PlayerDash theo tag Player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            dash = player.GetComponent<PlayerDash>();

        // Lúc đầu không che cooldown
        if (cooldownFill)
            cooldownFill.fillAmount = 0f;
    }

    void Update()
    {
        if (!dash) return;

        float cd = dash.DashCooldown;
        float remain = dash.DashCooldownRemaining;

        bool ready = remain <= 0.001f || cd <= 0.001f;

        // Cập nhật lớp che
        if (cooldownFill)
        {
            float ratio = (cd <= 0.001f) ? 0f : remain / cd;
            cooldownFill.fillAmount = Mathf.Clamp01(ratio); // 0 = không che, 1 = che full
        }

        // Đổi màu icon
        if (icon)
            icon.color = ready ? readyColor : cooldownColor;
    }
}
