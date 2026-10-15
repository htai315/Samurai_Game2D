// PersistentRoot.cs
using UnityEngine;

public class PersistentRoot : MonoBehaviour
{
    private static PersistentRoot _instance;
    public static PersistentRoot Instance => _instance;
    private PlayerContext context;
    public PlayerContext Context => _instance == this && gameObject.activeInHierarchy
        ? context ??= new PlayerContext(this) : null;

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject); 
    }

    private void OnDestroy()
    {
        context?.Dispose();
        if (_instance == this) _instance = null;
    }
    private void Start()
    {
        if (_instance != this) return;
        SoundManager.Prewarm();
        CombatVFX.Prewarm();
    }
}
