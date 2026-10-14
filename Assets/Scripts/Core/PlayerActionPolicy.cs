using System;

namespace Samurai.Core.Player
{
    [Flags]
    public enum PlayerGate
    {
        None = 0, Inactive = 1, Dead = 2, Hurt = 4, Paused = 8, Transition = 16, Respawning = 32, ActionReservation = 64
    }
    [Flags]
    public enum PlayerAction
    {
        None = 0, Dash = 1, Melee = 2, Cast = 4, Parry = 8, Focus = 16, Ultimate = 32, ParryRecovery = 64
    }

    // Immutable current snapshot, not a cached frame or a second mutable state machine.
    // Unity adapters supply lifecycle, active module and animator facts.
    public readonly struct PlayerActionState
    {
        public PlayerGate Gates { get; }
        public PlayerAction Actions { get; }
        public bool ActionAnimationBlocked { get; }
        public bool DashAnimationBlocked { get; }
        private const PlayerGate ContinueBlockers = PlayerGate.Inactive | PlayerGate.Dead | PlayerGate.Hurt |
            PlayerGate.Transition | PlayerGate.Respawning;
        private const PlayerGate PickupBlockers = PlayerGate.Inactive | PlayerGate.Dead | PlayerGate.Paused |
            PlayerGate.Transition | PlayerGate.Respawning;
        private const PlayerAction StartBlockers = PlayerAction.Dash | PlayerAction.Melee | PlayerAction.Cast |
            PlayerAction.Parry | PlayerAction.Focus | PlayerAction.Ultimate | PlayerAction.ParryRecovery;
        private const PlayerAction DashBlockers = StartBlockers & ~PlayerAction.Melee;
        private const PlayerAction LocomotionBlockers = StartBlockers & ~PlayerAction.ParryRecovery;
        public PlayerActionState(PlayerGate gates, PlayerAction actions, bool actionAnimationBlocked = false,
            bool dashAnimationBlocked = false)
        {
            Gates = gates; Actions = actions; ActionAnimationBlocked = actionAnimationBlocked;
            DashAnimationBlocked = dashAnimationBlocked;
        }
        public PlayerActionState WithAnimationBlocks(bool action, bool dash)
            => new PlayerActionState(Gates, Actions, action, dash);
        public bool CanContinueAction => (Gates & ContinueBlockers) == 0;
        public bool CanAct => CanContinueAction && (Gates & PlayerGate.Paused) == 0 && (Actions & PlayerAction.Ultimate) == 0;
        // Hurt and ultimate did not block pickups; do not silently rebalance this.
        public bool CanCollectPickups => (Gates & PickupBlockers) == 0;
        public bool CanStartAction => CanAct && (Gates & PlayerGate.ActionReservation) == 0 &&
            (Actions & StartBlockers) == 0 && !ActionAnimationBlocked;
        // Explicit melee-to-dash cancellation exception.
        public bool CanStartDash => CanAct && (Gates & PlayerGate.ActionReservation) == 0 &&
            (Actions & DashBlockers) == 0 && !DashAnimationBlocked;
        // Recovery blocks a new action, not the existing walking presentation/motor.
        public bool CanLocomote => CanAct && (Gates & PlayerGate.ActionReservation) == 0 && (Actions & LocomotionBlockers) == 0;
    }
}
