using Samurai.Core.Presentation;
using UnityEngine;

// Bind once, sample current state at render time. No cached frame, events per
// cooldown tick or gameplay writes; rebuilding the binding refreshes module refs.
public sealed class PlayerCombatHudSource
{
    private readonly PlayerController1 player;
    private readonly PlayerMana mana;
    private readonly PlayerDash dash;
    private readonly PlayerSwordSkill sword;
    private readonly PlayerFireSkill fire;
    private readonly PlayerUltimateSkill ultimate;
    public PlayerCombatHudSource(PlayerController1 player)
    {
        this.player = player;
        if (!player) return;
        mana = player.GetComponent<PlayerMana>();
        dash = player.GetComponent<PlayerDash>();
        sword = player.GetComponent<PlayerSwordSkill>();
        fire = player.GetComponent<PlayerFireSkill>();
        ultimate = player.GetComponent<PlayerUltimateSkill>();
    }
    public bool IsAvailable => player && player.isActiveAndEnabled;
    public CombatHudSnapshot Capture()
    {
        if (!IsAvailable) return default;
        float availableMana = mana ? mana.Current : 0;
        bool canAct = player.CanStartAction;
        return new CombatHudSnapshot(!player.IsDead,
            new SkillHudState(dash ? dash.DashCooldownRemaining : 0, dash ? dash.DashCooldown : 1,
                dash ? dash.ManaCost : 0, availableMana, player.IsDashing, player.CanStartDash),
            new SkillHudState(sword ? sword.SkillCooldownRemaining : 0, sword ? sword.SkillCooldown : 1,
                sword ? sword.manaCost : 0, availableMana, sword && sword.IsCasting, canAct),
            new SkillHudState(fire ? fire.SkillCooldownRemaining : 0, fire ? fire.SkillCooldown : 1,
                fire ? fire.manaCost : 0, availableMana, fire && fire.IsCasting, canAct),
            new SkillHudState(ultimate ? ultimate.CooldownRemaining : 0, ultimate ? ultimate.Cooldown : 1,
                ultimate ? ultimate.ManaCost : 0, availableMana, ultimate && ultimate.IsExecuting, canAct));
    }
    public static PlayerController1 ResolvePlayer()
    {
        var current = PlayerContext.Current?.Player;
        // Prefer the authoritative persistent root. Standalone scenes retain the
        // old fallback, but a disabled authoritative actor is never replaced.
        return current ? current : Object.FindFirstObjectByType<PlayerController1>();
    }
}
