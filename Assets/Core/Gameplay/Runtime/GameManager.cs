using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { Idle, Countdown, Playing, TimeUp, Settlement }

    [Header("設定")]
    public LevelConfig levelConfig;
    public bool autoStart = true;

    [Header("參照")]
    public PhotoCaptureManager captureManager;
    public PhotoScoreEvaluator scorer;
    public PhotoRigController rigController;
    public PhotoFreeCam freeCam;

    public State CurrentState { get; private set; } = State.Idle;
    public float RemainingTime { get; private set; }
    public float CountdownValue { get; private set; }
    public IReadOnlyList<PhotoResult> Photos => photos;

    [System.Serializable]
    public class PhotoResult
    {
        public Texture2D texture;
        public PhotoScoreEvaluator.PhotoScore score;
    }

    List<PhotoResult> photos = new List<PhotoResult>();
    float countdownTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        SetInputEnabled(false);
        if (autoStart) StartLevel();
    }

    void OnEnable()
    {
        if (captureManager != null) captureManager.OnPhotoReady += OnPhotoReady;
    }

    void OnDisable()
    {
        if (captureManager != null) captureManager.OnPhotoReady -= OnPhotoReady;
    }

    void Update()
    {
        switch (CurrentState)
        {
            case State.Countdown:
                UpdateCountdown();
                break;
            case State.Playing:
                UpdatePlaying();
                break;
        }
    }

    // ── 公開 API ──

    public void StartLevel()
    {
        if (levelConfig == null)
        {
            Debug.LogError("[GameManager] 未設定 LevelConfig。");
            return;
        }

        photos.Clear();
        RemainingTime = levelConfig.timeLimit;
        CurrentState = State.Playing;
        Time.timeScale = 1f;
        SetInputEnabled(true);
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── 狀態更新 ──

    void UpdateCountdown()
    {
        countdownTimer -= Time.deltaTime;
        CountdownValue = Mathf.Ceil(countdownTimer);

        if (countdownTimer <= 0f)
        {
            CurrentState = State.Playing;
            SetInputEnabled(true);
        }
    }

    void UpdatePlaying()
    {
        // 目前無時間限制，自由拍攝
    }

    void EnterSettlement()
    {
        CurrentState = State.Settlement;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ── 照片收集 ──

    void OnPhotoReady(Texture2D tex)
    {
        if (CurrentState != State.Playing && CurrentState != State.TimeUp) return;

        var copy = new Texture2D(tex.width, tex.height, tex.format, false, false);
        Graphics.CopyTexture(tex, copy);
        copy.Apply(false, false);

        var result = new PhotoResult { texture = copy };
        if (scorer != null) result.score = scorer.Evaluate();
        photos.Add(result);
    }

    // ── 統計查詢 ──

    public int GetValidCount()
    {
        if (levelConfig == null) return 0;
        int count = 0;
        foreach (var p in photos)
            if (p.score != null && p.score.total >= levelConfig.requiredScore) count++;
        return count;
    }

    public float GetBestScore()
    {
        float best = 0f;
        foreach (var p in photos)
            if (p.score != null && p.score.total > best) best = p.score.total;
        return best;
    }

    public float GetAverageScore()
    {
        if (photos.Count == 0) return 0f;
        float sum = 0f;
        int count = 0;
        foreach (var p in photos)
        {
            if (p.score != null) { sum += p.score.total; count++; }
        }
        return count > 0 ? sum / count : 0f;
    }

    public bool IsPassed()
    {
        return GetValidCount() >= levelConfig.requiredCount;
    }

    public int GetStars()
    {
        if (levelConfig == null) return 0;
        return levelConfig.EvaluateStars(GetValidCount(), GetBestScore());
    }

    // ── 輸入控制 ──

    void SetInputEnabled(bool on)
    {
        if (rigController != null) rigController.enabled = on;
        if (freeCam != null) freeCam.inputEnabled = on;
    }
}
