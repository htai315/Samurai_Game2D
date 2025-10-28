// GameManager.cs
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    private int score = 0;
    [SerializeField] private TextMeshProUGUI scoreText;

    void Start() => UpdatScore();

    public void AddScore(int points)
    {
        score += points;
        Debug.Log("Score: " + score);
        UpdatScore();
    }

    public void UpdatScore()
    {
        if (scoreText) scoreText.text = score.ToString();
    }

    // >>>> THÊM 2 HÀM NÀY <<<<
    public int Score => score;
    public void SetScore(int value)
    {
        score = Mathf.Max(0, value);
        UpdatScore();
    }
    // ✨ NEW: Trừ coin có kiểm tra
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (score < amount) return false;
        score -= amount;
        UpdatScore();
        return true;
    }
}
