using System;
using UnityEngine;

public enum CombatFeedbackKind { Melee, Dive, Slam, Ultimate, Parry, Deflection, SwordWave, FireballBurst, ReflectedEnemy, ReflectedBoss }

// Source-window confirmation, not one event per collider or one event per HP notification.
public readonly struct ConfirmedCombatHit
{
    public CombatFeedbackKind Kind { get; }
    public Vector3 Position { get; }
    public float RequestedDamage { get; }
    public int AcceptedTargets { get; }
    public int AttackSlot { get; }
    public bool FireTalisman { get; }
    private readonly bool awardsCombo;
    public bool AwardsCombo => awardsCombo && Kind != CombatFeedbackKind.ReflectedEnemy && Kind != CombatFeedbackKind.ReflectedBoss;
    public ConfirmedCombatHit(CombatFeedbackKind kind, Vector3 position, float damage, int acceptedTargets = 1,
        int attackSlot = 0, bool fireTalisman = false, bool awardCombo = true)
    {
        Kind = kind; Position = position; RequestedDamage = damage; AcceptedTargets = acceptedTargets;
        AttackSlot = attackSlot; FireTalisman = fireTalisman; awardsCombo = awardCombo;
    }
}

// One managed channel owned by each attacking component. No global bus, polling or scene subscriptions.
public sealed class CombatFeedbackChannel
{
    public event Action<ConfirmedCombatHit> Confirmed;
    private readonly Action<ConfirmedCombatHit> before, reward, after;
    private bool publishing;
    public CombatFeedbackChannel() : this(CombatFeedbackPresenter.BeforeReward,
        CombatFeedbackPresenter.Reward, CombatFeedbackPresenter.AfterReward) { }
    public CombatFeedbackChannel(Action<ConfirmedCombatHit> before, Action<ConfirmedCombatHit> reward,
        Action<ConfirmedCombatHit> after)
    { this.before = before; this.reward = reward; this.after = after; }
    public bool Publish(ConfirmedCombatHit hit)
        => PublishCore(hit, false);
    // Ultimate's physical stagger stays in gameplay code between impact visuals and reward.
    public bool PresentImpact(ConfirmedCombatHit hit)
    {
        if (publishing || hit.AcceptedTargets <= 0) return false;
        publishing = true;
        try { InvokeAdapter(before, hit); return true; }
        finally { publishing = false; }
    }
    public bool PublishAfterImpact(ConfirmedCombatHit hit) => PublishCore(hit, true);
    private bool PublishCore(ConfirmedCombatHit hit, bool impactPresented)
    {
        if (publishing || hit.AcceptedTargets <= 0) return false;
        publishing = true;
        try
        {
            if (!impactPresented) InvokeAdapter(before, hit);
            InvokeAdapter(reward, hit); InvokeAdapter(after, hit); Notify(Confirmed, hit);
            return true;
        }
        finally { publishing = false; }
    }
    // Each phase owns one adapter. Avoid three invocation-list allocations on every normal hit.
    private static void InvokeAdapter(Action<ConfirmedCombatHit> adapter, ConfirmedCombatHit hit)
    {
        try { adapter?.Invoke(hit); } catch (Exception error) { Debug.LogException(error); }
    }
    private static void Notify(Action<ConfirmedCombatHit> callbacks, ConfirmedCombatHit hit)
    {
        if (callbacks == null) return;
        foreach (Action<ConfirmedCombatHit> callback in callbacks.GetInvocationList())
            try { callback(hit); } catch (Exception error) { Debug.LogException(error); }
    }
}

// Compatibility presentation adapter. Attack components decide acceptance/aggregation, not this adapter.
internal static class CombatFeedbackPresenter
{
    internal static void BeforeReward(ConfirmedCombatHit hit)
    {
        switch (hit.Kind)
        {
            case CombatFeedbackKind.Melee:
                HitStop.Trigger(hit.AttackSlot == 3 ? .04f : .025f);
                CameraShaker.Shake(hit.AttackSlot == 3 ? .14f : .08f, hit.AttackSlot == 3 ? .22f : .15f);
                SoundManager.PlayHit(); CombatVFX.SpawnSlashImpactCross(hit.Position); break;
            case CombatFeedbackKind.Dive:
                HitStop.Trigger(.04f); CombatVFX.SpawnParryVFX(hit.Position); SoundManager.PlayHit(); break;
            case CombatFeedbackKind.Ultimate:
                CombatVFX.SpawnParryVFX(hit.Position); CombatVFX.SpawnFireSparks(hit.Position); break;
            case CombatFeedbackKind.Parry:
                HitStop.Trigger(.09f); CameraShaker.Shake(.2f, .3f); SoundManager.PlayParry();
                CombatVFX.SpawnParryVFX(hit.Position); break;
            case CombatFeedbackKind.Deflection:
                CombatVFX.SpawnDeflectVFX(hit.Position); CombatVFX.SpawnBladeGlint(hit.Position, .28f);
                SoundManager.PlayParry(); break;
            case CombatFeedbackKind.SwordWave:
                HitStop.Trigger(.03f); CameraShaker.Shake(.1f, .2f); break;
            case CombatFeedbackKind.FireballBurst:
                HitStop.Trigger(.05f); break;
            case CombatFeedbackKind.ReflectedEnemy:
                HitStop.Trigger(.08f); CameraShaker.Shake(.12f, .2f); break;
            case CombatFeedbackKind.ReflectedBoss:
                HitStop.Trigger(.08f); CameraShaker.Shake(.15f, .25f); break;
        }
    }
    internal static void Reward(ConfirmedCombatHit hit)
    {
        if (hit.AwardsCombo) ComboSystem.RecordHit(hit.Position, hit.RequestedDamage);
    }
    internal static void AfterReward(ConfirmedCombatHit hit)
    {
        if (hit.Kind == CombatFeedbackKind.SwordWave) CombatVFX.SpawnDeflectVFX(hit.Position);
        if (hit.Kind == CombatFeedbackKind.Melee && hit.FireTalisman) CombatVFX.SpawnFireSparks(hit.Position);
        if (hit.Kind == CombatFeedbackKind.Deflection)
        { HitStop.Trigger(.06f); CameraShaker.Shake(.08f, .15f); }
    }
}
