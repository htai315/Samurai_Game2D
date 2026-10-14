namespace Samurai.Core.Player
{
    [System.Flags]
    public enum PlayerInputCommands
    {
        None = 0, FocusHeld = 1, Parry = 2, Ultimate = 4, Dash = 8, JumpPressed = 16,
        JumpReleased = 32, Attack = 64, AttackUpHeld = 128, AttackDownHeld = 256, Fire = 512, Sword = 1024
    }
    public readonly struct PlayerInputFrame
    {
        public float Movement { get; }
        public PlayerInputCommands Commands { get; }
        public PlayerInputFrame(float movement, PlayerInputCommands commands)
        {
            Movement = float.IsNaN(movement) || float.IsInfinity(movement) ? 0 :
                movement < -1 ? -1 : movement > 1 ? 1 : movement;
            Commands = commands;
        }
        public bool Has(PlayerInputCommands command) => (Commands & command) != 0;
    }
    public interface IPlayerInputSink
    {
        void Move(float axis);
        bool Focus(bool held);
        bool Parry();
        bool Ultimate();
        bool Dash();
        void Jump(bool pressed, bool released);
        bool Attack(bool upHeld, bool downHeld);
        bool Fire();
        bool Sword();
    }
    public static class PlayerInputArbitration
    {
        // Preserve controller priority; previously unordered standalone casts come last, fire before sword.
        // Only successful requests consume priority. Jump sensing/buffering always runs at its old slot.
        public static PlayerInputCommands Dispatch(PlayerInputFrame frame, IPlayerInputSink sink)
        {
            sink.Move(frame.Movement);
            PlayerInputCommands winner = PlayerInputCommands.None;
            if (sink.Focus(frame.Has(PlayerInputCommands.FocusHeld))) winner = PlayerInputCommands.FocusHeld;
            if (winner == 0 && frame.Has(PlayerInputCommands.Parry) && sink.Parry()) winner = PlayerInputCommands.Parry;
            if (winner == 0 && frame.Has(PlayerInputCommands.Ultimate) && sink.Ultimate()) winner = PlayerInputCommands.Ultimate;
            if (winner == 0 && frame.Has(PlayerInputCommands.Dash) && sink.Dash()) winner = PlayerInputCommands.Dash;
            sink.Jump(frame.Has(PlayerInputCommands.JumpPressed), frame.Has(PlayerInputCommands.JumpReleased));
            if (winner == 0 && frame.Has(PlayerInputCommands.Attack) &&
                sink.Attack(frame.Has(PlayerInputCommands.AttackUpHeld), frame.Has(PlayerInputCommands.AttackDownHeld)))
                winner = PlayerInputCommands.Attack;
            if (winner == 0 && frame.Has(PlayerInputCommands.Fire) && sink.Fire()) winner = PlayerInputCommands.Fire;
            if (winner == 0 && frame.Has(PlayerInputCommands.Sword) && sink.Sword()) winner = PlayerInputCommands.Sword;
            return winner;
        }
    }
}
