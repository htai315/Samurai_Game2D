using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;   // 🎯 Thay vì "using Cinemachine;"

public class CinemachineAutoBinder : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";
    [SerializeField] string boundsObjectName = "CameraBounds";

    CinemachineCamera vcam;                // 🎯 v3: dùng CinemachineCamera
    CinemachineConfiner2D confiner;

    void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        confiner = GetComponent<CinemachineConfiner2D>();
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start() => Rebind();
    void OnSceneLoaded(Scene s, LoadSceneMode m) => Rebind();

    void Rebind()
    {
        // Follow/LookAt → Player
        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player)
        {
            vcam.Follow = player.transform;
            vcam.LookAt = null; // 2D thường không cần LookAt
        }

        // Confiner → CameraBounds trong scene hiện tại
        if (confiner)
        {
            var go = GameObject.Find(boundsObjectName);
            if (go)
            {
                var col = go.GetComponent<Collider2D>();
                if (col)
                {
                    confiner.BoundingShape2D = col;
                    confiner.InvalidateBoundingShapeCache(); // mới trong v3.x
                }
            }
        }
    }
}
