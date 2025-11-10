using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelRespawnManager : MonoBehaviour
{
    public static LevelRespawnManager Instance { get; private set; }

    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Animator fadeAnimator;
    [SerializeField] private float fadeDuration = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        if (fadeAnimator)
        {
            fadeAnimator.SetTrigger("FadeOut");
            yield return new WaitForSeconds(fadeDuration);
        }

        var player = GameObject.FindGameObjectWithTag("Player");

        if (!player)
        {
            // Nếu lỡ destroy mất player → reload scene
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.name);
            yield break;
        }

        var controller = player.GetComponent<PlayerController1>();
        var rb = player.GetComponent<Rigidbody2D>();
        var health = player.GetComponent<PlayerHealth>();
        var mana = player.GetComponent<PlayerMana>();

        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (respawnPoint)
            player.transform.position = respawnPoint.position;

        if (health)
        {
            float need = health.MaxHealth - health.CurrentHealth;
            if (need > 0) health.Heal(need);
            controller?.SetDead(false);
        }

        //if (mana)
        //{
        //    float needMp = mana.Max - mana.Current;
        //    if (needMp > 0) mana.AddMana(needMp);
        //}

        if (controller && !controller.enabled)
            controller.enabled = true;
    }
}
