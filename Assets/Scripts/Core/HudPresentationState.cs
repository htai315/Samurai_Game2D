namespace Samurai.Core.Presentation
{
    public enum SkillHudStatus { Executing, Cooldown, InsufficientMana, Ready, Busy }

    // Read-only presentation values, not a second gameplay eligibility authority.
    public readonly struct SkillHudState
    {
        public readonly float Remaining, Duration, Cost, AvailableMana;
        public readonly bool Executing, CanAct;
        public SkillHudState(float remaining, float duration, float cost, float mana, bool executing, bool canAct)
        {
            Remaining = remaining; Duration = duration; Cost = cost; AvailableMana = mana;
            Executing = executing; CanAct = canAct;
        }
        public SkillHudStatus Status => Executing ? SkillHudStatus.Executing :
            Remaining > .01f ? SkillHudStatus.Cooldown : AvailableMana < Cost ? SkillHudStatus.InsufficientMana :
            CanAct ? SkillHudStatus.Ready : SkillHudStatus.Busy;
        public bool Ready => !Executing && Remaining <= .01f && AvailableMana >= Cost && CanAct;
    }

    public readonly struct CombatHudSnapshot
    {
        public readonly bool Visible;
        public readonly SkillHudState Dash, Sword, Fire, Ultimate;
        public CombatHudSnapshot(bool visible, SkillHudState dash, SkillHudState sword, SkillHudState fire, SkillHudState ultimate)
        { Visible = visible; Dash = dash; Sword = sword; Fire = fire; Ultimate = ultimate; }
    }

    public readonly struct ComboHudSnapshot
    {
        public readonly int Count;
        public readonly float Timer, Timeout, PunchScale, Alpha, RewardTimer;
        public readonly string RewardText;
        public ComboHudSnapshot(int count, float timer, float timeout, float punchScale, float alpha, string rewardText, float rewardTimer)
        { Count = count; Timer = timer; Timeout = timeout; PunchScale = punchScale; Alpha = alpha; RewardText = rewardText; RewardTimer = rewardTimer; }
    }
}
