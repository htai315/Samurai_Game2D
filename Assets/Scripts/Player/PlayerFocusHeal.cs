using UnityEngine;
using Samurai.Core.Player;

public class PlayerFocusHeal : MonoBehaviour
{
    private PlayerActionToken actionToken;
    [Header("Focus Heal Configuration")]
    [SerializeField] private float manaCostPerPulse = 15f;
    [SerializeField] private float healAmountPerPulse = 2f;
    [SerializeField] private float focusChargeTime = 0.85f;

    private PlayerController1 controller;
    private PlayerHealth health;
    private PlayerMana mana;
    private PlayerJump jump;
    private Rigidbody2D rb;
    private Animator anim;

    private bool isFocusing = false;
    private float currentChargeTimer = 0f;
    private float gatherVfxTimer = 0f;

    public bool IsFocusing => isFocusing;

    public void Initialize(PlayerController1 ctrl, PlayerHealth hp, PlayerMana mp, PlayerJump jp, Rigidbody2D body, Animator animator)
    {
        controller = ctrl;
        health = hp;
        mana = mp;
        jump = jp;
        rb = body;
        anim = animator;
    }

    private void Awake()
    {
        controller = GetComponent<PlayerController1>();
        health = GetComponent<PlayerHealth>();
        mana = GetComponent<PlayerMana>();
        jump = GetComponent<PlayerJump>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    public PlayerInputCommands ReadInputCommands()
        => (Input.GetKey(KeyCode.H) || Input.GetKey(KeyCode.E)) ? PlayerInputCommands.FocusHeld : 0;
    public void HandleInput() => HandleFocusInput((ReadInputCommands() & PlayerInputCommands.FocusHeld) != 0);
    public void HandleFocusInput(bool holdFocusKey)
    {
        if (!controller || !controller.CanAct || (isFocusing && !controller.OwnsAction(actionToken)))
        {
            if (isFocusing) CancelFocus();
            return;
        }


        // Bắt đầu hoặc tiếp tục Thiền Tụ Khí
        if (holdFocusKey)
        {
            bool canFocus = (!isFocusing ? controller.CanStartAction : true) &&
                            jump != null && jump.IsGrounded &&
                            health != null && health.CurrentHealth < health.MaxHealth &&
                            mana != null && mana.Current >= manaCostPerPulse;

            if (canFocus)
            {
                if (!isFocusing)
                {
                    if (!StartFocus()) return;
                }
                UpdateFocus();
            }
            else
            {
                if (isFocusing) CancelFocus();
            }
        }
        else
        {
            if (isFocusing) CancelFocus();
        }
    }

    private bool StartFocus()
    {
        if (!controller.TryStartOwnedAction(PlayerActionKind.Focus, out actionToken)) return false;
        isFocusing = true;
        currentChargeTimer = 0f;
        gatherVfxTimer = 0f;

        if (rb) rb.linearVelocity = Vector2.zero;
        if (anim) anim.Play("Player_Defence");

        SoundManager.PlayBladeGlint();
        return true;
    }

    private void UpdateFocus()
    {
        if (rb) rb.linearVelocity = Vector2.zero;

        currentChargeTimer += Time.deltaTime;
        gatherVfxTimer -= Time.deltaTime;

        // Tụ hạt sáng lân tinh về ngực
        if (gatherVfxTimer <= 0f)
        {
            gatherVfxTimer = 0.25f;
            CombatVFX.SpawnChargeGather(transform.position + Vector3.up * 0.25f, 0.22f);
        }

        // Hoàn tất 1 chu kỳ tụ khí hồi máu!
        if (currentChargeTimer >= focusChargeTime)
        {
            currentChargeTimer = 0f;
            TriggerHealPulse();
        }
    }

    private void TriggerHealPulse()
    {
        if (!mana.TrySpend(manaCostPerPulse))
        {
            CancelFocus();
            return;
        }

        health.Heal(healAmountPerPulse);

        // 🌟 VFX Sóng hồi phục thanh tịnh xanh ngọc & Lóe sáng tim
        CombatVFX.SpawnParryVFX(transform.position);
        CombatVFX.SpawnBladeGlint(transform.position + Vector3.up * 0.35f, 0.28f);
        CameraShaker.Shake(0.05f, 0.1f);
        SoundManager.PlayBladeGlint();

        // Nếu đã đầy máu thì ngừng thiền
        if (health.CurrentHealth >= health.MaxHealth)
        {
            CancelFocus();
        }
    }

    public void CancelFocus()
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        if (!isFocusing) return;
        isFocusing = false;
        currentChargeTimer = 0f;
        if (anim && anim.runtimeAnimatorController)
        {
            anim.Play("Player_Idle");
        }
    }

    private void OnDisable()
    {
        CancelFocus();
    }

    // Vẽ thanh tiến độ tụ khí trên đầu Samurai khi đang thiền
    private GUIStyle healLabelStyle;

    private void OnGUI()
    {
        if (!isFocusing) return;

        var cam = Camera.main;
        if (!cam) return;

        Vector3 headPos = transform.position + Vector3.up * 1.25f;
        Vector3 screenPos = cam.WorldToScreenPoint(headPos);
        if (screenPos.z < 0) return;

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.8f);

        float width = 130f * scale;
        float height = 11f * scale;
        float x = screenPos.x - width * 0.5f;
        float y = Screen.height - screenPos.y;

        float pct = Mathf.Clamp01(currentChargeTimer / focusChargeTime);

        Color orig = GUI.color;

        // 1. Chữ tiêu đề thiền định trên đầu thanh máu
        int labelSize = Mathf.RoundToInt(13.5f * scale);
        if (healLabelStyle == null || healLabelStyle.fontSize != labelSize)
        {
            healLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = SamuraiUITheme.GetSamuraiFont(labelSize),
                fontSize = labelSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        healLabelStyle.normal.textColor = Color.Lerp(new Color(0.35f, 0.95f, 0.75f, 0.95f), new Color(1f, 1f, 1f, 1f), pct);
        GUI.Label(new Rect(x - 20f * scale, y - 22f * scale, width + 40f * scale, 20f * scale), "「 靜 心 」 VẬN KHÍ", healLabelStyle);

        // 2. Nền đen sơn mài sâu thẳm
        GUI.color = new Color(0.06f, 0.055f, 0.08f, 0.92f);
        GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);

        // 3. Viền vàng kim tinh tế
        GUI.color = new Color(1f, 0.85f, 0.35f, 0.85f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, width + 2, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1, y + height, width + 2, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1, y - 1, 1f, height + 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + width, y - 1, 1f, height + 2), Texture2D.whiteTexture);

        // 4. Thanh tiến độ màu ngọc bích phát sáng (Jade Qi Energy)
        GUI.color = Color.Lerp(new Color(0.18f, 0.78f, 0.55f), new Color(0.45f, 1f, 0.85f), pct);
        GUI.DrawTexture(new Rect(x, y, width * pct, height), Texture2D.whiteTexture);

        GUI.color = orig;
    }
}
