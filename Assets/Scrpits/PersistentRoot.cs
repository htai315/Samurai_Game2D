// PersistentRoot.cs
using UnityEngine;

public class PersistentRoot : MonoBehaviour
{
    private static PersistentRoot _instance;
    public static PersistentRoot Instance => _instance;

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject); 
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
