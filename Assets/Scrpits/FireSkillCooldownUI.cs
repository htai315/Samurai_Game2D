using UnityEngine;
using UnityEngine.UI;

public class FireSkillCooldownUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image icon;          // icon I
    [SerializeField] private Image cooldownFill;  // lớp mờ phía trên

    [Header("Colors")]
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color cooldownColor = Color.gray;

    private PlayerFireSkill fireSkill;

    void Start()
    {
        // Auto lấy Image của chính object nếu chưa gán
        if (!icon) icon = GetComponent<Image>();

        // Tìm PlayerFireSkill trên Player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            fireSkill = player.GetComponent<PlayerFireSkill>();

        if (cooldownFill)
            cooldownFill.fillAmount = 0f;
    }

    void Update()
    {
        if (!fireSkill) return;

        float cd = fireSkill.SkillCooldown;
        float remain = fireSkill.SkillCooldownRemaining;

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
