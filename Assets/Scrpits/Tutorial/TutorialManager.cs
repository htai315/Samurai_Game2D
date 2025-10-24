using UnityEngine;
using System.Collections;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("Refs")]
    public TutorialInputFilter filter;     // gắn trên Player
    public TMP_Text hintText;              // TextMeshPro (UI)
    public GameObject enemyPrefab;         // quái để farm mana (stage 2/4)
    public Transform enemySpawnPoint;      // vị trí spawn

    [Header("Bounds per stage (Left/Right)")]
    public Transform[] stageLefts;         // StageX_Bounds/Left theo thứ tự 0..N
    public Transform[] stageRights;        // StageX_Bounds/Right theo thứ tự 0..N

    [Header("Advance-by-Right-Edge")]
    [Tooltip("Các collider trigger đặt ở mép phải từng phòng. Mỗi stage 1 trigger, để trống nếu dùng proximity check.")]
    public Collider2D[] rightEdgeTriggers; // enable khi đã đạt goal, disable lúc chưa đạt
    [Tooltip("Không dùng trigger: tự động cho qua khi đã đạt goal và tiến sát Right bound trong khi đang đi sang phải.")]
    public bool useProximityAdvanceIfNoTrigger = true;
    [Range(0.02f, 0.5f)]
    public float rightEdgeProximity = 0.15f; // khoảng cách “gần mép phải” cho proximity check

    // runtime
    private BoundsLimiter limiter;         // lấy từ Player
    private PlayerMana mana;               // kiểm tra mana
    private Transform player;              // tham chiếu player
    private Rigidbody2D rb;

    // state
    private int stage = 0;
    private int moveCount;
    private int jumpCount;
    private int hitCount;
    private bool goalCompleted = false;
    private bool dashUnlocked = false;
    private bool dashedOnce = false;

    private const float requiredMana = 5f; // đủ để dash 1 lần

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (!p)
        {
            Debug.LogError("[TutorialManager] Không tìm thấy Player (tag=Player).");
            enabled = false; return;
        }

        player = p.transform;
        rb = p.GetComponent<Rigidbody2D>();
        mana = p.GetComponent<PlayerMana>();
        limiter = p.GetComponent<BoundsLimiter>();
        if (!limiter) Debug.LogWarning("[TutorialManager] Player chưa có BoundsLimiter.");

        GoToStage(0);
    }

    void Update()
    {
        switch (stage)
        {
            case 0: // MOVE
                if (!goalCompleted)
                {
                    if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ||
                        Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        moveCount++;
                        if (moveCount >= 2)
                            SetGoalCompleted("Tốt! Tiến tới mép phải để sang bài tiếp theo →");
                    }
                }
                CheckRightEdgeAdvance();
                break;

            case 1: // JUMP
                if (!goalCompleted)
                {
                    if (Input.GetKeyDown(KeyCode.K))
                    {
                        jumpCount++;
                        if (jumpCount >= 2) // coi như đã thử double jump
                            SetGoalCompleted("Hay lắm! Di chuyển tới mép phải để tiếp tục →");
                    }
                }
                CheckRightEdgeAdvance();
                break;

            case 2: // ATTACK + COMBO (vẫn cho K để nhảy)
                if (!goalCompleted && Input.GetKeyDown(KeyCode.J))
                {
                    bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                    bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                    if (up || down)
                        SetGoalCompleted("Đã thử combo! Đi tới mép phải để qua bài kế →");
                }
                CheckRightEdgeAdvance();
                break;

            case 3: // JUMP + ATTACK (air attack)
                if (!goalCompleted && Input.GetKeyDown(KeyCode.K))
                    StartCoroutine(WaitForAirAttack());
                CheckRightEdgeAdvance();
                break;

            case 4: // DASH (cần mana) — vẫn cho Move/Jump/Attack để farm
                if (!dashUnlocked && mana && mana.Current >= requiredMana)
                {
                    dashUnlocked = true;
                    hintText.text = "Đủ mana rồi! Nhấn L để Dash (ít nhất 1 lần), sau đó đi tới mép phải để tiếp tục →";
                    filter.Set(true, true, true, true); // mở thêm Dash
                }

                if (dashUnlocked && !goalCompleted && Input.GetKeyDown(KeyCode.L))
                {
                    dashedOnce = true;
                    SetGoalCompleted("Tốt! Đi tới mép phải để sang bài cuối →");
                }

                CheckRightEdgeAdvance();
                break;

            case 5: // DONE
                // có thể hiện nút "Tiếp tục" để load scene chính
                break;
        }
    }

    private IEnumerator WaitForAirAttack()
    {
        yield return new WaitForSeconds(0.3f);
        if (!goalCompleted && Input.GetKey(KeyCode.J))
            SetGoalCompleted("Đẹp! Đi tới mép phải để sang bài Dash →");
    }

    private void GoToStage(int s)
    {
        stage = s;
        goalCompleted = false;
        dashUnlocked = false;
        dashedOnce = false;

        // Cập nhật bounds cho Player
        if (limiter && s < stageLefts.Length && s < stageRights.Length)
            limiter.SetBounds(stageLefts[s], stageRights[s]);

        // Vô hiệu trigger mép phải (chỉ bật lại khi đã đạt goal)
        ArmRightTrigger(false);

        switch (stage)
        {
            case 0:
                hintText.text = "Di chuyển: A/D";
                filter.Set(true, false, false, false);
                moveCount = 0;
                break;

            case 1:
                hintText.text = "Nhảy: K (nhấn 2 lần để nhảy cao hơn)";
                filter.Set(true, true, false, false);
                jumpCount = 0;
                break;

            case 2:
                hintText.text = "Tấn công: J  •  Combo: W+J (từ trên xuống), S+J (xoáy)\n(Bạn vẫn có thể nhảy K).";
                filter.Set(true, true, true, false); // Move + Jump + Attack
                hitCount = 0;
                if (enemyPrefab && enemySpawnPoint)
                    Instantiate(enemyPrefab, enemySpawnPoint.position, Quaternion.identity);
                break;

            case 3:
                hintText.text = "Nhảy (K) rồi tấn công (J) trên không.";
                filter.Set(true, true, true, false);
                break;

            case 4:
                hintText.text = "Đánh quái để tích mana (≥ 5). Bạn có thể dùng K và J để farm.";
                filter.Set(true, true, true, false); // farm trước, đủ mana sẽ mở Dash
                break;

            case 5:
                hintText.text = "Hoàn thành tutorial! Vào cổng dịch chuyển để bước lên con đường báo thù...";
                filter.Set(true, true, true, true);
                break;
        }
    }

    private void NextStage() => GoToStage(stage + 1);

    // === Goal & Right-edge handling ===

    private void SetGoalCompleted(string nextHint)
    {
        goalCompleted = true;
        hintText.text = nextHint;
        ArmRightTrigger(true); // bật vùng trigger mép phải của stage hiện tại (nếu có)
    }

    // TransitionTrigger (ở mép phải) sẽ gọi hàm này khi Player chạm
    public void TryAdvanceAtRightEdge()
    {
        if (!goalCompleted) return; // chưa đạt goal thì không cho qua
        NextStage();
    }

    // Bật/ tắt collider trigger mép phải cho stage hiện tại
    private void ArmRightTrigger(bool on)
    {
        if (rightEdgeTriggers == null) return;
        if (stage < 0 || stage >= rightEdgeTriggers.Length) return;
        var t = rightEdgeTriggers[stage];
        if (t) t.enabled = on;
    }

    // Fallback: nếu bạn KHÔNG đặt trigger, có thể cho qua bằng proximity + đang đi sang phải
    private void CheckRightEdgeAdvance()
    {
        if (!goalCompleted) return;

        // Nếu có trigger, ưu tiên trigger (không dùng proximity)
        if (rightEdgeTriggers != null && stage < rightEdgeTriggers.Length && rightEdgeTriggers[stage] != null)
            return;

        if (!useProximityAdvanceIfNoTrigger) return;
        if (player == null || stage >= stageRights.Length) return;

        float rightX = stageRights[stage].position.x - rightEdgeProximity;
        bool movingRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || (rb && rb.linearVelocity.x > 0.2f);

        if (player.position.x >= rightX && movingRight)
            NextStage();
    }
}
