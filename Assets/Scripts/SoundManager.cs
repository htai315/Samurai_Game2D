using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý toàn bộ hiệu ứng âm thanh (SFX) của trò chơi.
/// Tự động tổng hợp âm thanh thủ tục (Procedural Synthesis) nếu chưa gán AudioClip trong Inspector,
/// giúp game lập tức sống động 100% mà không đòi hỏi thêm tài nguyên file ngoài.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Source Pool")]
    [SerializeField] private int audioSourcePoolSize = 6;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("Audio Clips (Tùy chọn - Tự động tạo nếu để trống)")]
    public AudioClip customSlashClip;
    public AudioClip customHitClip;
    public AudioClip customParryClip;
    public AudioClip customCoinClip;
    public AudioClip customDashClip;
    public AudioClip customJumpClip;
    public AudioClip customFireballClip;
    public AudioClip customSwordWaveClip;
    public AudioClip customHurtClip;

    // Bộ nhớ cache các clip đã tổng hợp hoặc tải từ Resources
    private AudioClip procSlash;
    private AudioClip procHit;
    private AudioClip procParry;
    private AudioClip procCoin;
    private AudioClip procDash;
    private AudioClip procJump;
    private AudioClip procFireball;
    private AudioClip procSwordWave;
    private AudioClip procHurt;
    private AudioClip procBladeGlint;

    private AudioClip clipPerilous;
    private AudioClip clipHeartbeat;
    private AudioClip clipFootstep1;
    private AudioClip clipFootstep2;
    private AudioClip clipLanding;
    private AudioClip clipSheathe;
    private AudioClip clipCeramicBreak;

    private readonly List<AudioSource> audioPool = new List<AudioSource>();
    private int poolIndex = 0;
    private readonly List<AudioClip> ownedClips = new List<AudioClip>();
    private static bool footStepAlt = false;

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("SoundManager");
        Instance = go.AddComponent<SoundManager>();
        DontDestroyOnLoad(go);
    }
    public static void Prewarm() => EnsureInstance();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Tạo pool AudioSource để không bị giật lag khi phát nhiều âm thanh cùng lúc
        for (int i = 0; i < Mathf.Clamp(audioSourcePoolSize, 1, 32); i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D Sound
            audioPool.Add(src);
        }

        GenerateProceduralClips();
    }

    private void GenerateProceduralClips()
    {
        // Ưu tiên tải các file audio thu âm chất lượng cao từ Resources/Audio
        procSlash = Resources.Load<AudioClip>("Audio/katana_slash") ?? CreateNoiseSweep("Proc_Slash", 0.12f, 0.45f);
        procHit = Resources.Load<AudioClip>("Audio/flesh_impact") ?? CreatePunch("Proc_Hit", 0.14f, 0.55f);
        procParry = Resources.Load<AudioClip>("Audio/katana_parry") ?? CreateMetallicClash("Proc_Parry", 0.28f, 0.6f);
        clipPerilous = Resources.Load<AudioClip>("Audio/perilous_warning") ?? CreateMetallicClash("Proc_Perilous", 0.3f, 0.7f);
        clipHeartbeat = Resources.Load<AudioClip>("Audio/heartbeat_lowhp") ?? CreatePunch("Proc_Heartbeat", 0.35f, 0.8f);
        clipFootstep1 = Resources.Load<AudioClip>("Audio/footstep_1") ?? CreatePunch("Proc_Foot1", 0.08f, 0.4f);
        clipFootstep2 = Resources.Load<AudioClip>("Audio/footstep_2") ?? CreatePunch("Proc_Foot2", 0.08f, 0.4f);
        clipLanding = Resources.Load<AudioClip>("Audio/landing_thud") ?? CreatePunch("Proc_Land", 0.12f, 0.6f);
        clipSheathe = Resources.Load<AudioClip>("Audio/sheathe_click") ?? CreateMetallicClash("Proc_Sheathe", 0.16f, 0.5f);
        clipCeramicBreak = Resources.Load<AudioClip>("Audio/ceramic_break") ?? CreateExplosion("Proc_Ceramic", 0.25f, 0.7f);

        procCoin = CreateTwoTone("Proc_Coin", 987f, 1318f, 0.18f, 0.5f);
        procDash = CreateNoiseSweep("Proc_Dash", 0.15f, 0.35f);
        procJump = CreateChirp("Proc_Jump", 240f, 480f, 0.12f, 0.35f);
        procFireball = CreateExplosion("Proc_Fireball", 0.35f, 0.6f);
        procSwordWave = CreateChirp("Proc_SwordWave", 700f, 350f, 0.2f, 0.4f);
        procHurt = CreatePunch("Proc_Hurt", 0.2f, 0.5f);
        procBladeGlint = procParry; // Sử dụng âm kim loại thanh tao thay vì tiếng bíp cao tần
    }

    private void PlayClip(AudioClip clip, float pitch = 1f, float volumeMultiplier = 1f)
    {
        if (!clip || audioPool.Count == 0) return;

        for (int i = 0; i < audioPool.Count; i++)
        {
            int candidate = (poolIndex + i) % audioPool.Count;
            if (!audioPool[candidate].isPlaying) { poolIndex = candidate; break; }
        }
        var src = audioPool[poolIndex];
        poolIndex = (poolIndex + 1) % audioPool.Count;

        src.pitch = pitch;
        src.volume = Mathf.Clamp01(sfxVolume * volumeMultiplier);
        src.Stop();
        src.clip = clip;
        src.Play();
    }

    // ================= STATIC APIS =================

    public static void PlaySlash()
    {
        EnsureInstance();
        float randomPitch = Random.Range(0.92f, 1.08f);
        Instance.PlayClip(Instance.customSlashClip ? Instance.customSlashClip : Instance.procSlash, randomPitch, 0.95f);
    }

    public static void PlayHit()
    {
        EnsureInstance();
        float randomPitch = Random.Range(0.95f, 1.05f);
        Instance.PlayClip(Instance.customHitClip ? Instance.customHitClip : Instance.procHit, randomPitch, 1.05f);
    }

    public static void PlayParry()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customParryClip ? Instance.customParryClip : Instance.procParry, 1f, 1.2f);
    }

    public static void PlayPerilousWarning()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.clipPerilous, 1f, 1.25f);
    }

    public static void PlayHeartbeat()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.clipHeartbeat, Random.Range(0.97f, 1.03f), 1.15f);
    }

    public static void PlayFootstep()
    {
        EnsureInstance();
        footStepAlt = !footStepAlt;
        var clip = footStepAlt ? Instance.clipFootstep1 : Instance.clipFootstep2;
        Instance.PlayClip(clip, Random.Range(0.94f, 1.06f), 0.36f);
    }

    public static void PlayLanding()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.clipLanding, Random.Range(0.95f, 1.05f), 0.55f);
    }

    public static void PlaySheathe()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.clipSheathe, 1f, 0.8f);
    }

    public static void PlayCeramicBreak()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.clipCeramicBreak, Random.Range(0.92f, 1.08f), 0.95f);
    }

    public static void PlayCoin()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customCoinClip ? Instance.customCoinClip : Instance.procCoin, 1f, 0.9f);
    }

    public static void PlayDash()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customDashClip ? Instance.customDashClip : Instance.procDash, 1.1f);
    }

    public static void PlayJump()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customJumpClip ? Instance.customJumpClip : Instance.procJump, 1f);
    }

    public static void PlayFireball()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customFireballClip ? Instance.customFireballClip : Instance.procFireball, 1f, 1.1f);
    }

    public static void PlaySwordWave()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customSwordWaveClip ? Instance.customSwordWaveClip : Instance.procSwordWave, 1f);
    }

    public static void PlayHurt()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.customHurtClip ? Instance.customHurtClip : Instance.procHurt, 0.9f);
    }

    public static void PlayBladeGlint()
    {
        EnsureInstance();
        Instance.PlayClip(Instance.procBladeGlint, 1f, 1.1f);
    }

    // ================= PROCEDURAL AUDIO SYNTHESIZERS =================

    private AudioClip CreateCrystallinePing(string name, float baseFreq, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float s1 = Mathf.Sin(2 * Mathf.PI * baseFreq * i / sampleRate);
            float s2 = Mathf.Sin(2 * Mathf.PI * (baseFreq * 1.5f) * i / sampleRate) * 0.45f;
            float s3 = Mathf.Sin(2 * Mathf.PI * (baseFreq * 2.25f) * i / sampleRate) * 0.25f;
            float env = Mathf.Pow(1f - t, 4.5f);
            samples[i] = (s1 + s2 + s3) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateTwoTone(string name, float f1, float f2, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        int half = totalSamples / 2;
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float freq = i < half ? f1 : f2;
            float env = 1f - ((float)i / totalSamples);
            samples[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sampleRate) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateChirp(string name, float startFreq, float endFreq, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            float env = Mathf.Sin(t * Mathf.PI);
            samples[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sampleRate) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateNoiseSweep(string name, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float env = Mathf.Sin(t * Mathf.PI);
            samples[i] = (Random.value * 2f - 1f) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreatePunch(string name, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float lowFreq = Mathf.Lerp(120f, 40f, t);
            float sine = Mathf.Sin(2 * Mathf.PI * lowFreq * i / sampleRate);
            float noise = (Random.value * 2f - 1f) * 0.4f;
            float env = Mathf.Pow(1f - t, 2.5f);
            samples[i] = (sine + noise) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateMetallicClash(string name, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float sine1 = Mathf.Sin(2 * Mathf.PI * 1850f * i / sampleRate);
            float sine2 = Mathf.Sin(2 * Mathf.PI * 2730f * i / sampleRate) * 0.7f;
            float noise = (Random.value * 2f - 1f) * 0.3f;
            float env = Mathf.Pow(1f - t, 3f);
            samples[i] = (sine1 + sine2 + noise) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateExplosion(string name, float duration, float vol)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float lowRumble = Mathf.Sin(2 * Mathf.PI * 60f * i / sampleRate) * 0.5f;
            float noise = (Random.value * 2f - 1f) * 0.8f;
            float env = Mathf.Pow(1f - t, 1.8f);
            samples[i] = (lowRumble + noise) * env * vol;
        }

        var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        ownedClips.Add(clip);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var clip in ownedClips) if (clip) Destroy(clip);
    }
}
