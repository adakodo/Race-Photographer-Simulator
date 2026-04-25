using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Game/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("基本資訊")]
    public string levelName = "Level 1";
    [TextArea] public string description = "";

    [Header("時間")]
    [Min(10f)] public float timeLimit = 120f;
    public float countdownDuration = 3f;

    [Header("過關條件（N 張 ≥ X 分）")]
    [Min(1)] public int requiredCount = 3;
    [Range(0f, 100f)] public float requiredScore = 70f;

    [Header("★★ 條件")]
    public int star2Count = 6;
    public float star2BestScore = 90f;

    [Header("★★★ 條件")]
    public int star3Count = 9;
    public float star3BestScore = 90f;

    public int EvaluateStars(int validCount, float bestScore)
    {
        if (validCount < requiredCount) return 0;
        if (validCount >= star3Count && bestScore >= star3BestScore) return 3;
        if (validCount >= star2Count || bestScore >= star2BestScore) return 2;
        return 1;
    }
}
