using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    static public UIManager S { get; private set; }

    [Header("Live HUD — drag TMP Text objects here")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI timeAliveText;
    public TextMeshProUGUI bestTimeText;

    [Header("Game Over Panel")]
    public TextMeshProUGUI gameOverText;

    private int   _score     = 0;
    private float _timeAlive = 0f;
    private bool  _gameOver  = false;

    // Persistent bests loaded from PlayerPrefs
    private int   _highScore;
    private float _bestTime;

    void Awake()
    {
        if (S != null)
        {
            Debug.LogError("UIManager: duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        S = this;

        // Load saved records
        _highScore = PlayerPrefs.GetInt("HighScore", 0);
        _bestTime  = PlayerPrefs.GetFloat("BestTime", 0f);

        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        RefreshAll();
    }

    void Update()
    {
        if (_gameOver) return;
        _timeAlive += Time.deltaTime;
        if (timeAliveText != null)
            timeAliveText.text = "Time: " + FormatTime(_timeAlive);
    }

    // ── Called by Main.SHIP_DESTROYED ───────────────────────────────────────
    public void AddScore(int points)
    {
        _score += points;
        if (scoreText != null)
            scoreText.text = "Score: " + _score.ToString("N0");

        if (_score > _highScore)
        {
            _highScore = _score;
            PlayerPrefs.SetInt("HighScore", _highScore);
            if (highScoreText != null)
                highScoreText.text = "Best Score: " + _highScore.ToString("N0");
        }
    }

    // ── Called by Main.HERO_DIED ─────────────────────────────────────────────
    public void ShowGameOver()
    {
        _gameOver = true;

        // Save best time if beaten
        if (_timeAlive > _bestTime)
        {
            _bestTime = _timeAlive;
            PlayerPrefs.SetFloat("BestTime", _bestTime);
            if (bestTimeText != null)
                bestTimeText.text = "Best Time: " + FormatTime(_bestTime);
        }

        PlayerPrefs.Save();

        if (gameOverText != null)
        {
            gameOverText.text =
                "GAME OVER\n" +
                "Score: "     + _score.ToString("N0")   + "\n" +
                "High Score: "+ _highScore.ToString("N0")+ "\n" +
                "Time Alive: "+ FormatTime(_timeAlive)   + "\n" +
                "Best Time: " + FormatTime(_bestTime);
            gameOverText.gameObject.SetActive(true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }

    private void RefreshAll()
    {
        if (scoreText     != null) scoreText.text     = "Score: 0";
        if (highScoreText != null) highScoreText.text = "Best Score: " + _highScore.ToString("N0");
        if (timeAliveText != null) timeAliveText.text = "Time: 00:00";
        if (bestTimeText  != null) bestTimeText.text  = "Best Time: "  + FormatTime(_bestTime);
    }
}
