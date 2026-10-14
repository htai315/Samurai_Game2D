using Samurai.Core.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;

// One context per authoritative root, not a second singleton/bootstrap or scene polling loop.
public sealed class PlayerContext : System.IDisposable
{
    private readonly PersistentRoot owner;
    private PlayerController1 player;
    private GameManager manager;
    private PlayerSaveBridge saveBridge;
    private SceneContext sceneContext;
    private bool disposed;
    internal PlayerContext(PersistentRoot owner)
    {
        this.owner = owner;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    public bool IsCurrent => !disposed && owner && owner == PersistentRoot.Instance && owner.gameObject.activeInHierarchy;
    public SceneContext Scene
    {
        get
        {
            if (!IsCurrent) return null;
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) return null;
            if (sceneContext == null || !sceneContext.IsLoaded || sceneContext.Handle != active.handle)
            {
                sceneContext?.Dispose();
                sceneContext = new SceneContext(active);
            }
            return sceneContext;
        }
    }
    private void OnSceneLoaded(Scene loaded, LoadSceneMode mode) { sceneContext?.Dispose(); sceneContext = null; }
    private void OnSceneUnloaded(Scene unloaded)
    {
        if (sceneContext != null && sceneContext.Handle == unloaded.handle) { sceneContext.Dispose(); sceneContext = null; }
    }
    public void Dispose()
    {
        disposed = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        sceneContext?.Dispose(); sceneContext = null; player = null; manager = null; saveBridge = null;
    }
    public PlayerController1 Player
    {
        get
        {
            if (!IsCurrent) return null;
            if (!player) player = owner.GetComponentInChildren<PlayerController1>(true);
            return player;
        }
    }
    public GameManager Manager
    {
        get
        {
            if (!IsCurrent) return null;
            if (!manager) manager = owner.GetComponentInChildren<GameManager>(true);
            return manager;
        }
    }
    // Snapshot ownership follows the authoritative actor, never a global tag or sibling.
    public PlayerSaveBridge SaveBridge
    {
        get
        {
            var actor = Player;
            if (!actor || !actor.gameObject.activeInHierarchy) return null;
            if (!saveBridge || saveBridge.gameObject != actor.gameObject)
                saveBridge = actor.GetComponent<PlayerSaveBridge>();
            return saveBridge;
        }
    }
    public IWallet Wallet => Manager ? Manager.WalletModel : null;
    public static PlayerContext Current => PersistentRoot.Instance ? PersistentRoot.Instance.Context : null;
    public static PlayerContext Resolve(Component participant)
    {
        if (!participant) return null;
        var root = participant.GetComponentInParent<PersistentRoot>();
        // An actor belonging to a duplicate root must never borrow the real player's wallet.
        return root ? root.Context : Current;
    }
}
