using UnityEngine;
using UnityEngine.SceneManagement;

public class SamuraiGuideUI : MonoBehaviour
{
    private static SamuraiGuideUI instance;
    public static SamuraiGuideUI Instance
    {
        get
        {
            if (!instance) instance = new GameObject("_SamuraiGuideUI").AddComponent<SamuraiGuideUI>();
            return instance;
        }
    }
    private bool isGuideOpen;
    public bool IsOpen => isGuideOpen && SamuraiModal.IsOwner(this);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() { _ = Instance; }
    private void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnDestroy()
    {
        CloseGuide();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }
    private void OnDisable() => CloseGuide();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CloseGuide();
    private void Update()
    {
        if (isGuideOpen && !SamuraiModal.IsOwner(this)) isGuideOpen = false;
        if (SamuraiModal.InputConsumed) return;
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (IsOpen) CloseGuide(); else OpenGuide();
        }
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseGuide();
    }
    public void OpenGuide()
    {
        if (isGuideOpen || !SamuraiModal.TryOpen(this)) return;
        isGuideOpen = true;
    }
    public void CloseGuide()
    {
        isGuideOpen = false;
        SamuraiModal.Close(this);
    }
    private static readonly string[,] Moves = {
        { "A / D", "Di chuyển", "Hoặc dùng phím mũi tên trái / phải." },
        { "K", "Nhảy & bám tường", "Nhấn để nhảy. Nhả sớm để nhảy thấp." },
        { "J", "Liên kích", "Chuỗi chém cận chiến. Có thể lướt để ngắt đòn." },
        { "L", "Lướt né", "Miễn sát thương trong lúc lướt; tiêu hao mana." },
        { "C / Shift", "Đỡ đòn", "Đỡ đúng nhịp: gây choáng và hồi mana." },
        { "U", "Kiếm khí", "Phóng kiếm khí tầm xa về phía đang nhìn." }
    };
    private static readonly string[,] Skills = {
        { "I", "Hỏa kiếm", "Thi triển đòn lửa về phía trước." },
        { "Q / O", "Nhất kiếm", "Lướt chém các mục tiêu trên đường đi." },
        { "Giữ H / E", "Tĩnh tâm", "Đứng trên đất, dùng mana để hồi máu." },
        { "P", "Rèn luyện", "Nâng thuộc tính và mua hộ phù." },
        { "B / W", "Đền thờ", "B mở hộ phù; W khi ở gần điểm nghỉ." },
        { "Esc", "Tạm dừng", "Mở cài đặt, lưu hoặc quay về menu." }
    };
    private void OnGUI()
    {
        if (!IsOpen) return;
        using (new SamuraiUI.Scope(-100))
        {
            var p = SamuraiUI.Modal("Bí kíp kiếm đạo", "Phím điều khiển và những điều cần biết trước khi giao chiến.", "SAMURAI  /  HƯỚNG DẪN");
            DrawColumn(new Rect(p.x + 28, p.y + 151, 510, 414), "THÂN THỦ & KIẾM PHÁP", Moves);
            DrawColumn(new Rect(p.x + 562, p.y + 151, 510, 414), "KỸ NĂNG & HÀNH TRANG", Skills);
            SamuraiUI.Text(new Rect(p.x + 28, p.y + 574, 740, 48),
                "Mẹo: đỡ đúng nhịp, lướt ra xa rồi tĩnh tâm hồi máu. Rơi khỏi màn vẫn gây tử vong.", 18, SamuraiUI.Muted, false, TextAnchor.MiddleLeft, true);
            if (SamuraiUI.Button(new Rect(p.xMax - 278, p.yMax - 65, 250, 42), "Đóng bí kíp   [F1 / Esc]")) CloseGuide();
        }
    }
    private static void DrawColumn(Rect r, string title, string[,] rows)
    {
        SamuraiUI.Text(new Rect(r.x, r.y, r.width, 28), title, 18, SamuraiUI.Gold, true);
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = r.y + 38 + i * 63;
            SamuraiUI.Fill(new Rect(r.x, y, r.width, 58), SamuraiUI.Card);
            SamuraiUI.Text(new Rect(r.x + 12, y + 8, 100, 38), rows[i, 0], 19, SamuraiUI.Gold, true, TextAnchor.MiddleCenter);
            SamuraiUI.Fill(new Rect(r.x + 120, y + 12, 1, 34), new Color(.3f, .27f, .22f));
            SamuraiUI.Text(new Rect(r.x + 136, y + 3, r.width - 148, 25), rows[i, 1], 21, SamuraiUI.Ivory, true);
            SamuraiUI.Text(new Rect(r.x + 136, y + 29, r.width - 148, 28), rows[i, 2], 16, SamuraiUI.Muted, false, TextAnchor.UpperLeft, true);
        }
    }
}
