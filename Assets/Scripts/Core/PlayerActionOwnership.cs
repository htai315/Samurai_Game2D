namespace Samurai.Core.Player
{
    public enum PlayerActionKind { None, Melee, Dive, Fire, Sword, Dash, Parry, Focus, Ultimate, ParryRecovery }
    public readonly struct PlayerActionToken
    {
        internal PlayerActionOwnership Owner { get; }
        public ulong Generation { get; }
        public PlayerActionKind Kind { get; }
        internal PlayerActionToken(PlayerActionOwnership owner, ulong generation, PlayerActionKind kind)
        { Owner = owner; Generation = generation; Kind = kind; }
    }

    // One actor, one active action, at most one startup reservation. No Unity/time/resource dependencies.
    public sealed class PlayerActionOwnership
    {
        private ulong sequence;
        private PlayerActionToken active, pending;
        public PlayerActionKind CurrentAction { get; private set; }
        public bool HasReservation => pending.Owner != null;
        public bool IsOwner(PlayerActionToken token) => Same(active, token);
        private static bool Same(PlayerActionToken a, PlayerActionToken b)
            => a.Owner != null && ReferenceEquals(a.Owner, b.Owner) && a.Generation == b.Generation;
        private bool CanReplace(PlayerActionKind next) => CurrentAction == PlayerActionKind.None ||
            (next == PlayerActionKind.Dash && (CurrentAction == PlayerActionKind.Melee || CurrentAction == PlayerActionKind.Dive));
        public bool TryReserve(PlayerActionKind kind, out PlayerActionToken token)
        {
            token = default;
            if (kind <= PlayerActionKind.None || kind >= PlayerActionKind.ParryRecovery ||
                HasReservation || !CanReplace(kind) || sequence == ulong.MaxValue) return false;
            token = pending = new PlayerActionToken(this, ++sequence, kind);
            return true;
        }
        public bool Commit(PlayerActionToken token)
        {
            if (!Same(pending, token) || !CanReplace(token.Kind)) return false;
            active = token; pending = default; CurrentAction = token.Kind;
            return true;
        }
        public bool Release(PlayerActionToken token)
        {
            if (Same(pending, token)) { pending = default; return true; }
            if (!Same(active, token)) return false;
            active = default; CurrentAction = PlayerActionKind.None;
            return true;
        }
        public bool EnterParryRecovery(PlayerActionToken token)
        {
            if (!IsOwner(token) || CurrentAction != PlayerActionKind.Parry) return false;
            CurrentAction = PlayerActionKind.ParryRecovery;
            return true;
        }
        public void CancelAll()
        { active = pending = default; CurrentAction = PlayerActionKind.None; }
    }
}
