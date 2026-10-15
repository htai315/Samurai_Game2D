using UnityEngine;

// One accepted action owns one animation and at most one damage/projectile event.
// No reliance on a final animation event: leaving the state also releases the lock.
public sealed class PlayerActionAnimation
{
    private Animator animator;
    private string state, trigger;
    private float startedAt;
    private bool entered, emitted;
    public bool Active { get; private set; }

    public static bool CanPlay(Animator animator, string state, string trigger)
    {
        if (!animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController ||
            !animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == trigger && parameter.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    public static bool InState(Animator animator, string name)
    {
        if (!animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName(name) ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(name));
    }

    public void Begin(Animator animator, string state, string trigger)
    {
        this.animator = animator;
        this.state = state;
        this.trigger = trigger;
        startedAt = Time.time;
        entered = emitted = false;
        Active = true;
        animator.SetTrigger(trigger);
    }

    public void Tick(bool canAct, bool paused)
    {
        if (!Active || paused) return;
        if (!canAct || !animator || !animator.isActiveAndEnabled) { Cancel(); return; }
        bool inState = InState(animator, state);
        if (inState) entered = true;
        // Allow the trigger to be evaluated before treating an action as interrupted.
        if ((entered && !inState) || (!entered && Time.time - startedAt > 0.5f) ||
            Time.time - startedAt > 3f) Cancel();
    }

    public bool TryEmit(bool canAct)
    {
        if (!Active || emitted || !canAct || !InState(animator, state)) return false;
        if (animator.IsInTransition(0) && !animator.GetNextAnimatorStateInfo(0).IsName(state)) return false;
        entered = emitted = true;
        return true;
    }

    // Only the cosmetic tail can yield to a new command; never skip the strike event.
    public bool CanReleaseRecovery(float normalizedTime)
        => Active && emitted && animator && !animator.IsInTransition(0) &&
           animator.GetCurrentAnimatorStateInfo(0).IsName(state) &&
           animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= normalizedTime;

    public void Cancel()
    {
        if (animator && animator.runtimeAnimatorController && !string.IsNullOrEmpty(trigger))
            animator.ResetTrigger(trigger);
        Active = false;
    }

    public static bool ValidCost(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
