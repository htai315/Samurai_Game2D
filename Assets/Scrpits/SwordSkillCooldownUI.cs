using UnityEngine;
using UnityEngine.UI;

public class SwordSkillCooldownUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image icon;          // icon U
    [SerializeField] private Image cooldownFill;  // lớp che mờ

    [Header("Màu sắc")]
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color cooldownColor = Color.gray;

    private PlayerSwordSkill swordSkill;

    private void Start()
    {
        // Auto lấy Image trên chính object nếu chưa gán
        if (!icon) icon = GetComponent<Image>();

        // Tìm PlayerSwordSkill trên Player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            swordSkill = player.GetComponent<PlayerSwordSkill>();

        if (cooldownFill)
            cooldownFill.fillAmount = 0f;
    }

    private void Update()
    {
        if (!swordSkill) return;

        float cd = swordSkill.SkillCooldown;
        float remain = swordSkill.SkillCooldownRemaining;

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
