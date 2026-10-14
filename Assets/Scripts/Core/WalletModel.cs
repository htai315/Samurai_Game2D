using System;

namespace Samurai.Core.Economy
{
    public interface IWallet
    {
        int Balance { get; }
        event Action<int> BalanceChanged;
        bool TryCredit(int amount);
        bool TrySpend(int amount);
        bool TrySetBalance(int value);
    }

    // Runtime economy state only. No Unity, scene, save or presentation dependencies.
    public sealed class WalletModel : IWallet
    {
        public int Balance { get; private set; }
        public event Action<int> BalanceChanged;
        public event Action<Exception> ObserverFaulted;
        private bool notifying;
        public bool TryCredit(int amount)
        {
            if (notifying || amount < 0 || Balance > int.MaxValue - amount) return false;
            return Commit(Balance + amount);
        }
        public bool TrySpend(int amount)
        {
            if (notifying || amount < 0 || amount > Balance) return false;
            return Commit(Balance - amount);
        }
        public bool TrySetBalance(int value) => !notifying && Commit(Math.Max(0, value));
        private bool Commit(int value)
        {
            if (value == Balance) return true;
            Balance = value;
            notifying = true;
            try
            {
                if (BalanceChanged == null) return true;
                foreach (Action<int> observer in BalanceChanged.GetInvocationList())
                {
                    try { observer(value); }
                    catch (Exception error)
                    {
                        if (ObserverFaulted == null) continue;
                        foreach (Action<Exception> diagnostic in ObserverFaulted.GetInvocationList())
                            try { diagnostic(error); } catch { /* Diagnostics cannot undo a committed balance. */ }
                    }
                }
                return true;
            }
            finally { notifying = false; }
        }
    }
}
