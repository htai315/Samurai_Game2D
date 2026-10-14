using UnityEngine;
using Samurai.Core.Player;

[RequireComponent(typeof(PlayerMana))]
public class PlayerFireSkill : MonoBehaviour
{
    private PlayerActionToken actionToken;
    public GameObject fireballPrefab;
    public Transform castPoint;
    public float manaCost = 10f;
    public float cooldown = 0.8f;
    private PlayerMana mana;
    private PlayerController1 controller;
    private Animator anim;
    private bool isCasting;
    private float lastCastTime = -999f;
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
        isCasting = action.Active;
    }
    public PlayerInputCommands ReadInputCommands()
        => Input.GetKeyDown(KeyCode.I) ? PlayerInputCommands.Fire : 0;
    public void HandleInput()
    {
        if ((ReadInputCommands() & PlayerInputCommands.Fire) != 0) TryCastFire();
    }

    public bool TryCastFire()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction || !(Time.time >= lastCastTime + cooldown) ||
            !fireballPrefab || !castPoint || !fireballPrefab.GetComponent<FireballController>() ||
            !fireballPrefab.GetComponent<Rigidbody2D>() || !PlayerActionAnimation.ValidCost(cooldown) ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Fire_Skill", "doCastFire") ||
            !mana) return false;
        if (!controller.TryStartOwnedAction(PlayerActionKind.Fire, mana, manaCost, out actionToken)) return false;

        isCasting = true;
        lastCastTime = Time.time;
        action.Begin(anim, "Player_Fire_Skill", "doCastFire");
        var body = GetComponent<Rigidbody2D>();
        if (body) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        return true;
    }

    // Animation event: only the accepted, still-active cast may produce one projectile.
    public void SpawnFireball()
    {
        if (!isActiveAndEnabled || !fireballPrefab || !castPoint ||
            !action.TryEmit(controller && controller.CanAct && controller.OwnsAction(actionToken))) return;
        float direction = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 position = castPoint.position;
        var projectile = Instantiate(fireballPrefab, position, Quaternion.identity).GetComponent<FireballController>();
        projectile.SetDirection(direction);

        // 🌟 VFX Hỏa Diệm Chưởng: Chùm tia lửa lòng bàn tay + bụi giật lùi gót chân + camera rung
        CombatVFX.SpawnFireSparks(position);
        CombatVFX.SpawnDust(transform.position + new Vector3(-direction * 0.35f, -0.55f, 0f), new Vector3(-direction, 1f, 1f));
        CameraShaker.Shake(0.08f, 0.14f);
        SoundManager.PlayFireball();
    }

    private void OnDisable() => ResetForRespawn(false);
    public bool IsCasting => isCasting;
    public float SkillCooldown => cooldown;
    public float SkillCooldownRemaining => Mathf.Max(0f, lastCastTime + cooldown - Time.time);
    public bool IsSkillReady => !isCasting && Time.time >= lastCastTime + cooldown;

    public void ResetForRespawn(bool newAttempt)
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        action.Cancel();
        isCasting = false;
        if (newAttempt) lastCastTime = -999f;
    }
}
