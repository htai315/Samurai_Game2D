using UnityEngine;
using Samurai.Core.Presentation;
using UnityEngine.SceneManagement;

public class ComboSystem : MonoBehaviour
{
    private static ComboSystem _instance;
    public static ComboSystem Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("_ComboSystem");
                _instance = go.AddComponent<ComboSystem>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Combo Configuration")]
    [SerializeField] private float comboTimeout = 2.4f;

    private int currentCombo = 0;
    private int maxComboRun = 0;
    private float timer = 0f;
    private float punchScale = 1f;
    private float displayAlpha = 0f;

    private string lastRewardText = "";
    private float rewardTextTimer = 0f;

    public int CurrentCombo => currentCombo;
    public int MaxComboRun => maxComboRun;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentCombo = 0;
        timer = displayAlpha = rewardTextTimer = 0f;
        lastRewardText = "";
        if (scene.name == "MenuScene") maxComboRun = 0;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (currentCombo > 0)
        {
            timer -= Time.deltaTime;
            displayAlpha = Mathf.Min(1f, displayAlpha + Time.deltaTime * 6f);

            // Punch animation decay
            if (punchScale > 1f)
            {
                punchScale = Mathf.MoveTowards(punchScale, 1f, Time.deltaTime * 2.5f);
            }

            if (timer <= 0f)
            {
                // Combo hết thời gian
                currentCombo = 0;
            }
        }
        else
        {
            if (displayAlpha > 0f)
            {
                displayAlpha = Mathf.MoveTowards(displayAlpha, 0f, Time.deltaTime * 3f);
            }
        }

        if (rewardTextTimer > 0f)
        {
            rewardTextTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Ghi nhận 1 đòn đánh trúng mục tiêu (kiếm, pogo, fireball, sword wave)
    /// </summary>
    public static void RecordHit(Vector3 hitPos = default, float damage = 1f)
    {
        Instance.InternalRecordHit(hitPos, damage);
    }

    private void InternalRecordHit(Vector3 hitPos, float damage)
    {
        currentCombo++;
        if (currentCombo > maxComboRun) maxComboRun = currentCombo;

        timer = comboTimeout;
        punchScale = 1.38f;
        displayAlpha = 1f;

        // Kiểm tra phần thưởng mốc Combo
        CheckMilestoneRewards(currentCombo);
    }

    private void CheckMilestoneRewards(int count)
    {
        if (count != 5 && count != 10 && count != 15 && count != 20 && count != 30) return;
        var player = PlayerContext.Current?.Player;
        if (!player || !player.isActiveAndEnabled) return;

        var mana = player.GetComponent<PlayerMana>();
        var wallet = PlayerContext.Resolve(player)?.Wallet;

        if (count == 5)
        {
            if (mana) mana.AddMana(5f);
            lastRewardText = "+5 mana";
            rewardTextTimer = 1.6f;
            SoundManager.PlayCoin();
        }
        else if (count == 10)
        {
            if (mana) mana.AddMana(10f);
            wallet?.TryCredit(2);
            lastRewardText = "+10 mana  /  +2 xu";
            rewardTextTimer = 2.0f;
            SoundManager.PlayCoin();
            CameraShaker.Shake(0.08f, 0.12f);
        }
        else if (count == 15 || count == 20 || count == 30)
        {
            if (mana) mana.AddMana(15f);
            wallet?.TryCredit(5);
            lastRewardText = "+15 mana  /  +5 xu";
            rewardTextTimer = 2.4f;
            SoundManager.PlayCoin();
            CameraShaker.Shake(0.18f, 0.25f);
        }
    }

    public ComboHudSnapshot Presentation => new ComboHudSnapshot(currentCombo, timer, comboTimeout,
        punchScale, displayAlpha, lastRewardText, rewardTextTimer);
    private void OnGUI() => ComboHudRenderer.Draw(Presentation);
}
