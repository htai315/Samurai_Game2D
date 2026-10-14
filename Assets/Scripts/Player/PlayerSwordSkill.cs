using UnityEngine;
using Samurai.Core.Player;

[RequireComponent(typeof(PlayerMana))]
public class PlayerSwordSkill : MonoBehaviour
{
    private PlayerActionToken actionToken;
    public GameObject swordWavePrefab;
    public Transform spawnPoint;
    public float manaCost = 5f;
    [SerializeField] private float skillCooldown = 1.0f;
    private PlayerMana mana;
    private PlayerController1 controller;
    private Animator anim;
    private bool isUsingSkill;
    private float nextCastTime = 0f;
    private readonly PlayerActionAnimation action = new();

    private void Awake()
    {
        mana = GetComponent<PlayerMana>();
        controller = GetComponent<PlayerController1>();
        anim = GetComponent<Animator>();
    }

    public void TickAction()
    {
        action.Tick(controller && controller.CanAct && controller.OwnsAction(actionToken), Time.timeScale == 0f);
        if (!action.Active && controller) controller.ReleaseAction(actionToken);
        isUsingSkill = action.Active;
    }
    public PlayerInputCommands ReadInputCommands()
        => Input.GetKeyDown(KeyCode.U) ? PlayerInputCommands.Sword : 0;
    public void HandleInput()
    {
        if ((ReadInputCommands() & PlayerInputCommands.Sword) != 0) TryUseSwordSkill();
    }

    public bool TryUseSwordSkill()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction || !(Time.time >= nextCastTime) ||
            !swordWavePrefab || !spawnPoint || !swordWavePrefab.GetComponent<SwordProjectile>() ||
            !swordWavePrefab.GetComponent<Rigidbody2D>() || !PlayerActionAnimation.ValidCost(skillCooldown) ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Skill1", "doSkillWave") ||
            !mana) return false;
        if (!controller.TryStartOwnedAction(PlayerActionKind.Sword, mana, manaCost, out actionToken)) return false;

        isUsingSkill = true;
        nextCastTime = Time.time + skillCooldown;
        action.Begin(anim, "Player_Skill1", "doSkillWave");
        var body = GetComponent<Rigidbody2D>();
        if (body) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        return true;
    }

    // Animation event: only the accepted, still-active cast may produce one projectile.
    public void ShootSwordWave()
    {
        if (!isActiveAndEnabled || !swordWavePrefab || !spawnPoint ||
            !action.TryEmit(controller && controller.CanAct && controller.OwnsAction(actionToken))) return;
        float direction = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 position = spawnPoint.position;
        position.x = transform.position.x + Mathf.Abs(spawnPoint.localPosition.x) * direction;
        var projectile = Instantiate(swordWavePrefab, position, Quaternion.identity).GetComponent<SwordProjectile>();
        projectile.SetDirection(direction);
        projectile.Launch();
        SoundManager.PlaySwordWave();

        // 🌟 VFX Kiếm Khí Uy Lực: Vòng sóng áp suất ở mũi kiếm + bụi giật lùi sau gót chân + camera rung
        CombatVFX.SpawnParryVFX(position);
        CombatVFX.SpawnDust(transform.position + new Vector3(-direction * 0.35f, -0.55f, 0f), new Vector3(-direction, 1f, 1f));
        CameraShaker.Shake(0.08f, 0.14f);
    }

    private void OnDisable() => ResetForRespawn(false);
    public bool IsCasting => isUsingSkill;
    public float SkillCooldown => skillCooldown;
    public float SkillCooldownRemaining => Mathf.Max(0f, nextCastTime - Time.time);
    public bool IsSkillReady => !isUsingSkill && Time.time >= nextCastTime;

    public void ResetForRespawn(bool newAttempt)
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        action.Cancel();
        isUsingSkill = false;
        if (newAttempt) nextCastTime = 0f;
    }
}
